namespace Hub.Application.Identity;

public sealed class IdentityService(IHubSessionFactory sessions, IPasswordService passwords)
{
    public async Task Bootstrap(string email, string password, CancellationToken ct = default)
    {
        await using var db = await sessions.CreateAsync(ct);
        if (await db.AnyAsync(db.Users, ct)) return;
        if (string.IsNullOrEmpty(password) || password.Length < 12) throw new InvalidOperationException("Set Bootstrap__Password to at least 12 characters.");
        var user = HubUser.Create(email, "Workspace Admin", UserRoles.Administrator, "General");
        user.SetInitialPassword(passwords.Hash(user, password));
        db.Add(user);
        await db.SaveChangesAsync(ct);
    }

    public async Task<AuthenticatedSession> SignIn(string email, string password, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password) || email.Length > 254 || password.Length > 256)
            throw new UseCaseException(ErrorKind.Validation, "Invalid credentials.");
        await using var db = await sessions.CreateAsync(ct);
        var normalizedEmail = email.Trim().ToLowerInvariant();
        var user = await db.SingleOrDefaultAsync(db.Users.Where(u => u.Email == normalizedEmail), ct);
        if (user == null || !user.CanSignIn(DateTime.UtcNow))
        {
            passwords.Hash(HubUser.Create("timing@atlas.invalid", "Timing", UserRoles.Member, "General"), password);
            throw InvalidCredentials();
        }
        if (!passwords.Verify(user, password))
        {
            user.RecordFailedLogin(DateTime.UtcNow);
            await db.SaveChangesAsync(ct);
            throw InvalidCredentials();
        }
        user.RecordSuccessfulLogin();
        db.Add(new AuditEntry { Actor = user.Id.ToString(), Action = "SignIn" });
        await db.SaveChangesAsync(ct);
        return new AuthenticatedSession(user.Profile(), user.SessionVersion);
    }

    public async Task<HubUser> GetUser(Guid id, CancellationToken ct = default)
    {
        await using var db = await sessions.CreateAsync(ct);
        return await db.FirstOrDefaultAsync(db.Users.Where(u => u.Id == id && u.Enabled), ct)
            ?? throw new UseCaseException(ErrorKind.Unauthorized, "Your session has expired.");
    }

    public async Task<bool> IsSessionValid(Guid id, string? version, CancellationToken ct)
    {
        await using var db = await sessions.CreateAsync(ct);
        var user = await db.FirstOrDefaultAsync(db.Users.Where(u => u.Id == id && u.Enabled), ct);
        return user != null && user.SessionVersion.ToString() == version;
    }

    public async Task<HubUser> GetSessionUser(Guid id, string version, CancellationToken ct)
    {
        if (!int.TryParse(version, out var sessionVersion)) throw new UseCaseException(ErrorKind.Unauthorized, "Your session has expired.");
        await using var db = await sessions.CreateAsync(ct);
        return await db.FirstOrDefaultAsync(db.Users.Where(u => u.Id == id && u.Enabled && u.SessionVersion == sessionVersion), ct)
            ?? throw new UseCaseException(ErrorKind.Unauthorized, "Your session has expired.");
    }

    public async Task ChangePassword(Guid id, string currentPassword, string newPassword, CancellationToken ct)
    {
        await using var db = await sessions.CreateAsync(ct);
        var user = await db.SingleAsync(db.Users.Where(u => u.Id == id && u.Enabled), ct);
        if (string.IsNullOrEmpty(newPassword) || string.IsNullOrEmpty(currentPassword) || newPassword.Length is < 12 or > 128 || !passwords.Verify(user, currentPassword))
            throw new UseCaseException(ErrorKind.Validation, "Check your current password. New passwords require 12-128 characters.");
        user.ChangePassword(passwords.Hash(user, newPassword));
        db.Add(new AuditEntry { Actor = id.ToString(), Action = "PasswordChanged" });
        await db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<UserProfile>> ListUsers(HubUser actor, CancellationToken ct)
    {
        actor.RequireAdministrator();
        await using var db = await sessions.CreateAsync(ct);
        return (await db.ListAsync(db.Users.OrderBy(u => u.Name), ct)).Select(u => u.Profile()).ToList();
    }

    public async Task<UserProfile> CreateUser(HubUser actor, string email, string name, string password, string role, string team, CancellationToken ct)
    {
        actor.RequireAdministrator();
        if (string.IsNullOrEmpty(password) || password.Length is < 12 or > 128) throw new UseCaseException(ErrorKind.Validation, "Passwords require 12–128 characters.");
        var user = HubUser.Create(email, name, role, team);
        await using var db = await sessions.CreateAsync(ct);
        if (await db.AnyAsync(db.Users.Where(u => u.Email == user.Email), ct)) throw new UseCaseException(ErrorKind.Conflict, "This email already has an account.");
        user.SetInitialPassword(passwords.Hash(user, password));
        db.Add(user);
        db.Add(new AuditEntry { Actor = actor.Id.ToString(), Action = "UserCreated", Detail = user.Id.ToString() });
        await db.SaveChangesAsync(ct);
        return user.Profile();
    }

    public async Task SetEnabled(HubUser actor, Guid id, bool enabled, CancellationToken ct)
    {
        actor.RequireAdministrator();
        if (id == actor.Id) throw new UseCaseException(ErrorKind.Validation, "You cannot disable your own account.");
        await using var db = await sessions.CreateAsync(ct);
        var user = await db.FirstOrDefaultAsync(db.Users.Where(u => u.Id == id), ct) ?? throw new UseCaseException(ErrorKind.NotFound, "Account not found.");
        user.SetEnabled(enabled);
        await db.SaveChangesAsync(ct);
    }

    private static UseCaseException InvalidCredentials() => new(ErrorKind.Unauthorized, "Email or password is incorrect, or the account is locked.");
}

using Hub.Domain.Common;
using Hub.Domain.Workspace;

namespace Hub.Domain.Identity;

public sealed class HubUser : IAggregateRoot
{
    private HubUser() { }
    public Guid Id { get; private init; } = Guid.NewGuid();
    public string Email { get; private init; } = "";
    public string Name { get; private init; } = "";
    public string PasswordHash { get; private set; } = "";
    public string Role { get; private init; } = UserRoles.Member;
    public string Department { get; private init; } = "General";
    public bool Enabled { get; private set; } = true;
    public int FailedLogins { get; private set; }
    public DateTime? LockedUntil { get; private set; }
    public int SessionVersion { get; private set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public static HubUser Create(string email, string name, string role, string team)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > 100) throw new DomainRuleException("Names require 1–100 characters.");
        if (role is not (UserRoles.Administrator or UserRoles.Member)) throw new DomainRuleException("Unknown account role.");
        return new HubUser { Email = new EmailAddress(email).Value, Name = name.Trim(), Role = role, Department = new TeamName(team).Value };
    }

    public bool CanSignIn(DateTime now) => Enabled && !(LockedUntil > now);

    public void RecordFailedLogin(DateTime now)
    {
        FailedLogins++;
        if (FailedLogins < 5) return;
        LockedUntil = now.AddMinutes(15);
        FailedLogins = 0;
    }

    public void RecordSuccessfulLogin() { FailedLogins = 0; LockedUntil = null; }

    public void SetInitialPassword(string hash)
    {
        if (PasswordHash.Length != 0) throw new DomainRuleException("The account already has a password.", true);
        ArgumentException.ThrowIfNullOrWhiteSpace(hash);
        PasswordHash = hash;
    }

    public void ChangePassword(string hash)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(hash);
        PasswordHash = hash;
        SessionVersion++;
    }

    public void SetEnabled(bool enabled) { Enabled = enabled; SessionVersion++; }

    public void EndSessions() => SessionVersion++;
}

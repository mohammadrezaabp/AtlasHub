using Microsoft.AspNetCore.Identity;

namespace Hub.Infrastructure.Security;

public sealed class PasswordService : IPasswordService
{
    private readonly PasswordHasher<HubUser> hasher = new();
    public string Hash(HubUser user, string password) => hasher.HashPassword(user, password);
    public bool Verify(HubUser user, string password) => hasher.VerifyHashedPassword(user, user.PasswordHash, password) != PasswordVerificationResult.Failed;
}

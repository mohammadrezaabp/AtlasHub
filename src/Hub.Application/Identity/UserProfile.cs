namespace Hub.Application.Identity;

public sealed record UserProfile(Guid Id, string Name, string Email, string Role, string Department, bool Enabled);

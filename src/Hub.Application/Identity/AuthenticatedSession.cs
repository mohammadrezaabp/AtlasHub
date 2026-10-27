namespace Hub.Application.Identity;

public sealed record AuthenticatedSession(UserProfile Profile, int SessionVersion);

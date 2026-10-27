namespace Hub.Application.Identity;

public static class UserProfiles
{
    public static UserProfile Profile(this HubUser user) => new(user.Id, user.Name, user.Email, user.Role, user.Department, user.Enabled);

    public static void RequireAdministrator(this HubUser user)
    {
        if (user.Role != UserRoles.Administrator) throw new UseCaseException(ErrorKind.Forbidden, "Administrator access is required.");
    }
}

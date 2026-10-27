using System.Security.Claims;

namespace Hub.Web.Security;

public sealed class CurrentUser(IHttpContextAccessor accessor, IdentityService identity)
{
    public Guid Id => Guid.TryParse(accessor.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier), out var id)
        ? id : throw new UseCaseException(ErrorKind.Unauthorized, "Your session has expired.");

    public Task<HubUser> Get(CancellationToken ct = default) => identity.GetUser(Id, ct);
    public string SessionVersion => accessor.HttpContext?.User.FindFirstValue("version")
        ?? throw new UseCaseException(ErrorKind.Unauthorized, "Your session has expired.");
}

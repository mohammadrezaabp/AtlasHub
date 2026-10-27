using Hub.Web.Contracts.Approvals;
using Hub.Web.Contracts.Identity;
using Hub.Web.Security;

namespace Hub.Web.Endpoints;

public static class AdminEndpoints
{
    public static void Map(RouteGroupBuilder api)
    {
        api.MapGet("/approvals", async (CurrentUser user, ApprovalService approvals, CancellationToken ct) => Results.Ok(await approvals.List(await user.Get(ct), ct)))
            .RequireAuthorization(AuthorizationPolicies.Administrator);
        api.MapPost("/approvals/{id:guid}", async (Guid id, ApprovalRequest request, CurrentUser user, ApprovalService approvals, CancellationToken ct) =>
        {
            await approvals.Resolve(await user.Get(ct), id, request.Approve, ct);
            return Results.Ok();
        }).RequireAuthorization(AuthorizationPolicies.Administrator);
        api.MapGet("/models", async (CurrentUser user, ModelCatalog models, CancellationToken ct) => Results.Ok(models.List(await user.Get(ct))))
            .RequireAuthorization(AuthorizationPolicies.Administrator);
        var admin = api.MapGroup("/admin").RequireAuthorization(AuthorizationPolicies.Administrator);
        admin.MapGet("/users", async (CurrentUser user, IdentityService identity, CancellationToken ct) => Results.Ok(await identity.ListUsers(await user.Get(ct), ct)));
        admin.MapPost("/users", async (NewUserRequest request, CurrentUser user, IdentityService identity, CancellationToken ct) => Results.Ok(
            await identity.CreateUser(await user.Get(ct), request.Email, request.Name, request.Password, request.Role, request.Department, ct)));
        admin.MapPost("/users/{id:guid}/enabled", async (Guid id, EnabledRequest request, CurrentUser user, IdentityService identity, CancellationToken ct) =>
        {
            await identity.SetEnabled(await user.Get(ct), id, request.Enabled, ct);
            return Results.Ok();
        });
        admin.MapGet("/usage", async (CurrentUser user, WorkspaceService workspace, CancellationToken ct) => Results.Ok(await workspace.Usage(await user.Get(ct), ct)));
        admin.MapGet("/audit", async (CurrentUser user, WorkspaceService workspace, CancellationToken ct) => Results.Ok(await workspace.Audit(await user.Get(ct), ct)));
        admin.MapGet("/system", async (ISystemMonitor monitor, CancellationToken ct) => Results.Ok(await monitor.Status(ct)));
    }
}

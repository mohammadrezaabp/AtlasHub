using Hub.Web.Contracts.Knowledge;
using Hub.Web.Security;

namespace Hub.Web.Endpoints;

public static class WorkspaceEndpoints
{
    public static void Map(RouteGroupBuilder api)
    {
        api.MapGet("/overview", async (CurrentUser user, WorkspaceService workspace, CancellationToken ct) => Results.Ok(await workspace.Overview(await user.Get(ct), ct)));
        api.MapPost("/search", async (SearchRequest request, CurrentUser user, RetrievalService retrieval, CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(request.Query) || request.Query.Length > 1200) throw new UseCaseException(ErrorKind.Validation, "Enter a search query under 1,200 characters.");
            return Results.Ok(await retrieval.Search(request.Query, await user.Get(ct), true, ct));
        });
    }
}

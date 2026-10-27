using Hub.Web.Contracts.Conversations;
using Hub.Web.Security;

namespace Hub.Web.Endpoints;

public static class ConversationEndpoints
{
    public static void Map(RouteGroupBuilder api)
    {
        api.MapGet("/conversations", async (CurrentUser user, ConversationService conversations, CancellationToken ct) => Results.Ok(await conversations.List(user.Id, ct)));
        api.MapGet("/conversations/{id:guid}", async (Guid id, CurrentUser user, ConversationService conversations, CancellationToken ct) => Results.Ok(await conversations.Messages(user.Id, id, ct)));
        api.MapPatch("/conversations/{id:guid}", async (Guid id, RenameConversationRequest request, CurrentUser user, ConversationService conversations, CancellationToken ct) =>
        {
            await conversations.Rename(user.Id, id, request.Title, ct);
            return Results.Ok();
        });
        api.MapPut("/conversations/{id:guid}/pin", async (Guid id, PinConversationRequest request, CurrentUser user, ConversationService conversations, CancellationToken ct) =>
        {
            await conversations.SetPinned(user.Id, id, request.Pinned, ct);
            return Results.Ok(new { pinned = request.Pinned });
        });
        api.MapDelete("/conversations/{id:guid}", async (Guid id, CurrentUser user, ConversationService conversations, CancellationToken ct) =>
        {
            await conversations.Delete(user.Id, id, ct);
            return Results.Ok();
        });
        api.MapPost("/chat", async (ChatRequest request, CurrentUser user, ConversationService conversations, CancellationToken ct) =>
            Results.Ok(await conversations.Start(await user.Get(ct), request.Message, request.ConversationId, request.ImageDocumentId, ct)));
        api.MapPost("/runs/{id:guid}/stop", async (Guid id, CurrentUser user, ConversationService conversations, CancellationToken ct) =>
        {
            await conversations.Stop(user.Id, id, ct);
            return Results.Ok();
        });
        api.MapGet("/runs/{id:guid}/events", EventStreamEndpoint.Stream);
    }
}

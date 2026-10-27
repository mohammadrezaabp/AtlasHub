using Hub.Web.Contracts.Speech;
using Hub.Web.Security;

namespace Hub.Web.Endpoints;

public static class SpeechEndpoints
{
    public static void Map(RouteGroupBuilder api)
    {
        api.MapPost("/speech/transcribe", Transcribe).DisableAntiforgery();
        api.MapPost("/speech/synthesize", async (SpeechRequest request, HttpContext context, CurrentUser user, SpeechService speech) =>
        {
            var result = await speech.Synthesize(await user.Get(context.RequestAborted), request.Text, request.ConversationId, context.RequestAborted);
            context.Response.Headers["X-Audio-Id"] = result.AudioId.ToString();
            return Results.File(result.Bytes, "audio/mpeg");
        });
        api.MapGet("/voices", async (CurrentUser user, SpeechService speech, CancellationToken ct) => Results.Ok(await speech.List(await user.Get(ct), ct)));
        api.MapGet("/voices/{id:guid}/audio", async (Guid id, CurrentUser user, SpeechService speech, CancellationToken ct) =>
        {
            var file = await speech.Audio(await user.Get(ct), id, ct);
            return Results.File(file.Bytes, file.ContentType, enableRangeProcessing: true);
        });
    }

    private static async Task<IResult> Transcribe(HttpContext context, CurrentUser user, SpeechService speech)
    {
        var ct = context.RequestAborted;
        var form = await context.Request.ReadFormAsync(ct);
        var file = form.Files.GetFile("file");
        if (file == null || file.Length is <= 0 or > 10_000_000) throw new UseCaseException(ErrorKind.Validation, "Use an audio recording under 10 MB.");
        Guid? conversation = null;
        if (!string.IsNullOrEmpty(form["conversationId"]))
        {
            if (!Guid.TryParse(form["conversationId"], out var id)) throw new UseCaseException(ErrorKind.Validation, "Invalid conversation.");
            conversation = id;
        }
        using var memory = new MemoryStream();
        await file.CopyToAsync(memory, ct);
        return Results.Ok(await speech.Transcribe(await user.Get(ct), memory.ToArray(), file.ContentType, conversation, ct));
    }
}

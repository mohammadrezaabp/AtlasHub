using Hub.Application.Abstractions.Realtime;
using Hub.Application.Realtime;
using Hub.Web.Security;

namespace Hub.Web.Endpoints;

public static class RealtimeEndpoints
{
    public static void Map(RouteGroupBuilder api) => api.MapGet("/realtime/{view}", Stream).RequireRateLimiting("streams");

    private static async Task Stream(string view, HttpContext context, CurrentUser user, ProjectionService projections, IChangeFeed feed)
    {
        using var lifetime = await StreamLifetime.Create(context);
        var ct = lifetime.Token;
        using var changes = feed.Subscribe(RealtimeTopics.For(view, user.Id));
        var held = context.Request.Headers["Last-Event-ID"].FirstOrDefault() ?? context.Request.Query["fingerprint"].FirstOrDefault();
        if (held != null && (held.Length != 64 || held.Any(character => !char.IsAsciiHexDigit(character))))
            throw new UseCaseException(ErrorKind.Validation, "Invalid view fingerprint.");
        var update = await projections.Read(user.Id, user.SessionVersion, view, held, null, ct);
        context.Response.ContentType = "text/event-stream";
        context.Response.Headers.CacheControl = "no-store";
        context.Response.Headers["X-Accel-Buffering"] = "no";
        await context.Response.StartAsync(ct);
        try
        {
            while (!ct.IsCancellationRequested)
            {
                if (update.Frame != null) await context.Response.WriteAsync(ProjectionCodec.Event(update.Frame), ct);
                await context.Response.Body.FlushAsync(ct);
                var changed = await changes.Wait(TimeSpan.FromSeconds(5), ct);
                update = await projections.Read(user.Id, user.SessionVersion, view, null, update.Snapshot, ct);
                if (!changed && update.Frame == null) await context.Response.WriteAsync(": heartbeat\n\n", ct);
            }
        }
        catch (UseCaseException error) when (error.Kind is ErrorKind.Unauthorized or ErrorKind.Forbidden)
        {
            await context.Response.WriteAsync("data: {\"type\":\"subscription_closed\",\"reason\":\"session_revoked\"}\n\n", context.RequestAborted);
            await context.Response.Body.FlushAsync(context.RequestAborted);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
    }
}

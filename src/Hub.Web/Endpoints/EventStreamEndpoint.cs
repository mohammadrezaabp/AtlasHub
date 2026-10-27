using Hub.Application.Abstractions.Realtime;
using Hub.Application.Realtime;
using Hub.Web.Security;

namespace Hub.Web.Endpoints;

public static class EventStreamEndpoint
{
    public static async Task Stream(Guid id, HttpContext context, CurrentUser user, RunEventReader reader, IChangeFeed feed)
    {
        using var lifetime = await StreamLifetime.Create(context);
        var ct = lifetime.Token;
        using var changes = feed.Subscribe([RealtimeTopics.Run(id), RealtimeTopics.Session(user.Id)]);
        long.TryParse(context.Request.Query["after"].FirstOrDefault() ?? context.Request.Headers["Last-Event-ID"].FirstOrDefault(), out var last);
        if (last < 0) throw new UseCaseException(ErrorKind.Validation, "Invalid event cursor.");
        var page = await reader.Read(id, last, user.Id, user.SessionVersion, ct);
        context.Response.ContentType = "text/event-stream";
        context.Response.Headers.CacheControl = "no-cache";
        context.Response.Headers["X-Accel-Buffering"] = "no";
        await context.Response.StartAsync(ct);
        try
        {
            while (!ct.IsCancellationRequested)
            {
                foreach (var item in page.Events)
                {
                    await context.Response.WriteAsync($"id: {item.Id}\ndata: {item.Data}\n\n", ct);
                    last = item.Id;
                }
                await context.Response.Body.FlushAsync(ct);
                if (page.Finished) break;
                if (page.Events.Count < 300 && !await changes.Wait(TimeSpan.FromSeconds(5), ct))
                    await context.Response.WriteAsync(": heartbeat\n\n", ct);
                page = await reader.Read(id, last, user.Id, user.SessionVersion, ct);
            }
        }
        catch (UseCaseException error) when (error.Kind is ErrorKind.Unauthorized or ErrorKind.Forbidden or ErrorKind.NotFound)
        {
            var reason = error.Kind == ErrorKind.NotFound ? "unavailable" : "session_revoked";
            await context.Response.WriteAsync($"data: {{\"type\":\"subscription_closed\",\"reason\":\"{reason}\"}}\n\n", context.RequestAborted);
            await context.Response.Body.FlushAsync(context.RequestAborted);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
    }
}

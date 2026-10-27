using Hub.Web.Security;

namespace Hub.Web.Endpoints;

public static class EventStreamEndpoint
{
    public static async Task Stream(Guid id, HttpContext context, CurrentUser user, RunEventReader reader)
    {
        var ct = context.RequestAborted;
        await reader.EnsureOwner(id, user.Id, ct);
        long.TryParse(context.Request.Query["after"].FirstOrDefault() ?? context.Request.Headers["Last-Event-ID"].FirstOrDefault(), out var last);
        context.Response.ContentType = "text/event-stream";
        context.Response.Headers.CacheControl = "no-cache";
        context.Response.Headers["X-Accel-Buffering"] = "no";
        var ticks = 0;
        while (!ct.IsCancellationRequested)
        {
            var page = await reader.Read(id, last, ct);
            foreach (var item in page.Events)
            {
                await context.Response.WriteAsync($"id: {item.Id}\ndata: {item.Data}\n\n", ct);
                last = item.Id;
            }
            if (++ticks % 40 == 0) await context.Response.WriteAsync(": heartbeat\n\n", ct);
            await context.Response.Body.FlushAsync(ct);
            if (page.Finished) break;
            await Task.Delay(250, ct);
        }
    }
}

using Hub.Infrastructure.Security;

namespace Hub.Web.Endpoints;

public static class OperationalEndpoints
{
    public static void Map(WebApplication app)
    {
        app.MapGet("/health", async (MetricsService metrics, CancellationToken ct) =>
            await metrics.Healthy(ct) ? Results.Ok(new { status = "healthy" }) : Results.StatusCode(503));
        app.MapGet("/metrics", async (HttpContext context, IConfiguration config, MetricsService metrics, CancellationToken ct) =>
            InternalRequest.IsMetricsScrape(context.Request.Headers.Authorization, config)
                ? Results.Text(await metrics.Collect(ct), "text/plain; version=0.0.4")
                : Results.Unauthorized());
    }
}

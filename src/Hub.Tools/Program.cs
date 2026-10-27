using Hub.Infrastructure;
using Hub.Infrastructure.Security;
using Hub.Tools.Endpoints;
var builder = WebApplication.CreateBuilder(args);
builder.AddHubInfrastructure("atlas-mcp-tools");
var app = builder.Build();
app.MapGet("/health", () => Results.Ok(new { status = "healthy", service = "mcp-tools" }));
app.Use(async (ctx, next) =>
{
    if (ctx.Request.Path == "/health") { await next(); return; }
    if (!InternalRequest.IsAuthorized(ctx.Request.Headers["X-Hub-Token"], app.Configuration)) { ctx.Response.StatusCode = 401; return; }
    if (ctx.Request.Headers.ContainsKey("Origin")) { ctx.Response.StatusCode = 403; return; }
    await next();
});
McpEndpoints.Map(app);
app.Run();

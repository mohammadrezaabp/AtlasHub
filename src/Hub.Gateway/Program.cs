using Hub.Gateway.Proxy;
using Hub.Infrastructure;
using Hub.Infrastructure.Security;


var builder = WebApplication.CreateBuilder(args);
builder.AddHubInfrastructure("atlas-model-gateway");
builder.Services.AddSingleton<ModelGatewayProxy>();
var app = builder.Build();
app.MapGet("/health", () => Results.Ok(new { status = "healthy", service = "model-gateway" }));
app.Use(async (ctx, next) =>
{
    if (ctx.Request.Path == "/health") { await next(); return; }
    if (!InternalRequest.IsAuthorized(ctx.Request.Headers["X-Hub-Token"], app.Configuration))
    { ctx.Response.StatusCode = 401; return; }
    await next();
});
app.MapGet("/v1/catalog", async (ModelGatewayProxy state, CancellationToken ct) => await state.Catalog(ct));
app.MapPost("/v1/{endpoint}", async (string endpoint, HttpContext ctx, ModelGatewayProxy state) =>
    await state.Forward(endpoint, ctx));
app.MapPost("/v1/chat/completions", async (HttpContext ctx, ModelGatewayProxy state) => await state.Forward("chat/completions", ctx));
app.Run();

using System.Security.Claims;
using Hub.Web.Contracts.Identity;
using Hub.Web.Security;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace Hub.Web.Endpoints;

public static class AuthEndpoints
{
    public static void MapPublic(WebApplication app)
    {
        app.MapGet("/api/auth/csrf", (HttpContext context, IAntiforgery anti) => Results.Ok(new { token = anti.GetAndStoreTokens(context).RequestToken }));
        app.MapPost("/api/auth/login", SignIn).RequireRateLimiting("login");
    }

    public static void Map(RouteGroupBuilder api)
    {
        api.MapGet("/me", async (CurrentUser user, CancellationToken ct) => Results.Ok((await user.Get(ct)).Profile()));
        api.MapPost("/auth/logout", async (HttpContext context) => { await context.SignOutAsync(); return Results.Ok(); });
        api.MapPost("/auth/password", async (PasswordRequest request, CurrentUser user, IdentityService identity, HttpContext context) =>
        {
            await identity.ChangePassword(user.Id, request.CurrentPassword, request.NewPassword, context.RequestAborted);
            await context.SignOutAsync();
            return Results.Ok();
        });
    }

    private static async Task<IResult> SignIn(LoginRequest request, HttpContext context, IdentityService identity)
    {
        var session = await identity.SignIn(request.Email, request.Password, context.RequestAborted);
        var user = session.Profile;
        var claims = new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()), new Claim(ClaimTypes.Name, user.Name),
            new Claim(ClaimTypes.Role, user.Role), new Claim("version", session.SessionVersion.ToString())], CookieAuthenticationDefaults.AuthenticationScheme);
        await context.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(claims));
        return Results.Ok(user);
    }
}

using System.Security.Claims;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.AspNetCore.DataProtection.Repositories;

namespace Hub.Web.Security;

public static class WebSecurityRegistration
{
    public static void AddWebSecurity(this WebApplicationBuilder builder)
    {
        builder.Services.AddHttpContextAccessor();
        builder.Services.AddScoped<CurrentUser>();
        builder.Services.AddScoped<SessionCookieEvents>();
        builder.Services.AddAntiforgery(o => { o.HeaderName = "X-CSRF-TOKEN"; o.Cookie.SameSite = SameSiteMode.Strict; });
        builder.Services.AddDataProtection().SetApplicationName("AtlasHub");
        builder.Services.AddOptions<KeyManagementOptions>().Configure<IConfiguration, IHostEnvironment, ILoggerFactory>((options, config, environment, logs) =>
        {
            var keyPath = config["Security:KeyPath"] ?? Path.Combine(environment.ContentRootPath, "data", "keys");
            Directory.CreateDirectory(keyPath);
            options.XmlRepository = new FileSystemXmlRepository(new DirectoryInfo(keyPath), logs);
        });
        builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme).AddCookie(o =>
        {
            o.Cookie.Name = "atlas.session";
            o.Cookie.HttpOnly = true;
            o.Cookie.SameSite = SameSiteMode.Strict;
            o.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
            o.ExpireTimeSpan = TimeSpan.FromHours(8);
            o.SlidingExpiration = true;
            o.EventsType = typeof(SessionCookieEvents);
        });
        builder.Services.AddAuthorization(o => o.AddPolicy(AuthorizationPolicies.Administrator, p => p.RequireRole(UserRoles.Administrator)));
        builder.Services.AddRateLimiter(o =>
        {
            o.RejectionStatusCode = 429;
            o.AddPolicy("login", c => RateLimitPartition.GetFixedWindowLimiter(c.Connection.RemoteIpAddress?.ToString() ?? "local", _ =>
                new FixedWindowRateLimiterOptions { PermitLimit = 12, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
            o.AddPolicy("user", c => RateLimitPartition.GetFixedWindowLimiter(c.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "anonymous", _ =>
                new FixedWindowRateLimiterOptions { PermitLimit = 120, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
        });
    }
}

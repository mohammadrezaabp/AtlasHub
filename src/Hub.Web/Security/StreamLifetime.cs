using Microsoft.AspNetCore.Authentication;

namespace Hub.Web.Security;

public static class StreamLifetime
{
    public static async Task<CancellationTokenSource> Create(HttpContext context)
    {
        var ticket = await context.AuthenticateAsync();
        var expires = ticket.Properties?.ExpiresUtc ?? DateTimeOffset.UtcNow.AddMinutes(15);
        var remaining = expires - DateTimeOffset.UtcNow;
        var lifetime = CancellationTokenSource.CreateLinkedTokenSource(context.RequestAborted);
        lifetime.CancelAfter(remaining <= TimeSpan.Zero ? TimeSpan.Zero : remaining < TimeSpan.FromMinutes(15) ? remaining : TimeSpan.FromMinutes(15));
        return lifetime;
    }
}

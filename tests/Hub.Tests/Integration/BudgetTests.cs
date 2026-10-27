using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Hub.Tests.Integration;

public sealed class BudgetTests
{
    [Fact]
    public async Task Concurrent_reservations_cannot_overspend_and_failed_calls_release_their_reservation()
    {
        using var parent = new HubFactory(backgroundWorkers: false);
        using var factory = parent.WithWebHostBuilder(b => b.ConfigureServices(s => s.Configure<BudgetOptions>(o => { o.DailyUsd = 0.1m; o.MaxRequestUsd = 0.05m; })));
        using var client = factory.CreateClient();
        await client.GetAsync("/health");
        var accounting = factory.Services.GetRequiredService<UsageAccountingService>();
        async Task<ModelUsage?> Reserve()
        {
            try { return await accounting.Reserve("test-actor", "Chat", "test/model", default); }
            catch (UseCaseException e) when (e.Kind == ErrorKind.RateLimited) { return null; }
        }
        var reservations = await Task.WhenAll(Reserve(), Reserve(), Reserve());
        Assert.Equal(2, reservations.Count(r => r != null));
        var failed = reservations.First(r => r != null)!;
        failed.Status = "Failed";
        failed.CostUsd = 0;
        await accounting.Settle(failed);
        Assert.NotNull(await Reserve());
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<HubDb>();
        Assert.Equal(0.1m, (await db.Usages.ToListAsync()).Sum(u => u.CostUsd));
    }
}

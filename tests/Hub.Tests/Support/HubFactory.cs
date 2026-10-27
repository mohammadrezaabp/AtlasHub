using Hub.Web.Workers;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace Hub.Tests.Support;

public sealed class HubFactory : WebApplicationFactory<Program>
{
    readonly string directory = Path.Combine(Path.GetTempPath(), "atlas-test-" + Guid.NewGuid());
    private readonly bool backgroundWorkers;
    private readonly bool realtimeDelivery;
    public HubFactory(bool backgroundWorkers = true, bool realtimeDelivery = true)
    {
        this.backgroundWorkers = backgroundWorkers;
        this.realtimeDelivery = realtimeDelivery;
        Directory.CreateDirectory(directory);
    }
    protected override void ConfigureWebHost(IWebHostBuilder b)
    {
        b.UseContentRoot(Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../src/Hub.Web")));
        b.ConfigureAppConfiguration((_, c) => c.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Database:Provider"] = "Sqlite",
            ["ConnectionStrings:Hub"] = "Data Source=" + Path.Combine(directory, "test.db"),
            ["Bootstrap:Email"] = "admin@test.local",
            ["Bootstrap:Password"] = "Test-Admin-Password-2026!",
            ["Security:KeyPath"] = Path.Combine(directory, "keys"),
            ["Security:InternalToken"] = new string('x', 64),
            ["Storage:Provider"] = "Local",
            ["Storage:Path"] = Path.Combine(directory, "uploads"),
            ["Features:Rewrite"] = "false",
            ["Features:Rerank"] = "false"
        }));
        b.ConfigureServices(services =>
        {
            if (!realtimeDelivery)
            {
                foreach (var worker in services.Where(s => s.ServiceType == typeof(IHostedService) && s.ImplementationType == typeof(Hub.Infrastructure.Realtime.ChangeOutboxDispatcher)).ToList())
                    services.Remove(worker);
            }
            if (!backgroundWorkers)
            {
                foreach (var worker in services.Where(s => s.ServiceType == typeof(IHostedService) && s.ImplementationType == typeof(AgentWorker)).ToList())
                    services.Remove(worker);
            }
        });
    }
    protected override void Dispose(bool disposing)
    { base.Dispose(disposing); if (disposing) { Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools(); try { Directory.Delete(directory, true); } catch (IOException) { } } }
}

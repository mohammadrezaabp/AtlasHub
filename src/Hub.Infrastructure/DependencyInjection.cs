using Hub.Application;
using Hub.Infrastructure.AI;
using Hub.Infrastructure.Caching;
using Hub.Infrastructure.Events;
using Hub.Infrastructure.Monitoring;
using Hub.Infrastructure.Persistence.Repositories;
using Hub.Infrastructure.Security;
using Hub.Infrastructure.Speech;
using Hub.Infrastructure.Storage;
using Hub.Infrastructure.Tools;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
namespace Hub.Infrastructure;

public static class DependencyInjection
{
    public static void AddHubInfrastructure(this IHostApplicationBuilder b, string service)
    {
        b.Services.AddDbContextFactory<HubDb>(o =>
        {
            var cs = b.Configuration.GetConnectionString("Hub") ?? "Data Source=data/atlas.db";
            if (b.Configuration["Database:Provider"] == "PostgreSQL") o.UseNpgsql(cs);
            else o.UseSqlite(cs);
        });
        b.Services.AddHttpClient("internal", c => c.Timeout = TimeSpan.FromMinutes(3));
        b.Services.AddHttpClient("external", c => c.Timeout = TimeSpan.FromMinutes(3));
        b.Services.AddSingleton<RedisStore>();
        b.Services.AddHubApplication(b.Configuration);
        b.Services.AddSingleton<IHubSessionFactory, EfHubSessionFactory>();
        b.Services.AddSingleton<IConversationStore, ConversationStore>();
        b.Services.AddSingleton<IApprovalStore, ApprovalStore>();
        b.Services.AddSingleton<IDocumentIndexStore, DocumentIndexStore>();
        b.Services.AddSingleton<IRunStore, RunStore>();
        b.Services.AddSingleton<IModelClient, ModelClient>();
        b.Services.AddSingleton<IModelProviderCatalog, OpenRouterCatalog>();
        b.Services.AddSingleton<LocalFileStore>();
        b.Services.AddSingleton<MinioFileStore>();
        b.Services.AddSingleton<IFileStore>(services => b.Configuration["Storage:Provider"] == "Minio"
            ? services.GetRequiredService<MinioFileStore>() : services.GetRequiredService<LocalFileStore>());
        b.Services.AddSingleton<IStorageMaintenance, StorageMaintenance>();
        b.Services.AddSingleton<IEventStore, EventStore>();
        b.Services.AddSingleton<IEventMirror, RedisEventMirror>();
        b.Services.AddSingleton<IHubToolClient, HubToolClient>();
        b.Services.AddSingleton<IPasswordService, PasswordService>();
        b.Services.AddSingleton<IVectorIndex, QdrantVectorIndex>();
        b.Services.AddSingleton<IDocumentTextExtractor, DocumentTextExtractor>();
        b.Services.AddSingleton<ISpeechProvider, DeepgramSpeechProvider>();
        b.Services.AddSingleton<ISystemMonitor, SystemMonitor>();
        b.Services.AddSingleton<ISeedDocumentSource, SeedDocumentSource>();
        b.Logging.AddJsonConsole();
        b.Logging.AddFilter("System.Net.Http.HttpClient", LogLevel.Warning);
        b.Logging.AddFilter("Microsoft.EntityFrameworkCore.Database.Command", LogLevel.Warning);
        b.Services.AddOpenTelemetry().ConfigureResource(r => r.AddService(service))
            .WithTracing(t =>
            {
                t.AddSource("AtlasHub").AddAspNetCoreInstrumentation().AddHttpClientInstrumentation();
                if (!string.IsNullOrEmpty(b.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"])) t.AddOtlpExporter();
            });
    }

}

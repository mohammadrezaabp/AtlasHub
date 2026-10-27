using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Hub.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddHubApplication(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<FeatureOptions>(configuration.GetSection("Features"));
        services.AddOptions<BudgetOptions>().Bind(configuration.GetSection("Budget")).Validate(o => o.DailyUsd > 0 && o.MaxRequestUsd > 0 && o.MaxRequestUsd <= o.DailyUsd, "AI budget limits must be positive and a request cannot exceed the daily limit.").ValidateOnStart();
        services.AddSingleton<IdentityService>();
        services.AddSingleton<DocumentService>();
        services.AddSingleton<ConversationService>();
        services.AddSingleton<ApprovalService>();
        services.AddSingleton<WorkspaceService>();
        services.AddSingleton<SpeechService>();
        services.AddSingleton<RunEventReader>();
        services.AddSingleton<KnowledgeTools>();
        services.AddSingleton<RetrievalService>();
        services.AddSingleton<IngestionService>();
        services.AddSingleton<SeedImporter>();
        services.AddSingleton<AgentRunProcessor>();
        services.AddSingleton<QueryRewriter>();
        services.AddSingleton<ImageEvidenceReader>();
        services.AddSingleton<AgentToolLoop>();
        services.AddSingleton<ModelCatalog>();
        services.AddSingleton<MetricsService>();
        services.AddSingleton<UsageAccountingService>();
        services.AddSingleton<Hub.Application.Realtime.ProjectionService>();
        services.AddSingleton<Hub.Application.Realtime.RealtimeMetrics>();
        return services;
    }
}

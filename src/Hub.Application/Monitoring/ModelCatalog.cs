using Microsoft.Extensions.Options;

namespace Hub.Application.Monitoring;

public sealed class ModelCatalog(IModelClient models, IOptions<FeatureOptions> features)
{
    public IReadOnlyList<ModelRole> List(HubUser actor)
    {
        actor.RequireAdministrator();
        var flags = features.Value;
        return [new("Chat", models.Model("Chat"), flags.Chat), new("Fast", models.Model("Fast"), flags.Fast),
            new("Rerank", models.Model("Rerank"), flags.Rerank), new("Vision", models.Model("Vision"), flags.Vision),
            new("Embedding", models.Model("Embedding"), flags.Embedding), new("STT", "deepgram/nova-3", flags.Speech),
            new("TTS", "deepgram/aura-2-thalia-en", flags.Speech)];
    }
}

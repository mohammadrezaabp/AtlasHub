namespace Hub.Application.Configuration;

public sealed class FeatureOptions
{
    public bool Rewrite { get; set; } = true;
    public bool Rerank { get; set; } = true;
    public bool Vision { get; set; } = true;
    public bool Speech { get; set; } = true;
    public bool Chat { get; set; } = true;
    public bool Fast { get; set; } = true;
    public bool Embedding { get; set; } = true;
}

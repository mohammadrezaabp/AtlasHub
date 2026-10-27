namespace Hub.Application.Monitoring;

public sealed record WorkspaceOverview(int Documents, int Ready, int Chunks, int Conversations, decimal CostUsd, long Tokens, IReadOnlyList<DocumentSummary> Recent);

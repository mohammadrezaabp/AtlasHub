namespace Hub.Domain.Knowledge;

public static class DocumentStatus
{
    public const string Queued = "Queued";
    public const string Indexing = "Indexing";
    public const string Ready = "Ready";
    public const string LexicalOnly = "LexicalOnly";
    public const string Failed = "Failed";
}

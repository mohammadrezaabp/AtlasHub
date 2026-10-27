namespace Hub.Application.Realtime;

public static class RealtimeTopics
{
    public const string Documents = "documents";
    public static string Conversations(Guid user) => $"conversations:{user:N}";
    public static string Session(Guid user) => $"session:{user:N}";
    public static string Run(Guid run) => $"run:{run:N}";
    public static string Usage(Guid user) => $"usage:{user:N}";
    public static string[] For(string view, Guid user) => view switch
    {
        "documents" => [Documents, Session(user)],
        "conversations" => [Conversations(user), Session(user)],
        "overview" => [Documents, Conversations(user), Usage(user), Session(user)],
        _ => throw new UseCaseException(ErrorKind.Validation, "Unknown realtime view.")
    };
}

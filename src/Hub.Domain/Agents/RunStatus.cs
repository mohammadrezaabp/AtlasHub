namespace Hub.Domain.Agents;

public static class RunStatus
{
    public const string Queued = "Queued";
    public const string Running = "Running";
    public const string Succeeded = "Succeeded";
    public const string Cancelled = "Cancelled";
    public const string TimedOut = "TimedOut";
    public const string Failed = "Failed";
    public const string Interrupted = "Interrupted";
    public static bool IsTerminal(string status) => status is Succeeded or Cancelled or TimedOut or Failed or Interrupted;
}

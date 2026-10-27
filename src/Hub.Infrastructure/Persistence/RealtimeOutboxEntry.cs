namespace Hub.Infrastructure.Persistence;

public sealed class RealtimeOutboxEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Topic { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

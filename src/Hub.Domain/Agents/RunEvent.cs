namespace Hub.Domain.Agents;

public class RunEvent
{
    public long Id { get; set; }
    public Guid RunId { get; set; }
    public string Data { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

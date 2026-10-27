namespace Hub.Domain.Auditing;

public class AuditEntry
{
    public long Id { get; set; }
    public string Actor { get; set; } = "system";
    public string Action { get; set; } = "";
    public string Detail { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

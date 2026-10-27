namespace Hub.Domain.Usage;

public class ModelUsage
{
    public long Id { get; set; }
    public string Actor { get; set; } = "system";
    public string Role { get; set; } = "chat";
    public string Model { get; set; } = "";
    public int InputTokens { get; set; }
    public int OutputTokens { get; set; }
    public decimal CostUsd { get; set; }
    public bool CostReported { get; set; }
    public string Status { get; set; } = ModelUsageStatus.Succeeded;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

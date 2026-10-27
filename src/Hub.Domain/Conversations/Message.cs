namespace Hub.Domain.Conversations;

public class Message
{
    public long Id { get; set; }
    public Guid ConversationId { get; set; }
    public string Role { get; set; } = "user";
    public string Text { get; set; } = "";
    public string Sources { get; set; } = "[]";
    public Guid? RunId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

namespace GraspIt.Core.Entities;

public class ChatMessage
{
    public int Id { get; set; }
    public string Role { get; set; } = "user"; // "user", "assistant", "system"
    public string Content { get; set; } = string.Empty;
    public bool UsedRag { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public int SessionId { get; set; }
    public ChatSession Session { get; set; } = null!;
}

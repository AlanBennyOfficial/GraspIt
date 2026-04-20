namespace GraspIt.Core.Entities;

public class ChatFolder
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public int UserId { get; set; }
    public User User { get; set; } = null!;

    public ICollection<ChatSession> Sessions { get; set; } = new List<ChatSession>();
}

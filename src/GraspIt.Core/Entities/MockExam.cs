namespace GraspIt.Core.Entities;

public class MockExam
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    public int UserId { get; set; }
    public User User { get; set; } = null!;

    public ICollection<Question> Questions { get; set; } = new List<Question>();
}

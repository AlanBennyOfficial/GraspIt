namespace GraspIt.Core.Entities;

public class Question
{
    public int Id { get; set; }
    public string Text { get; set; } = string.Empty;
    public string Type { get; set; } = "MCQ"; // MCQ, ShortAnswer
    public string? OptionsJson { get; set; } // Serialized options if MCQ
    public string CorrectAnswer { get; set; } = string.Empty;
    public string Explanation { get; set; } = string.Empty;

    public int MockExamId { get; set; }
    public MockExam MockExam { get; set; } = null!;
}

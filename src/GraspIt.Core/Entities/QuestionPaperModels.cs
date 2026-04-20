namespace GraspIt.Core.Entities;

/// <summary>
/// Models that match the qn_format.json structure used by LLM_qn_paper_generator.js.
/// The LLM generates questions in this exact format so the Node.js generator can
/// produce a pixel-perfect DOCX question paper.
/// </summary>

public class QuestionPaperData
{
    public string Code { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Semester { get; set; } = string.Empty;
    public string ExamNo { get; set; } = string.Empty;
    public string Duration { get; set; } = string.Empty;
    public string Date { get; set; } = string.Empty;
    public string Time { get; set; } = string.Empty;
    public string MaxMarks { get; set; } = string.Empty;
    public string Note { get; set; } = string.Empty;
    public string Footer { get; set; } = string.Empty;

    /// <summary>
    /// List of units. Each entry is either a <see cref="QuestionPaperUnit"/> object or the literal string "OR".
    /// We model this as a list of objects; "OR" separators at the unit level are represented
    /// by a unit whose Name is "OR".
    /// </summary>
    public List<QuestionPaperUnit> Units { get; set; } = new();
}

public class QuestionPaperUnit
{
    /// <summary>
    /// Unit name, e.g. "Unit - I". When this is "OR", it represents an OR separator row.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Questions in this unit. Each entry is either a <see cref="QuestionPaperQuestion"/> or an "OR" marker.
    /// "OR" markers are represented by a question whose No is "OR".
    /// </summary>
    public List<QuestionPaperQuestion> Questions { get; set; } = new();
}

public class QuestionPaperQuestion
{
    /// <summary>
    /// Question number, e.g. "1", "2". When this is "OR", it represents an OR separator.
    /// </summary>
    public string No { get; set; } = string.Empty;

    public List<QuestionPaperPart> Parts { get; set; } = new();
}

public class QuestionPaperPart
{
    public string Sub { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
    public string Marks { get; set; } = string.Empty;
    public string Bt { get; set; } = string.Empty;
    public string Co { get; set; } = string.Empty;
}

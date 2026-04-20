using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using UglyToad.PdfPig;

namespace GraspIt.Infrastructure.Services;

/// <summary>
/// Represents a structured exam paper with header metadata, sections, and questions.
/// This model preserves the format of the original uploaded reference paper.
/// </summary>
public class ExamPaperStructure
{
    /// <summary>Raw text lines extracted from the header block (institution, title, date, etc.).</summary>
    public List<string> HeaderLines { get; set; } = new();

    /// <summary>Parsed top-level sections (Part A, Part B, Section 1, etc.).</summary>
    public List<ExamSection> Sections { get; set; } = new();

    /// <summary>Full raw text from the PDF, used as format reference for the LLM prompt.</summary>
    public string RawText { get; set; } = string.Empty;

    /// <summary>True if the reference paper actually contained a multi-column table structure.</summary>
    public bool HasTableStructure { get; set; }

    /// <summary>Column names detected or inferred from the reference paper header row.</summary>
    public List<string> TableColumns { get; set; } = new() { "Q No", "Question", "Marks", "BT", "CO" };
}

public class ExamSection
{
    /// <summary>Section heading, e.g. "PART A", "Section 1", or empty if questions are not grouped.</summary>
    public string Heading { get; set; } = string.Empty;

    /// <summary>Optional annotation text beneath a heading, e.g. "(Answer All Questions)".</summary>
    public string SubHeading { get; set; } = string.Empty;

    /// <summary>The questions belonging to this section.</summary>
    public List<ExamQuestion> Questions { get; set; } = new();
}

public class ExamQuestion
{
    public string QNo { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
    public string Marks { get; set; } = string.Empty;
    public string BT { get; set; } = string.Empty;
    public string CO { get; set; } = string.Empty;
    /// <summary>Sub-parts (a, b, c) if the question is compound.</summary>
    public List<ExamQuestion> SubParts { get; set; } = new();
}

public interface IDocumentExtractionService
{
    Task<string> ExtractExamTableAsync(IFormFile extractedFile);
    Task<ExamPaperStructure> ExtractStructuredAsync(IFormFile extractedFile);
}

public class DocumentExtractionService : IDocumentExtractionService
{
    // Patterns for detecting question boundaries
    private static readonly Regex QnumPattern = new(@"^\s*(Q\.?\s*\d+[\.\):]?|[0-9]+[\.\):])\s*", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex SubpartPattern = new(@"^\s*[a-z][\.\)]\s*", RegexOptions.Compiled);
    private static readonly Regex SectionPattern = new(@"^\s*(PART|SECTION|MODULE|UNIT)\s*[-–]?\s*[A-Z0-9]+\s*$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex MarksPattern = new(@"\((\d+)\s*[Mm]arks?\)|\[(\d+)\]|\b(\d+)\s*[Mm]$", RegexOptions.Compiled);
    private static readonly Regex BtPattern = new(@"\b(L[1-6]|K[1-6]|BTL\s*[1-6]|APPLY|REMEMBER|UNDERSTAND|ANALYZE|EVALUATE|CREATE)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex CoPattern = new(@"\b(CO\s*[1-9]|PO\s*[1-9])\b", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex TablePipePattern = new(@"\|", RegexOptions.Compiled);

    /// <summary>
    /// Legacy compatibility: returns a flat Markdown table for the LLM prompt context.
    /// This is still used by LocalOpenAIProvider to understand the reference paper's format.
    /// </summary>
    public async Task<string> ExtractExamTableAsync(IFormFile extractedFile)
    {
        var structure = await ExtractStructuredAsync(extractedFile);
        if (string.IsNullOrWhiteSpace(structure.RawText))
            return ReturnFallbackTemplate();

        return BuildMarkdownFromStructure(structure);
    }

    /// <summary>
    /// Full structured extraction: returns an <see cref="ExamPaperStructure"/> that the
    /// export pipeline uses to faithfully clone the original paper's layout.
    /// </summary>
    public async Task<ExamPaperStructure> ExtractStructuredAsync(IFormFile extractedFile)
    {
        if (extractedFile == null || extractedFile.Length == 0)
            return new ExamPaperStructure();

        var tempPath = Path.GetTempFileName();
        var ext = Path.GetExtension(extractedFile.FileName).ToLowerInvariant();

        using (var fs = new FileStream(tempPath, FileMode.Create))
            await extractedFile.CopyToAsync(fs);

        try
        {
            if (ext == ".pdf")
            {
                try
                {
                    var rawText = ExtractPdfText(tempPath);
                    if (!string.IsNullOrWhiteSpace(rawText))
                        return ParseIntoStructure(rawText);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[Warning] PDF extraction failed: {ex.Message}. Using fallback.");
                }
            }

            // Fallback for non-PDF or failed extraction
            Console.WriteLine("[Info] Returning fallback exam structure.");
            return BuildFallbackStructure();
        }
        finally
        {
            if (File.Exists(tempPath))
                File.Delete(tempPath);
        }
    }

    // ─────────────────────────────────────────────────────────────────
    // PRIVATE: PDF TEXT EXTRACTION
    // ─────────────────────────────────────────────────────────────────

    private static string ExtractPdfText(string filePath)
    {
        var sb = new StringBuilder();
        using (var document = PdfDocument.Open(filePath))
        {
            foreach (var page in document.GetPages())
            {
                sb.AppendLine(page.Text);
            }
        }
        return sb.ToString().Trim();
    }

    // ─────────────────────────────────────────────────────────────────
    // PRIVATE: STRUCTURAL PARSER
    // ─────────────────────────────────────────────────────────────────

    internal static ExamPaperStructure ParseIntoStructure(string rawText)
    {
        var structure = new ExamPaperStructure { RawText = rawText };

        // Detect if the source paper uses a pipe-delimited table
        var lines = rawText.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        int pipeLines = lines.Count(l => TablePipePattern.Matches(l).Count >= 2);
        structure.HasTableStructure = pipeLines > 3;

        // ── Step 1: Extract header (lines before first question/section) ──
        int contentStart = 0;
        for (int i = 0; i < Math.Min(lines.Length, 20); i++)
        {
            if (SectionPattern.IsMatch(lines[i]) || QnumPattern.IsMatch(lines[i]))
            {
                contentStart = i;
                break;
            }
            structure.HeaderLines.Add(lines[i]);
            contentStart = i + 1;
        }

        // ── Step 2: Parse columns if pipe-table ──
        if (structure.HasTableStructure)
        {
            structure.TableColumns = DetectTableColumns(lines, contentStart);
        }

        // ── Step 3: Parse sections and questions ──
        ExamSection currentSection = new ExamSection { Heading = string.Empty };
        structure.Sections.Add(currentSection);

        ExamQuestion? currentQuestion = null;
        var questionBuffer = new StringBuilder();

        for (int i = contentStart; i < lines.Length; i++)
        {
            var line = lines[i].Trim();
            if (string.IsNullOrWhiteSpace(line)) continue;

            // New section heading detected
            if (SectionPattern.IsMatch(line))
            {
                FinalizeCurrentQuestion(currentQuestion, questionBuffer, currentSection);
                currentQuestion = null;
                questionBuffer.Clear();

                currentSection = new ExamSection { Heading = line.Trim() };
                // Check if next line is a sub-heading
                if (i + 1 < lines.Length && lines[i + 1].Trim().StartsWith("("))
                {
                    currentSection.SubHeading = lines[i + 1].Trim();
                    i++;
                }
                structure.Sections.Add(currentSection);
                continue;
            }

            // Sub-part (a. b. c.)
            if (SubpartPattern.IsMatch(line) && currentQuestion != null)
            {
                var sub = ParseQuestionLine(line);
                if (!string.IsNullOrWhiteSpace(sub.Text))
                {
                    currentQuestion.SubParts.Add(sub);
                    continue;
                }
            }

            // New top-level question
            if (QnumPattern.IsMatch(line))
            {
                FinalizeCurrentQuestion(currentQuestion, questionBuffer, currentSection);
                questionBuffer.Clear();
                questionBuffer.AppendLine(line);
                currentQuestion = ParseQuestionLine(line);
            }
            else
            {
                // Continuation of current question text
                if (currentQuestion != null)
                {
                    questionBuffer.Append(" " + line);
                    // Absorb any trailing marks/BT/CO from continuation lines
                    AbsorbMetadata(line, currentQuestion);
                }
            }
        }

        // Finalize last question
        FinalizeCurrentQuestion(currentQuestion, questionBuffer, currentSection);

        // Remove empty default section if nothing was found there
        if (structure.Sections.Count > 1 && !structure.Sections[0].Questions.Any())
            structure.Sections.RemoveAt(0);

        return structure;
    }

    private static List<string> DetectTableColumns(string[] lines, int startIdx)
    {
        // Find what looks like a header row (contains pipe characters and words like Q, Question, Marks)
        for (int i = startIdx; i < Math.Min(startIdx + 10, lines.Length); i++)
        {
            var line = lines[i];
            if (line.Contains('|') && (line.Contains("Q") || line.Contains("Marks") || line.Contains("Mark")))
            {
                return line.Split('|')
                           .Select(c => c.Trim())
                           .Where(c => !string.IsNullOrWhiteSpace(c) && !Regex.IsMatch(c, @"^-+$"))
                           .ToList();
            }
        }
        return new List<string> { "Q No", "Question", "Marks", "BT", "CO" };
    }

    private static ExamQuestion ParseQuestionLine(string line)
    {
        var q = new ExamQuestion();

        // Extract Q number
        var qMatch = QnumPattern.Match(line);
        if (qMatch.Success)
        {
            q.QNo = qMatch.Value.Trim();
            line = line.Substring(qMatch.Length).Trim();
        }

        // Extract marks
        var marksMatch = MarksPattern.Match(line);
        if (marksMatch.Success)
        {
            q.Marks = (marksMatch.Groups[1].Value.IsNullOrEmpty()
                ? marksMatch.Groups[2].Value.IsNullOrEmpty()
                    ? marksMatch.Groups[3].Value
                    : marksMatch.Groups[2].Value
                : marksMatch.Groups[1].Value).Trim();
            line = line.Replace(marksMatch.Value, "").Trim();
        }

        // Extract BT level
        var btMatch = BtPattern.Match(line);
        if (btMatch.Success)
        {
            q.BT = btMatch.Value.Trim().ToUpper();
            line = line.Replace(btMatch.Value, "").Trim();
        }

        // Extract CO
        var coMatch = CoPattern.Match(line);
        if (coMatch.Success)
        {
            q.CO = coMatch.Value.Trim().ToUpper();
            line = line.Replace(coMatch.Value, "").Trim();
        }

        q.Text = line.Trim().Trim(',', ';');
        return q;
    }

    private static void AbsorbMetadata(string line, ExamQuestion q)
    {
        if (string.IsNullOrWhiteSpace(q.Marks))
        {
            var m = MarksPattern.Match(line);
            if (m.Success) q.Marks = (m.Groups[1].Value + m.Groups[2].Value + m.Groups[3].Value).Trim();
        }
        if (string.IsNullOrWhiteSpace(q.BT))
        {
            var b = BtPattern.Match(line);
            if (b.Success) q.BT = b.Value.Trim().ToUpper();
        }
        if (string.IsNullOrWhiteSpace(q.CO))
        {
            var c = CoPattern.Match(line);
            if (c.Success) q.CO = c.Value.Trim().ToUpper();
        }
    }

    private static void FinalizeCurrentQuestion(ExamQuestion? q, StringBuilder buffer, ExamSection section)
    {
        if (q == null) return;
        if (string.IsNullOrWhiteSpace(q.Text) && buffer.Length > 0)
        {
            var full = buffer.ToString().Trim();
            var parsed = ParseQuestionLine(full);
            q.Text = parsed.Text;
            if (string.IsNullOrWhiteSpace(q.Marks)) q.Marks = parsed.Marks;
            if (string.IsNullOrWhiteSpace(q.BT)) q.BT = parsed.BT;
            if (string.IsNullOrWhiteSpace(q.CO)) q.CO = parsed.CO;
        }
        if (!string.IsNullOrWhiteSpace(q.Text) || !string.IsNullOrWhiteSpace(q.QNo))
            section.Questions.Add(q);
    }

    // ─────────────────────────────────────────────────────────────────
    // PRIVATE: MARKDOWN BUILDER (for LLM prompt)
    // ─────────────────────────────────────────────────────────────────

    private static string BuildMarkdownFromStructure(ExamPaperStructure s)
    {
        var sb = new StringBuilder();

        // Header block
        if (s.HeaderLines.Any())
            sb.AppendLine(string.Join("\n", s.HeaderLines));

        sb.AppendLine();
        sb.AppendLine("| Q No | Question | Marks | BT | CO |");
        sb.AppendLine("|---|---|---|---|---|");

        foreach (var section in s.Sections)
        {
            if (!string.IsNullOrWhiteSpace(section.Heading))
                sb.AppendLine($"| **{section.Heading}** | {section.SubHeading} | | | |");

            foreach (var q in section.Questions)
            {
                sb.AppendLine($"| {q.QNo} | {q.Text} | {q.Marks} | {q.BT} | {q.CO} |");
                foreach (var sp in q.SubParts)
                    sb.AppendLine($"| &nbsp;&nbsp;{sp.QNo} | {sp.Text} | {sp.Marks} | {sp.BT} | {sp.CO} |");
            }
        }

        return sb.ToString();
    }

    // ─────────────────────────────────────────────────────────────────
    // PRIVATE: FALLBACK
    // ─────────────────────────────────────────────────────────────────

    private static ExamPaperStructure BuildFallbackStructure()
    {
        return new ExamPaperStructure
        {
            RawText = "Fallback template",
            HasTableStructure = true,
            HeaderLines = new List<string> { "AI Generated Examination" },
            TableColumns = new List<string> { "Q No", "Question", "Marks", "BT", "CO" },
            Sections = new List<ExamSection>
            {
                new ExamSection
                {
                    Heading = "PART A",
                    SubHeading = "(Answer All Questions)",
                    Questions = new List<ExamQuestion>
                    {
                        new ExamQuestion { QNo = "Q1.", Text = "Short answer question placeholder", Marks = "2", BT = "L1", CO = "CO1" }
                    }
                }
            }
        };
    }

    private static string ReturnFallbackTemplate() =>
        "| Q No | Question | Marks | BT | CO |\n|---|---|---|---|---|\n| Q1. | What are the core concepts? | 10 | L1 | CO1 |";
}

// Extension helper to avoid null checks on string
internal static class StringExtensions
{
    public static bool IsNullOrEmpty(this string? s) => string.IsNullOrEmpty(s);
}

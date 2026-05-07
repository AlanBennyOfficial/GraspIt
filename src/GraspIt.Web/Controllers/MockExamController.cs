using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;
using GraspIt.Core.Entities;
using GraspIt.Infrastructure.Services;
using GraspIt.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace GraspIt.Web.Controllers;

public class MockExamController : Controller
{
    private readonly IDocumentExtractionService _ocrService;
    private readonly IRagRetrievalService _ragService;
    private readonly ILLMProvider _llmProvider;
    private readonly IExamRendererService _rendererService;
    private readonly GraspItDbContext _context;

    public MockExamController(
        IDocumentExtractionService ocrService,
        IRagRetrievalService ragService,
        ILLMProvider llmProvider,
        IExamRendererService rendererService,
        GraspItDbContext context)
    {
        _ocrService = ocrService;
        _ragService = ragService;
        _llmProvider = llmProvider;
        _rendererService = rendererService;
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var docs = await _context.Documents
            .OrderByDescending(d => d.UploadedAt)
            .Select(d => new { d.Id, d.Title, d.DocumentType })
            .ToListAsync();
        ViewBag.Documents = docs;
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> GenerateExam(IFormFile referenceImage, string instructions)
    {
        if (referenceImage == null || referenceImage.Length == 0)
            return Json(new { success = false, message = "A reference paper (image/PDF) must be provided." });

        if (string.IsNullOrWhiteSpace(instructions))
            return Json(new { success = false, message = "Generation instructions must be provided." });

        try
        {
            // ── Phase 1: Full Structural Extraction ──────────────────────────
            // Extract both the rich structure (for rendering) and the markdown table
            // (for the LLM prompt). Both come from the same PDF parse pass.
            var paperStructure = await _ocrService.ExtractStructuredAsync(referenceImage);
            string ocrMarkdownTable = await _ocrService.ExtractExamTableAsync(referenceImage);

            if (string.IsNullOrWhiteSpace(ocrMarkdownTable))
                return Json(new { success = false, message = "Could not extract text from the reference paper." });

            // ── Phase 2: RAG Context Retrieval ────────────────────────────────
            string syllabusRagContext = await _ragService.GetRelevantSyllabusContextAsync(instructions, topK: 5);

            // ── Phase 3: LLM Question Generation (Structured Format) ─────────
            // Try the new structured format first (qn_format.json compatible)
            var structuredPaper = await _llmProvider.GenerateStructuredExamAsync(
                ocrMarkdownTable, syllabusRagContext, instructions);

            if (structuredPaper != null && structuredPaper.Units.Count > 0)
            {
                // ── Success: Return structured JSON for high-fidelity exports ──
                var markdown = BuildStructuredMarkdownFromPaper(structuredPaper);
                var paperJson = SerializePaperToJson(structuredPaper);

                return Json(new
                {
                    success = true,
                    markdown,
                    paperJson,       // Structured data for export buttons
                    isStructured = true
                });
            }

            // ── Fallback: Use legacy flat QuestionRow generation ─────────────
            var generatedQuestions = await _llmProvider.GenerateExamQuestionsAsync(
                ocrMarkdownTable, syllabusRagContext, instructions);

            if (generatedQuestions == null || generatedQuestions.Count == 0)
                return Json(new { success = false, message = "The AI could not generate questions. Please ensure your LLM is running." });

            // ── Phase 4: Merge generated questions into the paper structure ───
            MergeQuestionsIntoStructure(paperStructure, generatedQuestions, instructions);

            // ── Phase 5: Build Markdown for on-screen preview ─────────────────
            var fallbackMarkdown = BuildStructuredMarkdown(paperStructure, instructions);

            return Json(new { success = true, markdown = fallbackMarkdown, isStructured = false });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = $"Error: {ex.Message}" });
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // PRIVATE: Build Markdown from QuestionPaperData (structured format)
    // ─────────────────────────────────────────────────────────────────────────

    private static string BuildStructuredMarkdownFromPaper(QuestionPaperData paper)
    {
        var sb = new StringBuilder();

        sb.AppendLine($"# {paper.Code}{paper.Title}");
        sb.AppendLine();
        sb.AppendLine($"**{paper.Semester} Mid Semester Examination {paper.ExamNo}**");
        sb.AppendLine();
        sb.AppendLine($"**Duration:** {paper.Duration} | **Date:** {paper.Date} {paper.Time} | **Max Marks:** {paper.MaxMarks}");
        sb.AppendLine();

        if (!string.IsNullOrWhiteSpace(paper.Note))
        {
            sb.AppendLine($"> *Note: {paper.Note}*");
            sb.AppendLine();
        }

        foreach (var unit in paper.Units)
        {
            if (unit.Name == "OR")
            {
                sb.AppendLine();
                sb.AppendLine("---");
                sb.AppendLine("### OR");
                sb.AppendLine("---");
                sb.AppendLine();
                continue;
            }

            sb.AppendLine($"## {unit.Name}");
            sb.AppendLine();
            sb.AppendLine("| Q No | Sub | Question | Marks | BT | CO |");
            sb.AppendLine("|------|-----|----------|-------|----|----|");

            foreach (var q in unit.Questions)
            {
                if (q.No == "OR")
                {
                    sb.AppendLine("| | | **OR** | | | |");
                    continue;
                }

                foreach (var p in q.Parts)
                {
                    sb.AppendLine($"| {q.No} | {p.Sub} | {p.Text} | {p.Marks} | {p.Bt} | {p.Co} |");
                }
            }
            sb.AppendLine();
        }

        if (!string.IsNullOrWhiteSpace(paper.Footer))
            sb.AppendLine($"*{paper.Footer}*");

        return sb.ToString();
    }

      /// <summary>
    /// Serializes QuestionPaperData to the raw JSON format compatible with qn_format.json,
    /// including "OR" string literals in arrays.
      /// </summary>
    private static string SerializePaperToJson(QuestionPaperData paper)
    {
        var units = new List<object>();
        foreach (var unit in paper.Units)
        {
            if (unit.Name == "OR")
            {
                units.Add("OR");
                continue;
            }

            var questions = new List<object>();
            foreach (var q in unit.Questions)
            {
                if (q.No == "OR")
                {
                    questions.Add("OR");
                    continue;
                }

                questions.Add(new
                {
                    no = q.No,
                    parts = q.Parts.Select(p => new
                    {
                        sub = p.Sub,
                        text = p.Text,
                        marks = p.Marks,
                        bt = p.Bt,
                        co = p.Co
                    }).ToArray()
                });
            }

            units.Add(new
            {
                name = unit.Name,
                questions = questions
            });
        }

        var rawPaper = new
        {
            code = paper.Code,
            title = paper.Title,
            semester = paper.Semester,
            examNo = paper.ExamNo,
            duration = paper.Duration,
            date = paper.Date,
            time = paper.Time,
            maxMarks = paper.MaxMarks,
            note = paper.Note,
            footer = paper.Footer,
            units = units
        };

        return JsonSerializer.Serialize(rawPaper, new JsonSerializerOptions
        {
            WriteIndented = false,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        });
    }

    // ─────────────────────────────────────────────────────────────────────────
    // PRIVATE: Merge LLM-generated QuestionRow list into the parsed structure
    // ─────────────────────────────────────────────────────────────────────────

    private static void MergeQuestionsIntoStructure(ExamPaperStructure structure, List<QuestionRow> rows, string instructions)
    {
        // If there are no sections yet, create one
        if (!structure.Sections.Any())
            structure.Sections.Add(new ExamSection { Heading = string.Empty });

        // Map the flat QuestionRow list back into sections based on numbering or section position
        // Strategy: distribute rows proportionally across sections that had questions in original
        var sectionsWithQ = structure.Sections.Where(s => s.Questions.Count > 0).ToList();

        if (!sectionsWithQ.Any())
        {
            // No sections with questions – put everything in the first section
            structure.Sections[0].Questions = rows.Select(ToExamQuestion).ToList();
            return;
        }

        // Distribute proportionally: keep same section names, replace question content
        int totalOriginal = sectionsWithQ.Sum(s => s.Questions.Count);
        int rowIdx = 0;

        foreach (var section in sectionsWithQ)
        {
            int count = (int)Math.Round((double)section.Questions.Count / totalOriginal * rows.Count);
            count = Math.Max(1, Math.Min(count, rows.Count - rowIdx));

            section.Questions = rows.Skip(rowIdx).Take(count).Select(ToExamQuestion).ToList();
            rowIdx += count;
            if (rowIdx >= rows.Count) break;
        }

        // If any rows remain, append to last section
        if (rowIdx < rows.Count)
            sectionsWithQ.Last().Questions.AddRange(rows.Skip(rowIdx).Select(ToExamQuestion));
    }

    private static ExamQuestion ToExamQuestion(QuestionRow r) => new()
    {
        QNo   = r.QNo   ?? "",
        Text  = r.Question ?? "",
        Marks = r.Marks ?? "",
        BT    = r.BT    ?? "",
        CO    = r.CO    ?? ""
    };

    // ─────────────────────────────────────────────────────────────────────────
    // PRIVATE: Build Markdown from the populated structure (legacy fallback)
    // ─────────────────────────────────────────────────────────────────────────

    private static string BuildStructuredMarkdown(ExamPaperStructure structure, string instructions)
    {
        var sb = new StringBuilder();

        // Title block
        if (structure.HeaderLines.Any())
        {
            sb.AppendLine($"# {structure.HeaderLines.First()}");
            foreach (var hl in structure.HeaderLines.Skip(1))
                sb.AppendLine($"**{hl}**");
        }
        else
        {
            sb.AppendLine("# AI Generated Mock Examination");
        }

        sb.AppendLine();
        sb.AppendLine($"**Instructions**: {instructions}");
        sb.AppendLine();

        foreach (var section in structure.Sections)
        {
            if (!section.Questions.Any()) continue;

            // Section heading row
            if (!string.IsNullOrWhiteSpace(section.Heading))
            {
                sb.AppendLine($"\n| **{section.Heading}** | {section.SubHeading} | | | |");
                sb.AppendLine("|---|---|---|---|---|");
            }
            else
            {
                sb.AppendLine("| Q No | Question | Marks | BT | CO |");
                sb.AppendLine("|---|---|---|---|---|");
            }

            foreach (var q in section.Questions)
            {
                sb.AppendLine($"| {q.QNo} | {q.Text} | {q.Marks} | {q.BT} | {q.CO} |");
                foreach (var sp in q.SubParts)
                    sb.AppendLine($"| &nbsp;&nbsp;{sp.QNo} | &nbsp;&nbsp;{sp.Text} | {sp.Marks} | {sp.BT} | {sp.CO} |");
            }
        }

        return sb.ToString();
    }
}

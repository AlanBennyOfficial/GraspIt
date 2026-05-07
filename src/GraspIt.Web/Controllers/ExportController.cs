using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using GraspIt.Core.Entities;
using GraspIt.Core.Interfaces;
using GraspIt.Infrastructure.Services;

namespace GraspIt.Web.Controllers;

public class ExportController : Controller
{
    private readonly IExportService _exportService;
    private readonly IQuestionPaperGeneratorService _qpGenerator;

    public ExportController(IExportService exportService, IQuestionPaperGeneratorService qpGenerator)
    {
        _exportService = exportService;
        _qpGenerator = qpGenerator;
    }

    // ─────────────────────────────────────────────────────────────────
    // Legacy exports (from markdown content, used by Chat/Documents)
    // ─────────────────────────────────────────────────────────────────

    [HttpPost]
    public async Task<IActionResult> DownloadDocx([FromForm] string content)
    {
        var bytes = await _exportService.ToDocxAsync(content);
        return File(bytes, "application/vnd.openxmlformats-officedocument.wordprocessingml.document", "GraspIt_Export.docx");
    }

    [HttpPost]
    public async Task<IActionResult> DownloadPdf([FromForm] string content)
    {
        var bytes = await _exportService.ToPdfAsync(content);
        return File(bytes, "application/pdf", "GraspIt_Export.pdf");
    }

    [HttpPost]
    public IActionResult DownloadMarkdown([FromForm] string content)
    {
        var bytes = _exportService.ToMarkdown(content);
        return File(bytes, "text/markdown", "GraspIt_Export.md");
    }

    [HttpPost]
    public IActionResult DownloadLatex([FromForm] string content)
    {
        var bytes = _exportService.ToLatex(content);
        return File(bytes, "application/x-latex", "GraspIt_Export.tex");
    }

    // ─────────────────────────────────────────────────────────────────
    // Question Paper exports (high-fidelity, via Node.js generator)
    // Accepts raw JSON body (Content-Type: application/json)
    // ─────────────────────────────────────────────────────────────────

    [HttpPost]
    public async Task<IActionResult> DownloadExamDocx()
    {
        var paperData = await ReadPaperDataFromBody();
        if (paperData == null)
            return BadRequest("Invalid question paper data.");

        try
        {
            var bytes = await _qpGenerator.GenerateDocxAsync(paperData);
            var filename = SanitizeFilename(paperData.Code, "docx");
            return File(bytes, "application/vnd.openxmlformats-officedocument.wordprocessingml.document", filename);
        }
        catch (Exception ex)
        {
            return BadRequest($"DOCX generation failed: {ex.Message}");
        }
    }

    [HttpPost]
    public async Task<IActionResult> DownloadExamPdf()
    {
        var paperData = await ReadPaperDataFromBody();
        if (paperData == null)
            return BadRequest("Invalid question paper data.");

        try
        {
            var bytes = await _qpGenerator.GeneratePdfAsync(paperData);
            var filename = SanitizeFilename(paperData.Code, "pdf");
            return File(bytes, "application/pdf", filename);
        }
        catch (Exception ex)
        {
            return BadRequest($"PDF generation failed: {ex.Message}");
        }
    }

    [HttpPost]
    public async Task<IActionResult> DownloadExamMarkdown()
    {
        var paperData = await ReadPaperDataFromBody();
        if (paperData == null)
            return BadRequest("Invalid question paper data.");

        var bytes = _qpGenerator.GenerateMarkdown(paperData);
        var filename = SanitizeFilename(paperData.Code, "md");
        return File(bytes, "text/markdown", filename);
    }

    [HttpPost]
    public async Task<IActionResult> DownloadExamLatex()
    {
        var paperData = await ReadPaperDataFromBody();
        if (paperData == null)
            return BadRequest("Invalid question paper data.");

        var bytes = _qpGenerator.GenerateLatex(paperData);
        var filename = SanitizeFilename(paperData.Code, "tex");
        return File(bytes, "application/x-latex", filename);
    }

    // ─────────────────────────────────────────────────────────────────
    // PRIVATE: Read and parse the JSON body
    // ─────────────────────────────────────────────────────────────────

      /// <summary>
    /// Reads the raw JSON from the request body and parses it into QuestionPaperData,
    /// handling mixed-type arrays (units/questions can contain "OR" strings or objects).
      /// </summary>
    private async Task<QuestionPaperData?> ReadPaperDataFromBody()
    {
        try
        {
            using var reader = new StreamReader(Request.Body);
            var json = await reader.ReadToEndAsync();

            if (string.IsNullOrWhiteSpace(json))
                return null;

            return DeserializePaperData(json);
        }
        catch
        {
            return null;
        }
    }

      /// <summary>
    /// Deserializes the structured question paper JSON, handling mixed-type arrays
    /// (units and questions can contain "OR" strings or objects).
      /// </summary>
    private static QuestionPaperData? DeserializePaperData(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var paper = new QuestionPaperData
            {
                Code = GetStr(root, "code"),
                Title = GetStr(root, "title"),
                Semester = GetStr(root, "semester"),
                ExamNo = GetStr(root, "examNo"),
                Duration = GetStr(root, "duration"),
                Date = GetStr(root, "date"),
                Time = GetStr(root, "time"),
                MaxMarks = GetStr(root, "maxMarks"),
                Note = GetStr(root, "note"),
                Footer = GetStr(root, "footer")
            };

            if (root.TryGetProperty("units", out var unitsArr) && unitsArr.ValueKind == JsonValueKind.Array)
            {
                foreach (var u in unitsArr.EnumerateArray())
                {
                    if (u.ValueKind == JsonValueKind.String && u.GetString() == "OR")
                    {
                        paper.Units.Add(new QuestionPaperUnit { Name = "OR" });
                        continue;
                    }
                    if (u.ValueKind != JsonValueKind.Object) continue;

                    var unit = new QuestionPaperUnit { Name = GetStr(u, "name") };

                    if (u.TryGetProperty("questions", out var qArr) && qArr.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var q in qArr.EnumerateArray())
                        {
                            if (q.ValueKind == JsonValueKind.String && q.GetString() == "OR")
                            {
                                unit.Questions.Add(new QuestionPaperQuestion { No = "OR" });
                                continue;
                            }
                            if (q.ValueKind != JsonValueKind.Object) continue;

                            var question = new QuestionPaperQuestion { No = GetStr(q, "no") };

                            if (q.TryGetProperty("parts", out var pArr) && pArr.ValueKind == JsonValueKind.Array)
                            {
                                foreach (var p in pArr.EnumerateArray())
                                {
                                    if (p.ValueKind != JsonValueKind.Object) continue;
                                    question.Parts.Add(new QuestionPaperPart
                                    {
                                        Sub = GetStr(p, "sub"),
                                        Text = GetStr(p, "text"),
                                        Marks = GetStr(p, "marks"),
                                        Bt = GetStr(p, "bt"),
                                        Co = GetStr(p, "co")
                                    });
                                }
                            }
                            unit.Questions.Add(question);
                        }
                    }
                    paper.Units.Add(unit);
                }
            }

            return paper;
        }
        catch
        {
            return null;
        }
    }

    private static string GetStr(JsonElement elem, string prop) =>
        elem.TryGetProperty(prop, out var v) && v.ValueKind == JsonValueKind.String
            ? v.GetString() ?? "" : "";

    private static string SanitizeFilename(string? code, string extension)
    {
        var safe = (code ?? "Export").Trim(' ', '-').Replace(" ", "_");
        // Remove invalid filename characters
        foreach (var c in Path.GetInvalidFileNameChars())
            safe = safe.Replace(c, '_');
        if (string.IsNullOrWhiteSpace(safe)) safe = "QuestionPaper";
        return $"QuestionPaper_{safe}.{extension}";
    }
}

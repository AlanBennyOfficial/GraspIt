using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using GraspIt.Core.Entities;
using Microsoft.Extensions.Logging;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace GraspIt.Infrastructure.Services;

  
/// Generates high-fidelity exam paper documents by invoking the Node.js
/// LLM_qn_paper_generator.js script. All formats (PDF, etc.) are derived
/// from the DOCX output to ensure visual consistency.
  
public interface IQuestionPaperGeneratorService
{
      /// <summary>
    /// Generates a DOCX file using the Node.js generator script.
      /// </summary>
    Task<byte[]> GenerateDocxAsync(QuestionPaperData paperData);

      /// <summary>
    /// Generates a DOCX and converts it to PDF.
    /// Uses LibreOffice CLI if available, otherwise falls back to QuestPDF.
      /// </summary>
    Task<byte[]> GeneratePdfAsync(QuestionPaperData paperData);

      /// <summary>
    /// Generates a Markdown representation of the question paper.
      /// </summary>
    byte[] GenerateMarkdown(QuestionPaperData paperData);

      /// <summary>
    /// Generates a LaTeX representation of the question paper.
      /// </summary>
    byte[] GenerateLatex(QuestionPaperData paperData);
}

public class QuestionPaperGeneratorService : IQuestionPaperGeneratorService
{
    private readonly string _toolsDir;
    private readonly string _scriptPath;
    private readonly string _logoPath;
    private readonly Microsoft.Extensions.Logging.ILogger<QuestionPaperGeneratorService> _logger;

    public QuestionPaperGeneratorService(
        IWebHostEnvironmentAccessor envAccessor,
        Microsoft.Extensions.Logging.ILogger<QuestionPaperGeneratorService> logger)
    {
        _toolsDir = envAccessor.ToolsDirectory;
        _scriptPath = Path.Combine(_toolsDir, "LLM_qn_paper_generator.js");
        _logoPath = Path.Combine(_toolsDir, "NITTE_Deemed_Univerity_logo.png");
        _logger = logger;
    }

    // ─────────────────────────────────────────────────────────────────
    // DOCX — Calls Node.js script for pixel-perfect output
    // ─────────────────────────────────────────────────────────────────

    public async Task<byte[]> GenerateDocxAsync(QuestionPaperData paperData)
    {
        // Serialize the paper data to a JSON array (script expects an array)
        var jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        // The Node.js script expects a JSON structure matching qn_format.json.
        // Convert our C# model to the raw JSON with "OR" string entries.
        var rawJson = SerializeToGeneratorFormat(paperData, jsonOptions);

        // Create temporary files for input/output
        var tempDir = Path.Combine(Path.GetTempPath(), "GraspIt_QP_" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(tempDir);
        var inputJsonPath = Path.Combine(tempDir, "input.json");
        var outputDocxPath = Path.Combine(tempDir, "output.docx");

        try
        {
            await File.WriteAllTextAsync(inputJsonPath, rawJson, new UTF8Encoding(false));
            _logger.LogInformation("DOCX Gen: Wrote JSON ({Len} chars) to {Path}", rawJson.Length, inputJsonPath);
            _logger.LogDebug("DOCX Gen: Script={Script}, Logo exists={Logo}", _scriptPath, File.Exists(_logoPath));

            // Execute the Node.js script
            var psi = new ProcessStartInfo
            {
                FileName = "node",
                Arguments = $"\"{_scriptPath}\" \"{inputJsonPath}\" \"{outputDocxPath}\"",
                WorkingDirectory = _toolsDir,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = new Process { StartInfo = psi };
            var stdout = new StringBuilder();
            var stderr = new StringBuilder();

            process.OutputDataReceived += (_, e) => { if (e.Data != null) stdout.AppendLine(e.Data); };
            process.ErrorDataReceived += (_, e) => { if (e.Data != null) stderr.AppendLine(e.Data); };

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            // Wait up to 30 seconds for generation
            bool exited = await Task.Run(() => process.WaitForExit(30_000));

            if (!exited)
            {
                try { process.Kill(entireProcessTree: true); } catch { }
                _logger.LogError("DOCX Gen: Node.js process timed out after 30s");
                throw new TimeoutException("Question paper generation timed out (30s).");
            }

            _logger.LogInformation("DOCX Gen: Exit={Code} stdout={Out} stderr={Err}",
                process.ExitCode, stdout.ToString().Trim(), stderr.ToString().Trim());

            if (process.ExitCode != 0)
            {
                throw new InvalidOperationException(
                    $"Node.js generator failed (exit code {process.ExitCode}): {stderr}");
            }

            if (!File.Exists(outputDocxPath))
            {
                throw new FileNotFoundException(
                    $"Generated DOCX not found. stdout: {stdout} stderr: {stderr}");
            }

            var bytes = await File.ReadAllBytesAsync(outputDocxPath);
            _logger.LogInformation("DOCX Gen: Success! File size = {Size} bytes", bytes.Length);
            return bytes;
        }
        finally
        {
            // Clean up temp files
            try { Directory.Delete(tempDir, recursive: true); } catch { }
        }
    }

    // ─────────────────────────────────────────────────────────────────
    // PDF — DOCX → PDF conversion
    // ─────────────────────────────────────────────────────────────────

    public async Task<byte[]> GeneratePdfAsync(QuestionPaperData paperData)
    {
        string? highFidelityError = null;

        // Strategy: Try high-fidelity conversion first (DOCX -> PDF via LibreOffice)
        try
        {
            var docxBytes = await GenerateDocxAsync(paperData);
            var pdfBytes = await TryLibreOfficeConversion(docxBytes);
            if (pdfBytes != null)
                return pdfBytes;
            
            highFidelityError = "LibreOffice (soffice) not found or conversion failed.";
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "High-fidelity PDF generation failed.");
            highFidelityError = ex.Message;
        }

        // Fallback: Use native QuestPDF renderer.
        try
        {
            return RenderPdfFromStructure(paperData);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Native PDF renderer (QuestPDF) failed.");
            var errorMsg = $"PDF generation failed.\n" +
                           $"- High-fidelity path: {highFidelityError ?? "Unknown error"}\n" +
                           $"- Native path: {ex.Message}";
            throw new InvalidOperationException(errorMsg, ex);
        }
    }

    private async Task<byte[]?> TryLibreOfficeConversion(byte[] docxBytes)
    {
        try
        {
            var tempDir = Path.Combine(Path.GetTempPath(), "GraspIt_PDF_" + Guid.NewGuid().ToString("N")[..8]);
            Directory.CreateDirectory(tempDir);
            var inputPath = Path.Combine(tempDir, "input.docx");
            var expectedPdfPath = Path.Combine(tempDir, "input.pdf");

            await File.WriteAllBytesAsync(inputPath, docxBytes);

            // Try to find LibreOffice
            string? sofficePath = FindSoffice();
            if (sofficePath == null) return null;

            var psi = new ProcessStartInfo
            {
                FileName = sofficePath,
                Arguments = $"--headless --convert-to pdf --outdir \"{tempDir}\" \"{inputPath}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = new Process { StartInfo = psi };
            process.Start();
            bool exited = await Task.Run(() => process.WaitForExit(30_000));

            if (!exited || !File.Exists(expectedPdfPath))
            {
                try { Directory.Delete(tempDir, true); } catch { }
                return null;
            }

            var result = await File.ReadAllBytesAsync(expectedPdfPath);
            try { Directory.Delete(tempDir, true); } catch { }
            return result;
        }
        catch
        {
            return null;
        }
    }

    private static string? FindSoffice()
    {
        // Common LibreOffice paths on Windows
        var candidates = new[]
        {
            @"C:\Program Files\LibreOffice\program\soffice.exe",
            @"C:\Program Files (x86)\LibreOffice\program\soffice.exe",
            Environment.GetEnvironmentVariable("LIBREOFFICE_PATH") ?? ""
        };

        foreach (var path in candidates)
        {
            if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
                return path;
        }

        // Try PATH
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "where",
                Arguments = "soffice",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using var proc = Process.Start(psi);
            if (proc != null)
            {
                var output = proc.StandardOutput.ReadToEnd().Trim();
                proc.WaitForExit(5000);
                if (!string.IsNullOrWhiteSpace(output) && File.Exists(output.Split('\n')[0].Trim()))
                    return output.Split('\n')[0].Trim();
            }
        }
        catch { }

        return null;
    }

      /// <summary>
    /// QuestPDF fallback renderer that matches the NITTE question paper format as defined in LLM_qn_paper_generator.js.
      /// </summary>
    private byte[] RenderPdfFromStructure(QuestionPaperData paper)
    {
        QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;

        // Load logo if available
        byte[]? logoBytes = null;
        try 
        { 
            if (File.Exists(_logoPath)) logoBytes = File.ReadAllBytes(_logoPath); 
        } 
        catch { }

        var doc = QuestPDF.Fluent.Document.Create(container =>
        {
            container.Page(page =>
            {
                // A4 dimensions in points (72 points per inch)
                page.Size(QuestPDF.Helpers.PageSizes.A4);
                page.Margin(0.5f, QuestPDF.Infrastructure.Unit.Inch);
                page.PageColor(QuestPDF.Helpers.Colors.White);
                
                // Use a standard font stack that's likely to be present
                page.DefaultTextStyle(x => x.FontSize(9).FontColor(QuestPDF.Helpers.Colors.Black));

                page.Content().Column(mainCol =>
                {
                    // ── Header Table (Grid Layout mirroring Node.js script) ──
                    // Node conversion (DXA to Points): COL_1 (110pt), COL_2 (64.4pt), COL_3 (174.5pt), COL_4 (174.5pt)
                    mainCol.Item().Table(table =>
                    {
                        table.ColumnsDefinition(cols =>
                        {
                            cols.ConstantColumn(110); // COL_1: Logo
                            cols.ConstantColumn(65);  // COL_2: Spacer / Grid start
                            cols.RelativeColumn();    // COL_3: Middle
                            cols.RelativeColumn();    // COL_4: Right
                        });

                        // Row 1: Logo (RowSpan 4) | USN Row (ColSpan 3)
                        table.Cell().RowSpan(4).Border(0.5f).BorderColor(QuestPDF.Helpers.Colors.Black)
                            .AlignCenter().AlignMiddle().Padding(4)
                            .Element(c =>
                            {
                                if (logoBytes != null) c.Image(logoBytes).FitWidth();
                                else c.Text("NITTE").FontSize(12).Bold();
                            });

                        table.Cell().ColumnSpan(3).Border(0.5f).BorderColor(QuestPDF.Helpers.Colors.Black)
                            .Padding(4).Table(usnTable => {
                                usnTable.ColumnsDefinition(c => {
                                    c.RelativeColumn(); // "USN" label
                                    for(int i=0; i<10; i++) c.ConstantColumn(12); // 10 boxes
                                });
                                usnTable.Cell().AlignRight().PaddingRight(4).Text("USN").FontSize(8.5f);
                                for(int i=0; i<10; i++) usnTable.Cell().Border(0.5f).Height(12);
                            });

                        // Row 2: Institute Name (ColSpan 3)
                        table.Cell().ColumnSpan(3).Border(0.5f).BorderColor(QuestPDF.Helpers.Colors.Black)
                            .AlignCenter().AlignMiddle().Padding(4)
                            .Text("Nitte Institute of Professional Education").FontSize(11).Bold();

                        // Row 3: Exam Info (ColSpan 3)
                        table.Cell().ColumnSpan(3).Border(0.5f).BorderColor(QuestPDF.Helpers.Colors.Black)
                            .AlignCenter().AlignMiddle().Padding(4)
                            .Text($"{paper.Semester} Mid Semester Examination {paper.ExamNo}").FontSize(9.5f).Bold();

                        // Row 4: Subject Code + Name (ColSpan 3)
                        table.Cell().ColumnSpan(3).Border(0.5f).BorderColor(QuestPDF.Helpers.Colors.Black)
                            .AlignCenter().AlignMiddle().Padding(4)
                            .Text($"{paper.Code}{paper.Title}").FontSize(9.5f).Bold();

                        // Row 5: Composite Info Row (Precise column spans)
                        table.Cell().ColumnSpan(2).Border(0.5f).BorderColor(QuestPDF.Helpers.Colors.Black)
                            .Padding(4).Text(t => {
                                t.Span("Duration: ").Bold().FontSize(8.5f);
                                t.Span(paper.Duration).FontSize(8.5f);
                            });

                        table.Cell().Border(0.5f).BorderColor(QuestPDF.Helpers.Colors.Black)
                            .AlignCenter().Padding(4).Text(t => {
                                t.Span("Date: ").Bold().FontSize(8.5f);
                                t.Span(paper.Date + (string.IsNullOrWhiteSpace(paper.Time) ? "" : " " + paper.Time)).FontSize(8.5f);
                            });

                        table.Cell().Border(0.5f).BorderColor(QuestPDF.Helpers.Colors.Black)
                            .AlignRight().Padding(4).Text(t => {
                                t.Span("Max Marks: ").Bold().FontSize(8.5f);
                                t.Span(paper.MaxMarks).FontSize(8.5f);
                            });
                    });

                    mainCol.Item().PaddingTop(6);

                    // ── Question Table ──
                    mainCol.Item().Table(qTable =>
                    {
                        qTable.ColumnsDefinition(cols =>
                        {
                            cols.ConstantColumn(28);   // Q No (DXA ~540)
                            cols.ConstantColumn(20);   // Sub (DXA ~400)
                            cols.RelativeColumn();     // Question (DXA ~7366)
                            cols.ConstantColumn(39);   // Marks (DXA ~780)
                            cols.ConstantColumn(35);   // BT (DXA ~700)
                            cols.ConstantColumn(34);   // CO (DXA ~680)
                        });

                        // Note row (Full Span)
                        if (!string.IsNullOrWhiteSpace(paper.Note))
                        {
                            qTable.Cell().ColumnSpan(6).Border(0.5f).BorderColor(QuestPDF.Helpers.Colors.Black)
                                .AlignCenter().Padding(4)
                                .Text(text =>
                                {
                                    text.Span("Note: ").Bold().Italic().FontSize(9);
                                    text.Span(paper.Note ?? "").Italic().FontSize(9);
                                });
                        }

                        // Header row
                        var headerBg = "#D4D4D4";
                        AddQuestPdfHeaderCell(qTable, "Q No", headerBg);
                        AddQuestPdfHeaderCell(qTable, "", headerBg);
                        AddQuestPdfHeaderCell(qTable, "Question", headerBg);
                        AddQuestPdfHeaderCell(qTable, "Marks", headerBg);
                        AddQuestPdfHeaderCell(qTable, "BT*", headerBg);
                        AddQuestPdfHeaderCell(qTable, "CO*", headerBg);

                        // Units and questions
                        foreach (var unit in paper.Units ?? new List<QuestionPaperUnit>())
                        {
                            if (unit.Name == "OR")
                            {
                                qTable.Cell().ColumnSpan(6).Border(0.5f).BorderColor(QuestPDF.Helpers.Colors.Black)
                                    .AlignCenter().Padding(4).Text("OR").Bold().FontSize(9);
                                continue;
                            }

                            // Unit header row
                            qTable.Cell().ColumnSpan(6).Border(0.5f).BorderColor(QuestPDF.Helpers.Colors.Black)
                                .Background("#E8E8E8").AlignCenter().Padding(4)
                                .Text(unit.Name ?? "").Bold().FontSize(9.5f);

                            foreach (var q in unit.Questions ?? new List<QuestionPaperQuestion>())
                            {
                                if (q.No == "OR")
                                {
                                    qTable.Cell().ColumnSpan(6).Border(0.5f).BorderColor(QuestPDF.Helpers.Colors.Black)
                                        .AlignCenter().Padding(4).Text("OR").Bold().FontSize(9);
                                    continue;
                                }

                                int partCount = q.Parts?.Count ?? 0;
                                for (int i = 0; i < partCount; i++)
                                {
                                    var part = q.Parts![i];

                                    // Spanned Q No cell
                                    if (i == 0)
                                    {
                                        qTable.Cell().RowSpan((uint)Math.Max(1, partCount))
                                            .Border(0.5f).BorderColor(QuestPDF.Helpers.Colors.Black)
                                            .AlignCenter().AlignMiddle().Padding(3)
                                            .Text(q.No ?? "").Bold().FontSize(9);
                                    }

                                    qTable.Cell().Border(0.5f).BorderColor(QuestPDF.Helpers.Colors.Black)
                                        .AlignCenter().AlignMiddle().Padding(3).Text(part.Sub ?? "").FontSize(9);

                                    qTable.Cell().Border(0.5f).BorderColor(QuestPDF.Helpers.Colors.Black)
                                        .Padding(4).Text(part.Text ?? "").FontSize(9);

                                    qTable.Cell().Border(0.5f).BorderColor(QuestPDF.Helpers.Colors.Black)
                                        .AlignCenter().AlignMiddle().Padding(3).Text(part.Marks ?? "").FontSize(9);

                                    qTable.Cell().Border(0.5f).BorderColor(QuestPDF.Helpers.Colors.Black)
                                        .AlignCenter().AlignMiddle().Padding(3).Text(part.Bt ?? "").FontSize(9);

                                    qTable.Cell().Border(0.5f).BorderColor(QuestPDF.Helpers.Colors.Black)
                                        .AlignCenter().AlignMiddle().Padding(3).Text(part.Co ?? "").FontSize(9);
                                }
                            }
                        }

                        // Footer row (Full Span)
                        if (!string.IsNullOrWhiteSpace(paper.Footer))
                        {
                            qTable.Cell().ColumnSpan(6).Border(0.5f).BorderColor(QuestPDF.Helpers.Colors.Black)
                                .Padding(4)
                                .Text(paper.Footer).FontSize(8);
                        }
                    });
                });
            });
        });

        return doc.GeneratePdf();
    }

    private static void AddQuestPdfHeaderCell(QuestPDF.Fluent.TableDescriptor table, string text, string bgColor)
    {
        table.Cell().Border(0.5f).BorderColor(QuestPDF.Helpers.Colors.Black)
            .Background(bgColor).AlignCenter().AlignMiddle().Padding(4)
            .Text(text).Bold().FontSize(9);
    }

    // ─────────────────────────────────────────────────────────────────
    // MARKDOWN
    // ─────────────────────────────────────────────────────────────────

    public byte[] GenerateMarkdown(QuestionPaperData paper)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"# {paper.Code}{paper.Title}");
        sb.AppendLine();
        sb.AppendLine($"**{paper.Semester} Mid Semester Examination {paper.ExamNo}**");
        sb.AppendLine();
        sb.AppendLine($"**Duration:** {paper.Duration} | **Date:** {paper.Date} {paper.Time} | **Max Marks:** {paper.MaxMarks}");
        sb.AppendLine();
        if (!string.IsNullOrWhiteSpace(paper.Note))
            sb.AppendLine($"> *Note: {paper.Note}*");
        sb.AppendLine();

        foreach (var unit in paper.Units)
        {
            if (unit.Name == "OR")
            {
                sb.AppendLine();
                sb.AppendLine("---");
                sb.AppendLine("**OR**");
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

        return Encoding.UTF8.GetBytes(sb.ToString());
    }

    // ─────────────────────────────────────────────────────────────────
    // LATEX
    // ─────────────────────────────────────────────────────────────────

    public byte[] GenerateLatex(QuestionPaperData paper)
    {
        var sb = new StringBuilder();

        sb.AppendLine(@"\documentclass[a4paper,10pt]{article}");
        sb.AppendLine(@"\usepackage[utf8]{inputenc}");
        sb.AppendLine(@"\usepackage[T1]{fontenc}");
        sb.AppendLine(@"\usepackage{geometry}");
        sb.AppendLine(@"\geometry{a4paper, margin=1.27cm}");
        sb.AppendLine(@"\usepackage{longtable}");
        sb.AppendLine(@"\usepackage{booktabs}");
        sb.AppendLine(@"\usepackage{array}");
        sb.AppendLine(@"\usepackage{xcolor}");
        sb.AppendLine(@"\usepackage{colortbl}");
        sb.AppendLine(@"\usepackage{graphicx}");
        sb.AppendLine(@"\usepackage{multirow}");
        sb.AppendLine(@"\definecolor{headercolor}{RGB}{212, 212, 212}");
        sb.AppendLine(@"\definecolor{unitcolor}{RGB}{232, 232, 232}");
        sb.AppendLine(@"\begin{document}");
        sb.AppendLine();

        // Header
        sb.AppendLine(@"\begin{center}");
        sb.AppendLine($@"{{\Large \textbf{{{Esc(paper.Code)}{Esc(paper.Title)}}}}}\\[4pt]");
        sb.AppendLine($@"\textbf{{{Esc(paper.Semester)} Mid Semester Examination {Esc(paper.ExamNo)}}}\\[4pt]");
        sb.AppendLine($@"\textbf{{Duration:}} {Esc(paper.Duration)} \hfill \textbf{{Date:}} {Esc(paper.Date)} {Esc(paper.Time)} \hfill \textbf{{Max Marks:}} {Esc(paper.MaxMarks)}");
        sb.AppendLine(@"\end{center}");
        sb.AppendLine(@"\vspace{0.3cm}");

        if (!string.IsNullOrWhiteSpace(paper.Note))
        {
            sb.AppendLine($@"\noindent\textit{{\textbf{{Note:}} {Esc(paper.Note)}}}");
            sb.AppendLine(@"\vspace{0.3cm}");
        }

        // Question table
        sb.AppendLine(@"\begin{longtable}{|p{0.8cm}|p{0.6cm}|p{10cm}|p{1cm}|p{0.8cm}|p{0.8cm}|}");
        sb.AppendLine(@"\hline");
        sb.AppendLine(@"\rowcolor{headercolor} \textbf{Q No} & & \textbf{Question} & \textbf{Marks} & \textbf{BT*} & \textbf{CO*} \\");
        sb.AppendLine(@"\hline");
        sb.AppendLine(@"\endhead");

        foreach (var unit in paper.Units)
        {
            if (unit.Name == "OR")
            {
                sb.AppendLine(@"\multicolumn{6}{|c|}{\textbf{OR}} \\");
                sb.AppendLine(@"\hline");
                continue;
            }

            sb.AppendLine($@"\rowcolor{{unitcolor}} \multicolumn{{6}}{{|c|}}{{\textbf{{{Esc(unit.Name)}}}}} \\");
            sb.AppendLine(@"\hline");

            foreach (var q in unit.Questions)
            {
                if (q.No == "OR")
                {
                    sb.AppendLine(@"\multicolumn{6}{|c|}{\textbf{OR}} \\");
                    sb.AppendLine(@"\hline");
                    continue;
                }

                for (int i = 0; i < q.Parts.Count; i++)
                {
                    var p = q.Parts[i];
                    if (i == 0 && q.Parts.Count > 1)
                        sb.Append($@"\multirow{{{q.Parts.Count}}}{{*}}{{{Esc(q.No)}}}");
                    else if (i == 0)
                        sb.Append(Esc(q.No));
                    else
                        sb.Append(""); // empty for spanned rows

                    sb.AppendLine($@" & {Esc(p.Sub)} & {Esc(p.Text)} & {Esc(p.Marks)} & {Esc(p.Bt)} & {Esc(p.Co)} \\");

                    if (i == q.Parts.Count - 1)
                        sb.AppendLine(@"\hline");
                    else
                        sb.AppendLine(@"\cline{2-6}");
                }
            }
        }

        sb.AppendLine(@"\end{longtable}");

        if (!string.IsNullOrWhiteSpace(paper.Footer))
        {
            sb.AppendLine($@"\noindent\small {Esc(paper.Footer)}");
        }

        sb.AppendLine(@"\end{document}");
        return Encoding.UTF8.GetBytes(sb.ToString());
    }

    private static string Esc(string? s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        return s
            .Replace(@"\", @"[BACKSLASH]") // Temporary placeholder
            .Replace("{", @"\{")
            .Replace("}", @"\}")
            .Replace("&", @"\&")
            .Replace("%", @"\%")
            .Replace("$", @"\$")
            .Replace("#", @"\#")
            .Replace("_", @"\_")
            .Replace("~", @"\textasciitilde{}")
            .Replace("^", @"\textasciicircum{}")
            .Replace("[BACKSLASH]", @"\textbackslash{}");
    }

    // ─────────────────────────────────────────────────────────────────
    // PRIVATE: Serialize C# model → qn_format.json-compatible JSON
    // ─────────────────────────────────────────────────────────────────

      /// <summary>
    /// Converts the QuestionPaperData model to the raw JSON format expected by
    /// LLM_qn_paper_generator.js, including "OR" string entries in arrays.
      /// </summary>
    private static string SerializeToGeneratorFormat(QuestionPaperData paper, JsonSerializerOptions options)
    {
        // Build a dynamic structure that matches qn_format.json exactly
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

        return JsonSerializer.Serialize(new[] { rawPaper }, options);
    }
}

  
/// Provides access to the Tools directory path without coupling to ASP.NET's IWebHostEnvironment.
  
public interface IWebHostEnvironmentAccessor
{
    string ToolsDirectory { get; }
}

public class WebHostEnvironmentAccessor : IWebHostEnvironmentAccessor
{
    public string ToolsDirectory { get; }

    public WebHostEnvironmentAccessor(Microsoft.AspNetCore.Hosting.IWebHostEnvironment env)
    {
        ToolsDirectory = Path.Combine(env.ContentRootPath, "Tools");
    }
}

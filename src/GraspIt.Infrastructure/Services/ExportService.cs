using System.IO;
using System.Text;
using System.Threading.Tasks;
using GraspIt.Core.Interfaces;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace GraspIt.Infrastructure.Services;

  
/// Provides multi-format export of AI-generated exam content.
/// All formats (DOCX, PDF, Markdown, LaTeX) faithfully reproduce the
/// structural layout of the original reference paper.
  
public class ExportService : IExportService
{
    public ExportService()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    // ─────────────────────────────────────────────────────────────────
    // DOCX – structured via ExamPaperStructure
    // ─────────────────────────────────────────────────────────────────

    public async Task<byte[]> ToDocxAsync(string markdownContent)
    {
        // Parse the markdown back into an ExamPaperStructure for proper formatting
        var structure = ParseMarkdownToStructure(markdownContent);
        var renderer = new ExamRendererService();
        return await renderer.RenderExamToDocxAsync(structure);
    }

    // ─────────────────────────────────────────────────────────────────
    // PDF – structured via QuestPDF with proper table rendering
    // ─────────────────────────────────────────────────────────────────

    public Task<byte[]> ToPdfAsync(string markdownContent)
    {
        var structure = ParseMarkdownToStructure(markdownContent);
        var doc = QuestPDF.Fluent.Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(1.5f, Unit.Centimetre);
                page.PageColor(Colors.White);
                page.DefaultTextStyle(x => x.FontSize(10).FontColor(Colors.Black));

                // ── Page header ──
                page.Header().Column(col =>
                {
                    if (structure.HeaderLines.Any())
                    {
                        col.Item().AlignCenter()
                           .Text(structure.HeaderLines.FirstOrDefault() ?? "AI Generated Examination")
                           .FontSize(16).Bold().FontColor(Colors.Black);

                        foreach (var hl in structure.HeaderLines.Skip(1))
                        {
                            col.Item().AlignCenter()
                               .Text(hl).FontSize(11);
                        }
                    }
                    else
                    {
                        col.Item().AlignCenter()
                           .Text("AI Generated Examination").FontSize(16).Bold();
                    }
                    col.Item().PaddingTop(6).LineHorizontal(1).LineColor(Colors.Grey.Medium);
                });

                // ── Content: table per section ──
                page.Content().PaddingTop(10).Column(mainCol =>
                {
                    mainCol.Spacing(12);
                    foreach (var section in structure.Sections)
                    {
                        if (!section.Questions.Any()) continue;

                        // Section heading
                        if (!string.IsNullOrWhiteSpace(section.Heading))
                        {
                            mainCol.Item().Background(Colors.Grey.Darken3).Padding(6).Column(hCol =>
                            {
                                hCol.Item().AlignCenter()
                                    .Text(section.Heading).FontSize(12).Bold().FontColor(Colors.White);
                                if (!string.IsNullOrWhiteSpace(section.SubHeading))
                                    hCol.Item().AlignCenter()
                                        .Text(section.SubHeading).FontSize(9).FontColor(Colors.Grey.Lighten2);
                            });
                        }

                        // Questions table
                        mainCol.Item().Table(table =>
                        {
                            // Define columns
                            table.ColumnsDefinition(cols =>
                            {
                                cols.ConstantColumn(50);   // Q No
                                cols.RelativeColumn(5);    // Question
                                cols.ConstantColumn(45);   // Marks
                                cols.ConstantColumn(40);   // BT
                                cols.ConstantColumn(45);   // CO
                            });

                            // Header row
                            var headerStyle = TextStyle.Default.FontSize(10).Bold().FontColor(Colors.White);
                            AddPdfCell(table, "Q No",     Colors.Grey.Darken3, headerStyle, isHeader: true);
                            AddPdfCell(table, "Question", Colors.Grey.Darken3, headerStyle, isHeader: true);
                            AddPdfCell(table, "Marks",    Colors.Grey.Darken3, headerStyle, isHeader: true);
                            AddPdfCell(table, "BT",       Colors.Grey.Darken3, headerStyle, isHeader: true);
                            AddPdfCell(table, "CO",       Colors.Grey.Darken3, headerStyle, isHeader: true);

                            // Data rows
                            bool alternate = false;
                            foreach (var q in section.Questions)
                            {
                                var fill = alternate ? Colors.Grey.Lighten4 : Colors.White;
                                var bodyStyle = TextStyle.Default.FontSize(9);
                                AddPdfCell(table, q.QNo,   fill, bodyStyle);
                                AddPdfCell(table, q.Text,  fill, bodyStyle);
                                AddPdfCell(table, q.Marks, fill, bodyStyle, centre: true);
                                AddPdfCell(table, q.BT,    fill, bodyStyle, centre: true);
                                AddPdfCell(table, q.CO,    fill, bodyStyle, centre: true);

                                // Sub-parts
                                foreach (var sp in q.SubParts)
                                {
                                    var spFill = alternate ? Colors.Grey.Lighten5 : Colors.Grey.Lighten4;
                                    AddPdfCell(table, "  " + sp.QNo, spFill, bodyStyle);
                                    AddPdfCell(table, "  " + sp.Text, spFill, TextStyle.Default.FontSize(8).Italic());
                                    AddPdfCell(table, sp.Marks, spFill, bodyStyle, centre: true);
                                    AddPdfCell(table, sp.BT,    spFill, bodyStyle, centre: true);
                                    AddPdfCell(table, sp.CO,    spFill, bodyStyle, centre: true);
                                }

                                alternate = !alternate;
                            }
                        });
                    }
                });

                // ── Page footer ──
                page.Footer().AlignCenter().Text(x =>
                {
                    x.Span("GraspIt AI · Page ").FontSize(8).FontColor(Colors.Grey.Medium);
                    x.CurrentPageNumber().FontSize(8).FontColor(Colors.Grey.Medium);
                    x.Span(" of ").FontSize(8).FontColor(Colors.Grey.Medium);
                    x.TotalPages().FontSize(8).FontColor(Colors.Grey.Medium);
                });
            });
        });

        return Task.FromResult(doc.GeneratePdf());
    }

    // ─────────────────────────────────────────────────────────────────
    // Markdown – preserve raw markdown (already formatted correctly)
    // ─────────────────────────────────────────────────────────────────

    public byte[] ToMarkdown(string markdownContent)
    {
        return Encoding.UTF8.GetBytes(markdownContent);
    }

    // ─────────────────────────────────────────────────────────────────
    // LaTeX – convert markdown table → LaTeX longtable
    // ─────────────────────────────────────────────────────────────────

    public byte[] ToLatex(string markdownContent)
    {
        var structure = ParseMarkdownToStructure(markdownContent);
        var sb = new StringBuilder();

        sb.AppendLine(@"\documentclass[a4paper,12pt]{article}");
        sb.AppendLine(@"\usepackage[utf8]{inputenc}");
        sb.AppendLine(@"\usepackage[T1]{fontenc}");
        sb.AppendLine(@"\usepackage{geometry}");
        sb.AppendLine(@"\geometry{a4paper, margin=2cm}");
        sb.AppendLine(@"\usepackage{longtable}");
        sb.AppendLine(@"\usepackage{booktabs}");
        sb.AppendLine(@"\usepackage{array}");
        sb.AppendLine(@"\usepackage{xcolor}");
        sb.AppendLine(@"\usepackage{colortbl}");
        sb.AppendLine(@"\definecolor{headercolor}{RGB}{17, 24, 39}");
        sb.AppendLine(@"\definecolor{sectioncolor}{RGB}{55, 65, 81}");
        sb.AppendLine(@"\begin{document}");
        sb.AppendLine();

        // Header
        if (structure.HeaderLines.Any())
        {
            sb.AppendLine(@"\begin{center}");
            sb.AppendLine($@"{{\Large \textbf{{{LatexEscape(structure.HeaderLines.FirstOrDefault() ?? "AI Generated Examination")}}}}}\\[4pt]");
            foreach (var hl in structure.HeaderLines.Skip(1))
                sb.AppendLine($@"{{\normalsize {LatexEscape(hl)}}}\\");
            sb.AppendLine(@"\end{center}");
            sb.AppendLine(@"\vspace{0.5cm}");
        }

        // Sections and questions
        foreach (var section in structure.Sections)
        {
            if (!section.Questions.Any()) continue;

            if (!string.IsNullOrWhiteSpace(section.Heading))
            {
                sb.AppendLine();
                sb.AppendLine($@"\noindent\colorbox{{sectioncolor}}{{\textcolor{{white}}{{\textbf{{\large {LatexEscape(section.Heading)}}}}}}}");
                if (!string.IsNullOrWhiteSpace(section.SubHeading))
                    sb.AppendLine($@"\\ \textit{{\small {LatexEscape(section.SubHeading)}}}");
                sb.AppendLine(@"\vspace{4pt}");
            }

            sb.AppendLine(@"\begin{longtable}{|p{1.2cm}|p{9.5cm}|p{1.2cm}|p{1cm}|p{1cm}|}");
            sb.AppendLine(@"\hline");
            sb.AppendLine(@"\rowcolor{headercolor}\textcolor{white}{\textbf{Q No}} & \textcolor{white}{\textbf{Question}} & \textcolor{white}{\textbf{Marks}} & \textcolor{white}{\textbf{BT}} & \textcolor{white}{\textbf{CO}} \\");
            sb.AppendLine(@"\hline");
            sb.AppendLine(@"\endhead");

            foreach (var q in section.Questions)
            {
                sb.AppendLine($@"{LatexEscape(q.QNo)} & {LatexEscape(q.Text)} & {LatexEscape(q.Marks)} & {LatexEscape(q.BT)} & {LatexEscape(q.CO)} \\");
                sb.AppendLine(@"\hline");
                foreach (var sp in q.SubParts)
                {
                    sb.AppendLine($@"\quad {LatexEscape(sp.QNo)} & \quad \textit{{{LatexEscape(sp.Text)}}} & {LatexEscape(sp.Marks)} & {LatexEscape(sp.BT)} & {LatexEscape(sp.CO)} \\");
                    sb.AppendLine(@"\hline");
                }
            }

            sb.AppendLine(@"\end{longtable}");
        }

        sb.AppendLine(@"\end{document}");
        return Encoding.UTF8.GetBytes(sb.ToString());
    }

    // ─────────────────────────────────────────────────────────────────
    // PRIVATE: MARKDOWN → ExamPaperStructure
    // ─────────────────────────────────────────────────────────────────

    private static ExamPaperStructure ParseMarkdownToStructure(string markdown)
    {
        var structure = new ExamPaperStructure { HasTableStructure = true };
        var lines = markdown.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

        ExamSection currentSection = new ExamSection();
        bool inTable = false;
        bool headerRowSeen = false;

        foreach (var line in lines)
        {
            var trimmed = line.Trim();

            // Headings
            if (trimmed.StartsWith("# "))
            {
                structure.HeaderLines.Add(trimmed.TrimStart('#').Trim());
                continue;
            }
            if (trimmed.StartsWith("## ") || trimmed.StartsWith("**") && !trimmed.Contains("|"))
            {
                structure.HeaderLines.Add(trimmed.TrimStart('#').Trim('*').Trim());
                continue;
            }

            // Table separator line (e.g. |---|---|)
            if (trimmed.StartsWith("|") && trimmed.All(c => c == '|' || c == '-' || c == ':' || c == ' '))
            {
                headerRowSeen = true;
                inTable = true;
                continue;
            }

            // Table row
            if (trimmed.StartsWith("|"))
            {
                inTable = true;
                var cells = trimmed.Split('|')
                    .Select(c => c.Trim())
                    .Where(c => !string.IsNullOrWhiteSpace(c))
                    .ToList();

                if (!headerRowSeen) { headerRowSeen = true; continue; } // skip header row content

                if (cells.Count < 2) continue;

                // Section row detection (bold heading, mostly empty cells)
                bool isSectionRow = cells[0].StartsWith("**") || cells[0].Contains("PART") || cells[0].Contains("SECTION");
                if (isSectionRow && cells.Skip(1).All(c => string.IsNullOrWhiteSpace(c)))
                {
                    currentSection = new ExamSection
                    {
                        Heading = cells[0].Trim('*').Trim(),
                        SubHeading = cells.Count > 1 ? cells[1].Trim() : ""
                    };
                    structure.Sections.Add(currentSection);
                    continue;
                }

                // Normal question row
                var q = new ExamQuestion
                {
                    QNo      = cells.Count > 0 ? cells[0] : "",
                    Text     = cells.Count > 1 ? cells[1] : "",
                    Marks    = cells.Count > 2 ? cells[2] : "",
                    BT       = cells.Count > 3 ? cells[3] : "",
                    CO       = cells.Count > 4 ? cells[4] : ""
                };

                if (!string.IsNullOrWhiteSpace(q.Text) || !string.IsNullOrWhiteSpace(q.QNo))
                    currentSection.Questions.Add(q);

                continue;
            }

            // Non-table text – treat as header / instructions
            if (!inTable && !string.IsNullOrWhiteSpace(trimmed))
                structure.HeaderLines.Add(trimmed);
        }

        // Ensure at least one section with questions
        if (!structure.Sections.Any() || structure.Sections.All(s => !s.Questions.Any()))
        {
            if (currentSection.Questions.Any())
                structure.Sections.Add(currentSection);
        }

        return structure;
    }

    // ─────────────────────────────────────────────────────────────────
    // PRIVATE: PDF CELL HELPER
    // ─────────────────────────────────────────────────────────────────

    private static void AddPdfCell(
        TableDescriptor table,
        string text,
        string bgColor,
        TextStyle style,
        bool isHeader = false,
        bool centre = false)
    {
        table.Cell().Background(bgColor).Border(0.5f).BorderColor(Colors.Grey.Medium)
             .Padding(4).Element(c =>
             {
                 if (centre || isHeader)
                     c.AlignCenter().Text(text ?? "").Style(style);
                 else
                     c.AlignLeft().Text(text ?? "").Style(style);
             });
    }

    // ─────────────────────────────────────────────────────────────────
    // PRIVATE: LaTeX ESCAPING
    // ─────────────────────────────────────────────────────────────────

    private static string LatexEscape(string? s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        return s
            .Replace("\\", "\\textbackslash{}")
            .Replace("&", "\\&")
            .Replace("%", "\\%")
            .Replace("$", "\\$")
            .Replace("#", "\\#")
            .Replace("_", "\\_")
            .Replace("{", "\\{")
            .Replace("}", "\\}")
            .Replace("~", "\\textasciitilde{}")
            .Replace("^", "\\textasciicircum{}");
    }
}

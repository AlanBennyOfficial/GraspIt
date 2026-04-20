using System.IO;
using System.Collections.Generic;
using System.Threading.Tasks;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace GraspIt.Infrastructure.Services;

/// <summary>
/// Renders a structured exam paper into a properly formatted Word (DOCX) document.
/// The document faithfully mirrors the sections, table layout, header, and column
/// structure of the original uploaded reference paper.
/// </summary>
public interface IExamRendererService
{
    Task<byte[]> RenderExamToDocxAsync(ExamPaperStructure structure);
    // Keep legacy overload for compatibility
    Task<byte[]> RenderExamToDocxAsync(List<QuestionRow> questions);
}

public class ExamRendererService : IExamRendererService
{
    // ─────────────────────────────────────────────────────────────────
    // PUBLIC API
    // ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Primary renderer: uses the full parsed structure to faithfully mirror the original paper.
    /// </summary>
    public Task<byte[]> RenderExamToDocxAsync(ExamPaperStructure structure)
    {
        using var stream = new MemoryStream();
        using (var doc = WordprocessingDocument.Create(stream, WordprocessingDocumentType.Document, true))
        {
            var mainPart = doc.AddMainDocumentPart();
            mainPart.Document = new DocumentFormat.OpenXml.Wordprocessing.Document();
            var body = mainPart.Document.AppendChild(new Body());

            // ── Page setup: A4 margins ──
            body.AppendChild(new SectionProperties(
                new PageMargin { Top = 1080, Bottom = 1080, Left = 1080, Right = 1080 }));

            // ── Header block (institution name, title, date) ──
            foreach (var headerLine in structure.HeaderLines)
            {
                if (string.IsNullOrWhiteSpace(headerLine)) continue;
                bool isTitle = structure.HeaderLines.IndexOf(headerLine) <= 1;
                AddHeadingParagraph(body, headerLine, isTitle ? "28" : "20", JustificationValues.Center, isTitle);
            }
            AddSpacerParagraph(body);

            // ── Question table ──
            var table = BuildQuestionTable(structure);
            body.InsertBefore(table, body.LastChild!); // insert before section props

            mainPart.Document.Save();
        }

        return Task.FromResult(stream.ToArray());
    }

    /// <summary>
    /// Legacy overload: builds a simple table from a flat list of QuestionRow objects.
    /// Used as a fallback when the structured extraction is unavailable.
    /// </summary>
    public Task<byte[]> RenderExamToDocxAsync(List<QuestionRow> questions)
    {
        var structure = new ExamPaperStructure
        {
            HasTableStructure = true,
            HeaderLines = new List<string> { "AI Generated Mock Examination", $"Date: {DateTime.Now:dd MMMM yyyy}" },
            Sections = new List<ExamSection>
            {
                new ExamSection
                {
                    Heading = string.Empty,
                    Questions = questions.Select(q => new ExamQuestion
                    {
                        QNo = q.QNo ?? "",
                        Text = q.Question ?? "",
                        Marks = q.Marks ?? "",
                        BT = q.BT ?? "",
                        CO = q.CO ?? ""
                    }).ToList()
                }
            }
        };
        return RenderExamToDocxAsync(structure);
    }

    // ─────────────────────────────────────────────────────────────────
    // PRIVATE: TABLE BUILDER
    // ─────────────────────────────────────────────────────────────────

    private static Table BuildQuestionTable(ExamPaperStructure structure)
    {
        var table = new Table();

        // Table-level properties
        table.AppendChild(new TableProperties(
            new TableWidth { Width = "9638", Type = TableWidthUnitValues.Dxa },  // full page width
            new TableBorders(
                new TopBorder    { Val = BorderValues.Single, Size = 6, Color = "000000" },
                new BottomBorder { Val = BorderValues.Single, Size = 6, Color = "000000" },
                new LeftBorder   { Val = BorderValues.Single, Size = 6, Color = "000000" },
                new RightBorder  { Val = BorderValues.Single, Size = 6, Color = "000000" },
                new InsideHorizontalBorder { Val = BorderValues.Single, Size = 4, Color = "888888" },
                new InsideVerticalBorder   { Val = BorderValues.Single, Size = 4, Color = "888888" }
            ),
            new TableLook { Val = "04A0" }
        ));

        // Determine column configuration based on detected columns
        var cols = structure.TableColumns;
        var colWidths = ComputeColumnWidths(cols);

        // Table grid
        var tblGrid = new TableGrid();
        foreach (var w in colWidths)
            tblGrid.AppendChild(new GridColumn { Width = w.ToString() });
        table.AppendChild(tblGrid);

        // Header row (shaded)
        table.AppendChild(BuildHeaderRow(cols, colWidths));

        // Data rows per section
        foreach (var section in structure.Sections)
        {
            if (!section.Questions.Any()) continue;

            // Section heading row (spans all columns)
            if (!string.IsNullOrWhiteSpace(section.Heading))
            {
                table.AppendChild(BuildSectionRow(section.Heading, section.SubHeading, cols.Count));
            }

            // Question rows
            foreach (var q in section.Questions)
            {
                table.AppendChild(BuildQuestionRow(q, colWidths, cols, isSubPart: false));
                foreach (var sp in q.SubParts)
                    table.AppendChild(BuildQuestionRow(sp, colWidths, cols, isSubPart: true));
            }
        }

        return table;
    }

    // ─────────────────────────────────────────────────────────────────
    // PRIVATE: ROW BUILDERS
    // ─────────────────────────────────────────────────────────────────

    private static TableRow BuildHeaderRow(List<string> columns, List<int> widths)
    {
        var row = new TableRow();
        // Make header row non-breakable and repeat across pages
        row.AppendChild(new TableRowProperties(
            new TableHeader(),
            new CantSplit()));

        for (int i = 0; i < columns.Count; i++)
        {
            var cell = new TableCell();
            cell.AppendChild(new TableCellProperties(
                new TableCellWidth { Width = widths[i].ToString(), Type = TableWidthUnitValues.Dxa },
                new Shading { Val = ShadingPatternValues.Clear, Color = "auto", Fill = "1F2937" }  // dark header
            ));
            var para = new Paragraph();
            para.AppendChild(new ParagraphProperties(
                new Justification { Val = JustificationValues.Center }));
            var run = para.AppendChild(new Run());
            run.AppendChild(new RunProperties(
                new Bold(),
                new Color { Val = "FFFFFF" },  // white text on dark header
                new FontSize { Val = "20" }));
            run.AppendChild(new Text(columns[i]));
            cell.AppendChild(para);
            row.AppendChild(cell);
        }
        return row;
    }

    private static TableRow BuildSectionRow(string heading, string subHeading, int colCount)
    {
        var row = new TableRow();

        var cell = new TableCell();
        cell.AppendChild(new TableCellProperties(
            new GridSpan { Val = colCount },
            new Shading { Val = ShadingPatternValues.Clear, Color = "auto", Fill = "374151" }  // section shade
        ));

        var para = new Paragraph();
        para.AppendChild(new ParagraphProperties(
            new Justification { Val = JustificationValues.Center }));

        var headingRun = para.AppendChild(new Run());
        headingRun.AppendChild(new RunProperties(
            new Bold(),
            new Color { Val = "FFFFFF" },
            new FontSize { Val = "22" }));
        headingRun.AppendChild(new Text(heading));

        if (!string.IsNullOrWhiteSpace(subHeading))
        {
            para.AppendChild(new Run(
                new RunProperties(new Color { Val = "D1D5DB" }, new FontSize { Val = "18" }),
                new Text("  " + subHeading)));
        }

        cell.AppendChild(para);
        row.AppendChild(cell);
        return row;
    }

    private static TableRow BuildQuestionRow(ExamQuestion q, List<int> widths, List<string> cols, bool isSubPart)
    {
        var row = new TableRow();

        var colMap = MapColumnsToQuestion(q, cols, isSubPart);
        for (int i = 0; i < cols.Count; i++)
        {
            var cell = new TableCell();
            cell.AppendChild(new TableCellProperties(
                new TableCellWidth { Width = widths[i].ToString(), Type = TableWidthUnitValues.Dxa },
                new TableCellVerticalAlignment { Val = TableVerticalAlignmentValues.Center }
            ));

            var para = new Paragraph();
            // Centre-align numeric/short columns, left-align question text
            bool centreAlign = i != 1 || IsShortColumn(cols[i]);
            para.AppendChild(new ParagraphProperties(
                new Justification { Val = centreAlign && i != 1 ? JustificationValues.Center : JustificationValues.Left },
                new SpacingBetweenLines { After = "40" }));

            var run = para.AppendChild(new Run());
            run.AppendChild(new RunProperties(
                new FontSize { Val = isSubPart ? "18" : "20" }));

            string text = colMap.TryGetValue(i, out var v) ? v : "";
            if (isSubPart && i == 0) text = "    " + text; // indent sub-parts
            run.AppendChild(new Text(text) { Space = SpaceProcessingModeValues.Preserve });

            cell.AppendChild(para);
            row.AppendChild(cell);
        }
        return row;
    }

    // ─────────────────────────────────────────────────────────────────
    // PRIVATE: UTILITIES
    // ─────────────────────────────────────────────────────────────────

    private static Dictionary<int, string> MapColumnsToQuestion(ExamQuestion q, List<string> cols, bool isSubPart)
    {
        var map = new Dictionary<int, string>();
        for (int i = 0; i < cols.Count; i++)
        {
            var colLower = cols[i].ToLower().Trim();
            map[i] = colLower switch
            {
                var c when c.Contains("q") && (c.Contains("no") || c.Contains("num") || c == "q") => q.QNo,
                var c when c.Contains("question") || c.Contains("description") => q.Text,
                var c when c.Contains("mark") => q.Marks,
                var c when c.Contains("bt") || c.Contains("bloom") || c.Contains("level") || c.Contains("cognitive") => q.BT,
                var c when c.Contains("co") || c.Contains("course") || c.Contains("outcome") => q.CO,
                _ => ""
            };
        }
        return map;
    }

    /// <summary>
    /// Computes DXA widths so columns sum to ~9638 (A4 full width with 1080pt margins each side).
    /// The "Question" column gets the most space.
    /// </summary>
    private static List<int> ComputeColumnWidths(List<string> cols)
    {
        const int total = 9638;
        var widths = new List<int>();

        foreach (var col in cols)
        {
            var cl = col.ToLower().Trim();
            int w = cl switch
            {
                var c when c.Contains("question") || c.Contains("description") => 0, // placeholder for largest
                var c when c.Contains("q") && (c.Contains("no") || c == "q") => 600,
                var c when c.Contains("mark") => 700,
                var c when c.Contains("bt") || c.Contains("bloom") || c.Contains("cognitive") || c.Contains("level") => 700,
                var c when c.Contains("co") || c.Contains("outcome") => 700,
                _ => 900
            };
            widths.Add(w);
        }

        int fixedTotal = widths.Sum();
        int questionIdx = cols.FindIndex(c => c.ToLower().Contains("question") || c.ToLower().Contains("description"));
        if (questionIdx >= 0)
            widths[questionIdx] = total - fixedTotal;
        else if (widths.Any(w => w == 0))
        {
            for (int i = 0; i < widths.Count; i++)
                if (widths[i] == 0) widths[i] = total - fixedTotal;
        }

        return widths;
    }

    private static bool IsShortColumn(string colName) =>
        !colName.ToLower().Contains("question") && !colName.ToLower().Contains("description");

    private static void AddHeadingParagraph(Body body, string text, string fontSize, JustificationValues just, bool bold)
    {
        var para = new Paragraph();
        para.AppendChild(new ParagraphProperties(
            new Justification { Val = just },
            new SpacingBetweenLines { After = "80" }));
        var run = para.AppendChild(new Run());
        var rpr = new RunProperties(new FontSize { Val = fontSize });
        if (bold) rpr.AppendChild(new Bold());
        run.AppendChild(rpr);
        run.AppendChild(new Text(text));
        body.AppendChild(para);
    }

    private static void AddSpacerParagraph(Body body)
    {
        var para = new Paragraph();
        para.AppendChild(new ParagraphProperties(new SpacingBetweenLines { After = "160" }));
        body.AppendChild(para);
    }
}

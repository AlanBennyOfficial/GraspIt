using System.IO;
using System.Text;
using GraspIt.Core.Interfaces;
using UglyToad.PdfPig;
using DocumentFormat.OpenXml.Packaging;

namespace GraspIt.Infrastructure.Services;

  
/// Multi-format document text extractor.
/// Supports: PDF (.pdf), Word (.docx), Plaintext (.txt), Markdown (.md), and CSV (.csv).
/// Falls back gracefully on corrupted or unsupported files.
  
public class PdfParserService : IPdfParserService
{
    private static readonly IReadOnlyList<string> _supportedExtensions = new[]
    {
        ".pdf", ".docx", ".txt", ".md", ".csv", ".pptx", ".jpeg", ".jpg", ".png"
    };

    public IReadOnlyList<string> SupportedExtensions => _supportedExtensions;

    public async Task<string> ExtractTextAsync(string filePath)
    {
        if (!File.Exists(filePath))
            return string.Empty;

        var extension = Path.GetExtension(filePath).ToLowerInvariant();

        try
        {
            var text = extension switch
            {
                ".pdf" => ExtractFromPdf(filePath),
                ".docx" => ExtractFromDocx(filePath),
                ".pptx" => ExtractFromPptx(filePath),
                ".jpeg" or ".jpg" or ".png" => await ExtractFromImage(filePath),
                ".txt" or ".md" or ".csv" => ExtractFromTextFile(filePath),
                _ => string.Empty
            };

            return text.Trim();
        }
        catch (Exception ex)
        {
            // Gracefully return empty so background Hangfire jobs don't crash
            Console.WriteLine($"[Error] Document parsing failed for '{filePath}': {ex.Message}");
            return string.Empty;
        }
    }

      /// <summary>
    /// Extracts text from PDF files using UglyToad.PdfPig.
      /// </summary>
    private static string ExtractFromPdf(string filePath)
    {
        var sb = new StringBuilder();
        using (var document = PdfDocument.Open(filePath))
        {
            foreach (var page in document.GetPages())
            {
                sb.AppendLine(page.Text);
                sb.AppendLine();
            }
        }
        return sb.ToString();
    }

      /// <summary>
    /// Extracts text from Word (.docx) files using DocumentFormat.OpenXml.
    /// Handles paragraphs and tables, preserving logical reading order.
      /// </summary>
    private static string ExtractFromDocx(string filePath)
    {
        var sb = new StringBuilder();
        using var wordDoc = WordprocessingDocument.Open(filePath, false);
        var body = wordDoc.MainDocumentPart?.Document?.Body;
        if (body == null) return string.Empty;

        foreach (var element in body.ChildElements)
        {
            // Process paragraphs
            if (element is DocumentFormat.OpenXml.Wordprocessing.Paragraph para)
            {
                var text = para.InnerText;
                if (!string.IsNullOrWhiteSpace(text))
                {
                    sb.AppendLine(text);
                }
                else
                {
                    // Empty paragraph = line break
                    sb.AppendLine();
                }
            }
            // Process tables: extract row-by-row text
            else if (element is DocumentFormat.OpenXml.Wordprocessing.Table table)
            {
                foreach (var row in table.Elements<DocumentFormat.OpenXml.Wordprocessing.TableRow>())
                {
                    var cells = row.Elements<DocumentFormat.OpenXml.Wordprocessing.TableCell>();
                    var cellTexts = cells.Select(c => c.InnerText.Trim());
                    sb.AppendLine(string.Join(" | ", cellTexts));
                }
                sb.AppendLine();
            }
        }
        return sb.ToString();
    }

      /// <summary>
    /// Reads plaintext files (TXT, Markdown, CSV) directly.
      /// </summary>
    private static string ExtractFromTextFile(string filePath)
    {
        return File.ReadAllText(filePath, Encoding.UTF8);
    }

      /// <summary>
    /// Extracts text from PowerPoint (.pptx) files using DocumentFormat.OpenXml.
      /// </summary>
    private static string ExtractFromPptx(string filePath)
    {
        var sb = new StringBuilder();
        using var pptDoc = PresentationDocument.Open(filePath, false);
        if (pptDoc.PresentationPart == null || pptDoc.PresentationPart.Presentation.SlideIdList == null) return string.Empty;

        foreach (var slideId in pptDoc.PresentationPart.Presentation.SlideIdList.Elements<DocumentFormat.OpenXml.Presentation.SlideId>())
        {
            if (slideId.RelationshipId == null) continue;
            var slidePart = (SlidePart)pptDoc.PresentationPart.GetPartById(slideId.RelationshipId);
            if (slidePart.Slide != null)
            {
                foreach (var text in slidePart.Slide.Descendants<DocumentFormat.OpenXml.Drawing.Text>())
                {
                    if (!string.IsNullOrWhiteSpace(text.Text))
                        sb.AppendLine(text.Text);
                }
                sb.AppendLine();
            }
        }
        return sb.ToString();
    }

      /// <summary>
    /// Extracts text from Images using Tesseract OCR.
      /// </summary>
    private static async Task<string> ExtractFromImage(string filePath)
    {
        var tessdataPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tessdata");
        if (!Directory.Exists(tessdataPath)) Directory.CreateDirectory(tessdataPath);
        var trainedDataPath = Path.Combine(tessdataPath, "eng.traineddata");
        
        if (!File.Exists(trainedDataPath))
        {
            Console.WriteLine("[Info] Downloading Tesseract English trained data...");
            using var client = new HttpClient();
            var dataBytes = await client.GetByteArrayAsync("https://github.com/tesseract-ocr/tessdata_fast/raw/main/eng.traineddata");
            await File.WriteAllBytesAsync(trainedDataPath, dataBytes);
        }

        try
        {
            using var engine = new Tesseract.TesseractEngine(tessdataPath, "eng", Tesseract.EngineMode.Default);
            using var img = Tesseract.Pix.LoadFromFile(filePath);
            using var page = engine.Process(img);
            return page.GetText();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Error] Tesseract OCR failed for '{filePath}': {ex.Message}");
            return string.Empty;
        }
    }
}

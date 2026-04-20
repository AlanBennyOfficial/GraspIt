using System.Text;
using GraspIt.Core.Interfaces;
using UglyToad.PdfPig;

namespace GraspIt.Infrastructure.Services;

public class PdfParserService : IPdfParserService
{
    public Task<string> ExtractTextAsync(string filePath)
    {
        if (!File.Exists(filePath))
            return Task.FromResult(string.Empty);

        var sb = new StringBuilder();

        try
        {
            using (var document = PdfDocument.Open(filePath))
            {
                foreach (var page in document.GetPages())
                {
                    sb.AppendLine(page.Text);
                    sb.AppendLine();
                }
            }
        }
        catch (System.Exception ex)
        {
            // Log the exception but gracefully return the extracted text so far (if any)
            // or an empty string so the background job doesn't completely crash and lock the DB pipeline.
            System.Console.WriteLine($"[Error] PDF Parsing failed: {ex.Message}");
            return Task.FromResult(string.Empty);
        }

        return Task.FromResult(sb.ToString().Trim());
    }
}

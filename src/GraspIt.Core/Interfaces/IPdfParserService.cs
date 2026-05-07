namespace GraspIt.Core.Interfaces;

  
/// Contract for extracting text from uploaded documents.
/// Supports PDF, DOCX (OpenXML), TXT, Markdown, and CSV file formats.
  
public interface IPdfParserService
{
      /// <summary>
    /// Extracts text content from a file at the given path.
    /// The implementation auto-detects file type from the extension.
      /// </summary>
    Task<string> ExtractTextAsync(string filePath);

      /// <summary>
    /// Returns the list of file extensions this parser supports.
      /// </summary>
    IReadOnlyList<string> SupportedExtensions { get; }
}

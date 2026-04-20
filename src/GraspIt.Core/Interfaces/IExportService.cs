namespace GraspIt.Core.Interfaces;

public interface IExportService
{
    Task<byte[]> ToDocxAsync(string markdownContent);
    Task<byte[]> ToPdfAsync(string markdownContent);
    byte[] ToMarkdown(string markdownContent);
    byte[] ToLatex(string markdownContent);
}

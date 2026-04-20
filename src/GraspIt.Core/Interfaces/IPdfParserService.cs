namespace GraspIt.Core.Interfaces;

public interface IPdfParserService
{
    Task<string> ExtractTextAsync(string filePath);
}

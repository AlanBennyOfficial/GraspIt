using FluentAssertions;
using GraspIt.Infrastructure.Services;
using System.IO;
using System.Threading.Tasks;
using Xunit;

namespace GraspIt.Tests.Infrastructure.Services;

public class PdfParserServiceTests
{
    private readonly PdfParserService _service;

    public PdfParserServiceTests()
    {
        _service = new PdfParserService();
    }

    [Fact]
    public async Task ExtractTextAsync_NonExistentFile_ReturnsEmpty()
    {
        var result = await _service.ExtractTextAsync("non-existent.pdf");
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task ExtractTextAsync_InvalidPdfFile_ReturnsEmpty()
    {
        var tempFile = Path.GetTempFileName();
        File.WriteAllText(tempFile, "not a pdf");
        
        try
        {
            var result = await _service.ExtractTextAsync(tempFile);
            result.Should().BeEmpty();
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }
}

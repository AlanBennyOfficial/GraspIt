using FluentAssertions;
using GraspIt.Infrastructure.Services;
using Microsoft.AspNetCore.Http;
using Moq;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Xunit;
using System.Threading;

namespace GraspIt.Tests.Infrastructure.Services;

public class DocumentExtractionServiceTests
{
    private readonly DocumentExtractionService _service;

    public DocumentExtractionServiceTests()
    {
        _service = new DocumentExtractionService();
    }

    private IFormFile CreateMockFile(string content, string fileName)
    {
        var bytes = Encoding.UTF8.GetBytes(content);
        var stream = new MemoryStream(bytes);
        var fileMock = new Mock<IFormFile>();
        fileMock.Setup(f => f.OpenReadStream()).Returns(stream);
        fileMock.Setup(f => f.FileName).Returns(fileName);
        fileMock.Setup(f => f.Length).Returns(bytes.Length);
        fileMock.Setup(f => f.CopyToAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .Callback<Stream, CancellationToken>((s, _) => {
                stream.Position = 0;
                stream.CopyTo(s);
            })
            .Returns(Task.CompletedTask);
        return fileMock.Object;
    }

    [Fact]
    public async Task ExtractStructuredAsync_NullOrEmptyFile_ReturnsEmpty()
    {
        var result = await _service.ExtractStructuredAsync(null);
        result.RawText.Should().BeEmpty();

        var emptyMock = new Mock<IFormFile>();
        emptyMock.Setup(f => f.Length).Returns(0);
        var emptyResult = await _service.ExtractStructuredAsync(emptyMock.Object);
        emptyResult.RawText.Should().BeEmpty();
    }

    [Fact]
    public async Task ExtractStructuredAsync_NonPdf_ReturnsFallback()
    {
        var file = CreateMockFile("Some text", "test.txt");
        var result = await _service.ExtractStructuredAsync(file);

        result.RawText.Should().Be("Fallback template");
        result.Sections.Should().HaveCount(1);
        result.Sections[0].Heading.Should().Be("PART A");
    }

    [Fact]
    public async Task ExtractExamTableAsync_EmptyRawText_ReturnsFallbackTemplate()
    {
        var file = CreateMockFile("Some text", "test.txt");
        var result = await _service.ExtractExamTableAsync(file);
        
        result.Should().Contain("| Q No | Question | Marks | BT | CO |");
    }

    [Fact]
    public void ParseIntoStructure_WithTable_ParsesCorrectly()
    {
        var rawText = "University Name\nDepartment\nSection 1\n| Q No | Question | Marks |\n| 1 | What is X? | 10 |\n| 2 | What is Y? | 5 |\n| 3 | What is Z? | 2 |\n| 4 | Final? | 1 |";
        var result = DocumentExtractionService.ParseIntoStructure(rawText);

        result.HeaderLines.Should().Contain("University Name");
        result.Sections.Should().HaveCountGreaterThan(0);
        result.HasTableStructure.Should().BeTrue();
    }

    [Fact]
    public void ParseIntoStructure_WithNumberedQuestions_ParsesCorrectly()
    {
        var rawText = "1. First Question\n2. Second Question";
        var result = DocumentExtractionService.ParseIntoStructure(rawText);
        
        result.Sections[0].Questions.Should().NotBeEmpty();
    }

    [Fact]
    public void StringExtensions_WorksCorrectly()
    {
        string? n = null;
        string? e = "";
        string? v = "val";

        n.IsNullOrEmpty().Should().BeTrue();
        e.IsNullOrEmpty().Should().BeTrue();
        v.IsNullOrEmpty().Should().BeFalse();
    }
}

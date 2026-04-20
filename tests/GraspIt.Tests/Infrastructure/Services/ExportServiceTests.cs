using FluentAssertions;
using GraspIt.Infrastructure.Services;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace GraspIt.Tests.Infrastructure.Services;

public class ExportServiceTests
{
    private readonly ExportService _exportService;

    public ExportServiceTests()
    {
        _exportService = new ExportService();
    }

    [Fact]
    public void ToMarkdown_ConvertsCorrectly()
    {
        var input = "# Title\n\n**Some test**";
        var result = _exportService.ToMarkdown(input);

        Encoding.UTF8.GetString(result).Should().Be(input);
    }

    [Fact]
    public void ToLatex_ConvertsCorrectlyWithEscaping()
    {
        var input = "| NO | Question | M | BT | CO |\n|---|---|---|---|---|\n| 1 | Test & \\ % $ | 5 | L1 | CO1 |";
        var result = _exportService.ToLatex(input);

        var latexStr = Encoding.UTF8.GetString(result);
        latexStr.Should().Contain("documentclass[a4paper,12pt]{article}");
        
        // escaping tests
        latexStr.Should().Contain(@"Test \& \textbackslash\{\} \% \$");
    }

    [Fact]
    public async Task ToPdfAsync_GeneratesPdf()
    {
        var input = "# Mock Exam\n| 1 | Question | 5 | L1 | CO1 |";
        var result = await _exportService.ToPdfAsync(input);

        result.Should().NotBeNull();
        result.Length.Should().BeGreaterThan(0);
        
        // rudimentary pdf magic number check
        result[0].Should().Be(0x25); // %
        result[1].Should().Be(0x50); // P
        result[2].Should().Be(0x44); // D
        result[3].Should().Be(0x46); // F
        result[4].Should().Be(0x2D); // -
    }

    [Fact]
    public async Task ToDocxAsync_GeneratesDocx()
    {
        var input = "# Mock Exam\n| 1 | Question | 5 | L1 | CO1 |";
        var result = await _exportService.ToDocxAsync(input);

        result.Should().NotBeNull();
        result.Length.Should().BeGreaterThan(0);
        
        // rudimentary PK zip magic number check (docx is a zip file)
        result[0].Should().Be(0x50); // P
        result[1].Should().Be(0x4B); // K
        result[2].Should().Be(0x03);
        result[3].Should().Be(0x04);
    }
}

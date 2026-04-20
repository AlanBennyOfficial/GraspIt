using FluentAssertions;
using GraspIt.Application.Features.Documents.Commands;

namespace GraspIt.Tests.Application.Features.Documents;

public class ProcessDocumentCommandTests
{
    [Fact]
    public void ProcessDocumentCommand_DefaultValues_AreCorrect()
    {
        var cmd = new ProcessDocumentCommand();

        cmd.FilePath.Should().BeEmpty();
        cmd.DocumentType.Should().Be("Other");
        cmd.UserId.Should().Be(0);
    }

    [Fact]
    public void ProcessDocumentCommand_SetProperties_RetainsValues()
    {
        var cmd = new ProcessDocumentCommand
        {
            FilePath = "/uploads/test.pdf",
            DocumentType = "Textbook",
            UserId = 7
        };

        cmd.FilePath.Should().Be("/uploads/test.pdf");
        cmd.DocumentType.Should().Be("Textbook");
        cmd.UserId.Should().Be(7);
    }

    [Theory]
    [InlineData("Textbook")]
    [InlineData("Syllabus")]
    [InlineData("Previous Question Paper")]
    [InlineData("Presentation Slides")]
    [InlineData("Teacher Notes")]
    [InlineData("Other")]
    public void ProcessDocumentCommand_DocumentType_AcceptsAllValidTypes(string docType)
    {
        var cmd = new ProcessDocumentCommand { DocumentType = docType };
        cmd.DocumentType.Should().Be(docType);
    }
}

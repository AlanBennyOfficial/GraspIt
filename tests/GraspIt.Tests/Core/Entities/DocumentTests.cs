using GraspIt.Core.Entities;
using FluentAssertions;

namespace GraspIt.Tests.Core.Entities;

public class DocumentTests
{
    [Fact]
    public void Document_DefaultValues_AreCorrect()
    {
        var doc = new Document();

        doc.Id.Should().Be(0);
        doc.Title.Should().BeEmpty();
        doc.FilePath.Should().BeEmpty();
        doc.DocumentType.Should().Be("Other");
        doc.UploadedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        doc.UserId.Should().Be(0);
    }

    [Fact]
    public void Document_SetProperties_RetainsValues()
    {
        var now = DateTime.UtcNow;
        var doc = new Document
        {
            Id = 42,
            Title = "Machine_Learning.pdf",
            FilePath = "/uploads/Machine_Learning.pdf",
            DocumentType = "Textbook",
            UploadedAt = now,
            UserId = 7
        };

        doc.Id.Should().Be(42);
        doc.Title.Should().Be("Machine_Learning.pdf");
        doc.FilePath.Should().Be("/uploads/Machine_Learning.pdf");
        doc.DocumentType.Should().Be("Textbook");
        doc.UploadedAt.Should().Be(now);
        doc.UserId.Should().Be(7);
    }

    [Theory]
    [InlineData("Textbook")]
    [InlineData("Notes")]
    [InlineData("Syllabus")]
    [InlineData("Previous Question Paper")]
    [InlineData("Presentation Slides")]
    [InlineData("Teacher Notes")]
    [InlineData("Other")]
    [InlineData("")]
    public void Document_DocumentType_AcceptsVariousValues(string docType)
    {
        var doc = new Document { DocumentType = docType };
        doc.DocumentType.Should().Be(docType);
    }

    [Fact]
    public void Document_NavigationProperty_User_CanBeSet()
    {
        var user = new User { Id = 1, Username = "TestUser" };
        var doc = new Document { User = user, UserId = 1 };

        doc.User.Should().NotBeNull();
        doc.User.Username.Should().Be("TestUser");
    }
}

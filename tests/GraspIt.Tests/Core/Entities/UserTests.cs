using GraspIt.Core.Entities;
using FluentAssertions;

namespace GraspIt.Tests.Core.Entities;

public class UserTests
{
    [Fact]
    public void User_DefaultValues_AreCorrect()
    {
        var user = new User();

        user.Id.Should().Be(0);
        user.Username.Should().BeEmpty();
        user.Email.Should().BeEmpty();
        user.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        user.Documents.Should().NotBeNull().And.BeEmpty();
        user.MockExams.Should().NotBeNull().And.BeEmpty();
    }

    [Fact]
    public void User_SetProperties_RetainsValues()
    {
        var user = new User
        {
            Id = 1,
            Username = "john_doe",
            Email = "john@example.com"
        };

        user.Id.Should().Be(1);
        user.Username.Should().Be("john_doe");
        user.Email.Should().Be("john@example.com");
    }

    [Fact]
    public void User_Documents_CollectionCanBePopulated()
    {
        var user = new User { Id = 1, Username = "testuser" };
        user.Documents.Add(new Document { Id = 1, Title = "doc1.pdf" });
        user.Documents.Add(new Document { Id = 2, Title = "doc2.pdf" });

        user.Documents.Should().HaveCount(2);
    }

    [Fact]
    public void User_MockExams_CollectionCanBePopulated()
    {
        var user = new User { Id = 1, Username = "testuser" };
        user.MockExams.Add(new MockExam { Id = 1, Title = "Exam 1" });

        user.MockExams.Should().HaveCount(1);
        user.MockExams.First().Title.Should().Be("Exam 1");
    }
}

using GraspIt.Core.Entities;
using FluentAssertions;

namespace GraspIt.Tests.Core.Entities;

public class MockExamTests
{
    [Fact]
    public void MockExam_DefaultValues_AreCorrect()
    {
        var exam = new MockExam();

        exam.Id.Should().Be(0);
        exam.Title.Should().BeEmpty();
        exam.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        exam.UserId.Should().Be(0);
        exam.Questions.Should().NotBeNull().And.BeEmpty();
    }

    [Fact]
    public void MockExam_SetProperties_RetainsValues()
    {
        var now = DateTime.UtcNow;
        var exam = new MockExam
        {
            Id = 10,
            Title = "Chapter 3 Mock Test",
            CreatedAt = now,
            UserId = 5
        };

        exam.Id.Should().Be(10);
        exam.Title.Should().Be("Chapter 3 Mock Test");
        exam.CreatedAt.Should().Be(now);
        exam.UserId.Should().Be(5);
    }

    [Fact]
    public void MockExam_Questions_CollectionCanBePopulated()
    {
        var exam = new MockExam { Id = 1, Title = "Test Exam" };
        exam.Questions.Add(new Question { Id = 1, Text = "What is AI?" });
        exam.Questions.Add(new Question { Id = 2, Text = "Define ML" });
        exam.Questions.Add(new Question { Id = 3, Text = "What is NLP?" });

        exam.Questions.Should().HaveCount(3);
    }

    [Fact]
    public void MockExam_NavigationProperty_User_CanBeSet()
    {
        var user = new User { Id = 1, Username = "student1" };
        var exam = new MockExam { UserId = 1, User = user };

        exam.User.Should().NotBeNull();
        exam.User.Username.Should().Be("student1");
    }
}

using FluentAssertions;
using GraspIt.Application.Features.Exams.Queries;

namespace GraspIt.Tests.Application.Features.Exams;

public class GenerateMockExamQueryTests
{
    [Fact]
    public void GenerateMockExamQuery_DefaultValues_AreCorrect()
    {
        var query = new GenerateMockExamQuery();

        query.UserId.Should().Be(0);
        query.Topic.Should().BeEmpty();
        query.MockStyle.Should().Be("Standard");
        query.FocusAreas.Should().BeEmpty();
        query.ProviderId.Should().BeNull();
    }

    [Fact]
    public void GenerateMockExamQuery_SetProperties_RetainsValues()
    {
        var query = new GenerateMockExamQuery
        {
            UserId = 3,
            Topic = "Computer Networks",
            MockStyle = "Clone Previous Question Papers",
            FocusAreas = "Focus on Chapter 5 definitions",
            ProviderId = 2
        };

        query.UserId.Should().Be(3);
        query.Topic.Should().Be("Computer Networks");
        query.MockStyle.Should().Be("Clone Previous Question Papers");
        query.FocusAreas.Should().Be("Focus on Chapter 5 definitions");
        query.ProviderId.Should().Be(2);
    }

    [Theory]
    [InlineData("Clone Previous Question Papers")]
    [InlineData("Standard Multiple Choice")]
    [InlineData("Short Answer / Essay")]
    [InlineData("Mix match of everything")]
    [InlineData("Standard")]
    public void GenerateMockExamQuery_MockStyle_AcceptsAllValidStyles(string style)
    {
        var query = new GenerateMockExamQuery { MockStyle = style };
        query.MockStyle.Should().Be(style);
    }

    [Fact]
    public void GenerateMockExamQuery_ProviderId_CanBeNull()
    {
        var query = new GenerateMockExamQuery { ProviderId = null };
        query.ProviderId.Should().BeNull();
    }
}

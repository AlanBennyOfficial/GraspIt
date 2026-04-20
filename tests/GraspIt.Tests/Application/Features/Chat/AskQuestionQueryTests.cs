using FluentAssertions;
using GraspIt.Application.Features.Chat.Queries;

namespace GraspIt.Tests.Application.Features.Chat;

public class AskQuestionQueryTests
{
    [Fact]
    public void AskQuestionQuery_DefaultValues_AreCorrect()
    {
        var query = new AskQuestionQuery();

        query.Query.Should().BeEmpty();
        query.ProviderId.Should().BeNull();
    }

    [Fact]
    public void AskQuestionQuery_SetProperties_RetainsValues()
    {
        var query = new AskQuestionQuery
        {
            Query = "What is polymorphism?",
            ProviderId = 3
        };

        query.Query.Should().Be("What is polymorphism?");
        query.ProviderId.Should().Be(3);
    }

    [Fact]
    public void AskQuestionQuery_ProviderId_CanBeNull()
    {
        var query = new AskQuestionQuery { Query = "test", ProviderId = null };
        query.ProviderId.Should().BeNull();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("A very long question that spans multiple words to simulate a realistic user query about computer science topics")]
    public void AskQuestionQuery_Query_AcceptsVariousInputs(string input)
    {
        var query = new AskQuestionQuery { Query = input };
        query.Query.Should().Be(input);
    }
}

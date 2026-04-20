using GraspIt.Core.Entities;
using FluentAssertions;

namespace GraspIt.Tests.Core.Entities;

public class QuestionTests
{
    [Fact]
    public void Question_DefaultValues_AreCorrect()
    {
        var question = new Question();

        question.Id.Should().Be(0);
        question.Text.Should().BeEmpty();
        question.Type.Should().Be("MCQ");
        question.OptionsJson.Should().BeNull();
        question.CorrectAnswer.Should().BeEmpty();
        question.Explanation.Should().BeEmpty();
        question.MockExamId.Should().Be(0);
    }

    [Fact]
    public void Question_SetProperties_RetainsValues()
    {
        var question = new Question
        {
            Id = 1,
            Text = "What is the OSI model?",
            Type = "ShortAnswer",
            OptionsJson = null,
            CorrectAnswer = "A conceptual framework...",
            Explanation = "The OSI model is a 7-layer...",
            MockExamId = 10
        };

        question.Id.Should().Be(1);
        question.Text.Should().Be("What is the OSI model?");
        question.Type.Should().Be("ShortAnswer");
        question.OptionsJson.Should().BeNull();
        question.CorrectAnswer.Should().Contain("conceptual framework");
        question.Explanation.Should().Contain("7-layer");
        question.MockExamId.Should().Be(10);
    }

    [Fact]
    public void Question_MCQ_OptionsJson_CanStoreSerializedOptions()
    {
        var optionsJson = "[\"Option A\",\"Option B\",\"Option C\",\"Option D\"]";
        var question = new Question
        {
            Text = "Which layer handles routing?",
            Type = "MCQ",
            OptionsJson = optionsJson,
            CorrectAnswer = "Option C"
        };

        question.OptionsJson.Should().NotBeNullOrEmpty();
        question.OptionsJson.Should().Contain("Option A");
        question.OptionsJson.Should().Contain("Option D");
    }

    [Theory]
    [InlineData("MCQ")]
    [InlineData("ShortAnswer")]
    [InlineData("Essay")]
    [InlineData("TrueFalse")]
    public void Question_Type_AcceptsVariousValues(string type)
    {
        var question = new Question { Type = type };
        question.Type.Should().Be(type);
    }

    [Fact]
    public void Question_NavigationProperty_MockExam_CanBeSet()
    {
        var exam = new MockExam { Id = 5, Title = "Unit Test Exam" };
        var question = new Question { MockExamId = 5, MockExam = exam };

        question.MockExam.Should().NotBeNull();
        question.MockExam.Title.Should().Be("Unit Test Exam");
    }
}

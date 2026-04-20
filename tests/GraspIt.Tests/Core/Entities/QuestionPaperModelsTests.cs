using FluentAssertions;
using GraspIt.Core.Entities;
using System.Collections.Generic;
using Xunit;

namespace GraspIt.Tests.Core.Entities;

public class QuestionPaperModelsTests
{
    [Fact]
    public void QuestionPaperData_Initialization_SetsDefaultValues()
    {
        var data = new QuestionPaperData();

        data.Code.Should().Be(string.Empty);
        data.Title.Should().Be(string.Empty);
        data.Semester.Should().Be(string.Empty);
        data.ExamNo.Should().Be(string.Empty);
        data.Duration.Should().Be(string.Empty);
        data.Date.Should().Be(string.Empty);
        data.Time.Should().Be(string.Empty);
        data.MaxMarks.Should().Be(string.Empty);
        data.Note.Should().Be(string.Empty);
        data.Footer.Should().Be(string.Empty);
        data.Units.Should().NotBeNull().And.BeEmpty();
    }

    [Fact]
    public void QuestionPaperData_Properties_CanBeSet()
    {
        var units = new List<QuestionPaperUnit> { new QuestionPaperUnit() };
        var data = new QuestionPaperData
        {
            Code = "CS101",
            Title = "Intro to CS",
            Semester = "1",
            ExamNo = "E1",
            Duration = "3 Hrs",
            Date = "2024-01-01",
            Time = "10 AM",
            MaxMarks = "100",
            Note = "Answer all",
            Footer = "Page 1",
            Units = units
        };

        data.Code.Should().Be("CS101");
        data.Title.Should().Be("Intro to CS");
        data.Semester.Should().Be("1");
        data.ExamNo.Should().Be("E1");
        data.Duration.Should().Be("3 Hrs");
        data.Date.Should().Be("2024-01-01");
        data.Time.Should().Be("10 AM");
        data.MaxMarks.Should().Be("100");
        data.Note.Should().Be("Answer all");
        data.Footer.Should().Be("Page 1");
        data.Units.Should().BeEquivalentTo(units);
    }

    [Fact]
    public void QuestionPaperUnit_Initialization_SetsDefaultValues()
    {
        var unit = new QuestionPaperUnit();
        unit.Name.Should().Be(string.Empty);
        unit.Questions.Should().NotBeNull().And.BeEmpty();
    }

    [Fact]
    public void QuestionPaperUnit_Properties_CanBeSet()
    {
        var questions = new List<QuestionPaperQuestion> { new QuestionPaperQuestion() };
        var unit = new QuestionPaperUnit
        {
            Name = "Unit 1",
            Questions = questions
        };

        unit.Name.Should().Be("Unit 1");
        unit.Questions.Should().BeEquivalentTo(questions);
    }

    [Fact]
    public void QuestionPaperQuestion_Initialization_SetsDefaultValues()
    {
        var question = new QuestionPaperQuestion();
        question.No.Should().Be(string.Empty);
        question.Parts.Should().NotBeNull().And.BeEmpty();
    }

    [Fact]
    public void QuestionPaperQuestion_Properties_CanBeSet()
    {
        var parts = new List<QuestionPaperPart> { new QuestionPaperPart() };
        var question = new QuestionPaperQuestion
        {
            No = "1",
            Parts = parts
        };

        question.No.Should().Be("1");
        question.Parts.Should().BeEquivalentTo(parts);
    }

    [Fact]
    public void QuestionPaperPart_Initialization_SetsDefaultValues()
    {
        var part = new QuestionPaperPart();
        part.Sub.Should().Be(string.Empty);
        part.Text.Should().Be(string.Empty);
        part.Marks.Should().Be(string.Empty);
        part.Bt.Should().Be(string.Empty);
        part.Co.Should().Be(string.Empty);
    }

    [Fact]
    public void QuestionPaperPart_Properties_CanBeSet()
    {
        var part = new QuestionPaperPart
        {
            Sub = "a",
            Text = "What is C#?",
            Marks = "5",
            Bt = "L1",
            Co = "CO1"
        };

        part.Sub.Should().Be("a");
        part.Text.Should().Be("What is C#?");
        part.Marks.Should().Be("5");
        part.Bt.Should().Be("L1");
        part.Co.Should().Be("CO1");
    }
}

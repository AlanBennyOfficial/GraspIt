using FluentAssertions;
using GraspIt.Infrastructure.Services;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;

namespace GraspIt.Tests.Infrastructure.Services;

public class ExamRendererServiceTests
{
    private readonly ExamRendererService _service;

    public ExamRendererServiceTests()
    {
        _service = new ExamRendererService();
    }

    [Fact]
    public async Task RenderExamToDocxAsync_WithStructure_ReturnsBytes()
    {
        var structure = new ExamPaperStructure
        {
            HeaderLines = new List<string> { "University Name", "Department", "Subject" },
            TableColumns = new List<string> { "Q No", "Question", "Marks", "BT", "CO" },
            Sections = new List<ExamSection>
            {
                new ExamSection
                {
                    Heading = "Section A",
                    SubHeading = "Answer all",
                    Questions = new List<ExamQuestion>
                    {
                        new ExamQuestion 
                        { 
                            QNo = "1", 
                            Text = "What is X?", 
                            Marks = "10", 
                            BT = "L1", 
                            CO = "CO1",
                            SubParts = new List<ExamQuestion>
                            {
                                new ExamQuestion { QNo = "a", Text = "Define X", Marks = "5" }
                            }
                        }
                    }
                }
            }
        };

        var result = await _service.RenderExamToDocxAsync(structure);

        result.Should().NotBeNull();
        result.Length.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task RenderExamToDocxAsync_WithFlatList_ReturnsBytes()
    {
        var questions = new List<QuestionRow>
        {
            new QuestionRow { QNo = "1", Question = "Q1", Marks = "5", BT = "L1", CO = "CO1" }
        };

        var result = await _service.RenderExamToDocxAsync(questions);

        result.Should().NotBeNull();
        result.Length.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task RenderExamToDocxAsync_WithEmptyStructure_ReturnsBytes()
    {
        var structure = new ExamPaperStructure();
        var result = await _service.RenderExamToDocxAsync(structure);
        
        result.Should().NotBeNull();
    }
}

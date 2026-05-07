using FluentAssertions;
using GraspIt.Core.Entities;
using GraspIt.Core.Interfaces;
using GraspIt.Infrastructure.Data;
using GraspIt.Infrastructure.Services;
using GraspIt.Web.Controllers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Moq;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Xunit;

namespace GraspIt.Tests.Web.Controllers;

public class MockExamControllerTests
{
    private readonly Mock<IDocumentExtractionService> _ocrMock;
    private readonly Mock<IRagRetrievalService> _ragMock;
    private readonly Mock<ILLMProvider> _llmMock;
    private readonly Mock<IExamRendererService> _rendererMock;

    public MockExamControllerTests()
    {
        _ocrMock = new Mock<IDocumentExtractionService>();
        _ragMock = new Mock<IRagRetrievalService>();
        _llmMock = new Mock<ILLMProvider>();
        _rendererMock = new Mock<IExamRendererService>();
    }

    private GraspItDbContext GetDbContext()
    {
        var options = new DbContextOptionsBuilder<GraspItDbContext>()
            .UseInMemoryDatabase(databaseName: System.Guid.NewGuid().ToString())
            .Options;
        return new GraspItDbContext(options);
    }

    [Fact]
    public async Task Index_ReturnsViewResult()
    {
        var context = GetDbContext();
        var controller = new MockExamController(_ocrMock.Object, _ragMock.Object, _llmMock.Object, _rendererMock.Object, context);

        var result = await controller.Index();

        Assert.IsType<ViewResult>(result);
    }

    [Fact]
    public async Task GenerateExam_NullFile_ReturnsErrorJson()
    {
        var controller = new MockExamController(_ocrMock.Object, _ragMock.Object, _llmMock.Object, _rendererMock.Object, GetDbContext());

        var result = await controller.GenerateExam(null!, "instructions");

        var json = Assert.IsType<JsonResult>(result);
        json.Value.Should().NotBeNull();
        json.Value!.ToString().Should().Contain("reference paper");
    }

    [Fact]
    public async Task GenerateExam_NullInstructions_ReturnsErrorJson()
    {
        var controller = new MockExamController(_ocrMock.Object, _ragMock.Object, _llmMock.Object, _rendererMock.Object, GetDbContext());
        var fileMock = new Mock<IFormFile>();
        fileMock.Setup(f => f.Length).Returns(100);

        var result = await controller.GenerateExam(fileMock.Object, "");

        var json = Assert.IsType<JsonResult>(result);
        json.Value.Should().NotBeNull();
        json.Value!.ToString().Should().Contain("Generation instructions");
    }

    [Fact]
    public async Task GenerateExam_StructuredFallback_ReturnsJson()
    {
        var controller = new MockExamController(_ocrMock.Object, _ragMock.Object, _llmMock.Object, _rendererMock.Object, GetDbContext());
        
        var fileMock = new Mock<IFormFile>();
        fileMock.Setup(f => f.Length).Returns(100);

        _ocrMock.Setup(x => x.ExtractStructuredAsync(It.IsAny<IFormFile>()))
            .ReturnsAsync(new ExamPaperStructure { Sections = new List<ExamSection>() });
        _ocrMock.Setup(x => x.ExtractExamTableAsync(It.IsAny<IFormFile>())).ReturnsAsync("table content");
        _ragMock.Setup(x => x.GetRelevantSyllabusContextAsync(It.IsAny<string>(), It.IsAny<int>())).ReturnsAsync("context");
        
        _llmMock.Setup(x => x.GenerateStructuredExamAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync((QuestionPaperData?)null); // Fallback to classic

        _llmMock.Setup(x => x.GenerateExamQuestionsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(new List<QuestionRow> { new QuestionRow { QNo = "1", Question = "Test?", Marks = "5" } });

        var result = await controller.GenerateExam(fileMock.Object, "Generate it");

        var json = Assert.IsType<JsonResult>(result);
        json.Value.Should().NotBeNull();
        json.Value!.ToString().Should().Contain("markdown");
        json.Value.ToString().Should().Contain("1");
    }

    [Fact]
    public async Task GenerateExam_LlmFailure_ReturnsError()
    {
        var controller = new MockExamController(_ocrMock.Object, _ragMock.Object, _llmMock.Object, _rendererMock.Object, GetDbContext());
        var fileMock = new Mock<IFormFile>();
        fileMock.Setup(f => f.Length).Returns(100);

        _ocrMock.Setup(x => x.ExtractStructuredAsync(It.IsAny<IFormFile>())).ReturnsAsync(new ExamPaperStructure());
        _ocrMock.Setup(x => x.ExtractExamTableAsync(It.IsAny<IFormFile>())).ReturnsAsync("table content");
        
        _llmMock.Setup(x => x.GenerateStructuredExamAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync((QuestionPaperData?)null);
        _llmMock.Setup(x => x.GenerateExamQuestionsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(new List<QuestionRow>()); // Empty

        var result = await controller.GenerateExam(fileMock.Object, "Generate it");

        var json = Assert.IsType<JsonResult>(result);
        json.Value.Should().NotBeNull();
        json.Value!.ToString().Should().Contain("could not generate questions");
    }

    [Fact]
    public async Task GenerateExam_WithOrBlocks_HandlesCorrectly()
    {
        var controller = new MockExamController(_ocrMock.Object, _ragMock.Object, _llmMock.Object, _rendererMock.Object, GetDbContext());
        var fileMock = new Mock<IFormFile>();
        fileMock.Setup(f => f.Length).Returns(100);

        _ocrMock.Setup(x => x.ExtractStructuredAsync(It.IsAny<IFormFile>())).ReturnsAsync(new ExamPaperStructure());
        _ocrMock.Setup(x => x.ExtractExamTableAsync(It.IsAny<IFormFile>())).ReturnsAsync("table content");
        
        var paper = new QuestionPaperData 
        { 
            Code = "C1", 
            Units = new List<QuestionPaperUnit> { 
                new QuestionPaperUnit { 
                    Name = "U1",
                    Questions = new List<QuestionPaperQuestion> {
                        new QuestionPaperQuestion { No = "OR" }
                    }
                },
                new QuestionPaperUnit { Name = "OR" }
            } 
        };
        _llmMock.Setup(x => x.GenerateStructuredExamAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(paper);

        var result = await controller.GenerateExam(fileMock.Object, "Generate it");

        var json = Assert.IsType<JsonResult>(result);
        json.Value.Should().NotBeNull();
        json.Value!.ToString().Should().Contain("OR");
    }
}

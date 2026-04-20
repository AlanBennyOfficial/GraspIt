using FluentAssertions;
using GraspIt.Core.Entities;
using GraspIt.Core.Interfaces;
using GraspIt.Infrastructure.Services;
using GraspIt.Web.Controllers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace GraspIt.Tests.Web.Controllers;

public class ExportControllerTests
{
    private readonly Mock<IExportService> _exportServiceMock;
    private readonly Mock<IQuestionPaperGeneratorService> _qpGeneratorMock;
    private readonly ExportController _controller;

    public ExportControllerTests()
    {
        _exportServiceMock = new Mock<IExportService>();
        _qpGeneratorMock = new Mock<IQuestionPaperGeneratorService>();
        _controller = new ExportController(_exportServiceMock.Object, _qpGeneratorMock.Object);
        
        // Mock HttpContext for Request.Body
        var context = new DefaultHttpContext();
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = context
        };
    }

    private void SetRequestBody(string json)
    {
        var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));
        _controller.Request.Body = stream;
        _controller.Request.ContentLength = stream.Length;
    }

    [Fact]
    public async Task DownloadDocx_ReturnsFile()
    {
        _exportServiceMock.Setup(x => x.ToDocxAsync(It.IsAny<string>()))
            .ReturnsAsync(new byte[] { 1, 2, 3 });

        var result = await _controller.DownloadDocx("content");

        result.Should().BeOfType<FileContentResult>();
    }

    [Fact]
    public async Task DownloadExamDocx_WithValidJson_ReturnsFile()
    {
        var json = "{\"code\":\"CS101\", \"units\": []}";
        SetRequestBody(json);
        _qpGeneratorMock.Setup(x => x.GenerateDocxAsync(It.IsAny<QuestionPaperData>()))
            .ReturnsAsync(new byte[] { 1, 2, 3 });

        var result = await _controller.DownloadExamDocx();

        result.Should().BeOfType<FileContentResult>();
    }

    [Fact]
    public async Task DownloadExamDocx_WithInvalidJson_ReturnsBadRequest()
    {
        SetRequestBody("");
        var result = await _controller.DownloadExamDocx();
        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task DownloadExamPdf_ReturnsFile()
    {
        SetRequestBody("{\"code\":\"CS101\"}");
        _qpGeneratorMock.Setup(x => x.GeneratePdfAsync(It.IsAny<QuestionPaperData>()))
            .ReturnsAsync(new byte[] { 1, 2, 3 });

        var result = await _controller.DownloadExamPdf();
        result.Should().BeOfType<FileContentResult>();
    }

    [Fact]
    public async Task DownloadExamMarkdown_ReturnsFile()
    {
        SetRequestBody("{\"code\":\"CS101\"}");
        _qpGeneratorMock.Setup(x => x.GenerateMarkdown(It.IsAny<QuestionPaperData>()))
            .Returns(new byte[] { 1, 2, 3 });

        var result = await _controller.DownloadExamMarkdown();
        result.Should().BeOfType<FileContentResult>();
    }

    [Fact]
    public async Task DownloadExamLatex_ReturnsFile()
    {
        SetRequestBody("{\"code\":\"CS101\"}");
        _qpGeneratorMock.Setup(x => x.GenerateLatex(It.IsAny<QuestionPaperData>()))
            .Returns(new byte[] { 1, 2, 3 });

        var result = await _controller.DownloadExamLatex();
        result.Should().BeOfType<FileContentResult>();
    }
}

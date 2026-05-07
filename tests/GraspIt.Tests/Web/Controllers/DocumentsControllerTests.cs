using FluentAssertions;
using GraspIt.Application.Features.Documents.Commands;
using GraspIt.Core.Entities;
using GraspIt.Infrastructure.Data;
using GraspIt.Web.Controllers;
using Hangfire;
using Hangfire.Common;
using Hangfire.States;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace GraspIt.Tests.Web.Controllers;

public class DocumentsControllerTests
{
    private readonly Mock<IBackgroundJobClient> _backgroundJobClientMock;
    private readonly Mock<ILogger<DocumentsController>> _loggerMock;

    public DocumentsControllerTests()
    {
        _backgroundJobClientMock = new Mock<IBackgroundJobClient>();
        _loggerMock = new Mock<ILogger<DocumentsController>>();
    }

    private GraspItDbContext GetDbContext()
    {
        var options = new DbContextOptionsBuilder<GraspItDbContext>()
            .UseInMemoryDatabase(databaseName: System.Guid.NewGuid().ToString())
            .Options;
        return new GraspItDbContext(options);
    }

    [Fact]
    public async Task Index_ReturnsViewResult_WithListOfDocuments()
    {
        var context = GetDbContext();
        context.Documents.Add(new Document { Id = 1, Title = "Doc1" });
        await context.SaveChangesAsync();
        var controller = new DocumentsController(_backgroundJobClientMock.Object, context, _loggerMock.Object);

        var result = await controller.Index();

        var viewResult = Assert.IsType<ViewResult>(result);
        var model = Assert.IsAssignableFrom<List<Document>>(viewResult.ViewData.Model);
        model.Should().HaveCount(1);
    }

    [Fact]
    public async Task Upload_NullFile_ReturnsRedirectWithTempDataError()
    {
        var controller = new DocumentsController(_backgroundJobClientMock.Object, GetDbContext(), _loggerMock.Object);
        var tempData = new TempDataDictionary(new DefaultHttpContext(), Mock.Of<ITempDataProvider>());
        controller.TempData = tempData;

        var result = await controller.Upload(null!);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        redirect.ActionName.Should().Be("Index");
        controller.TempData["Error"].Should().Be("Please select a file to upload.");
    }

    [Fact]
    public async Task Upload_ValidFile_EnqueuesJobAndRedirects()
    {
        var context = GetDbContext();
        var controller = new DocumentsController(_backgroundJobClientMock.Object, context, _loggerMock.Object);
        var tempData = new TempDataDictionary(new DefaultHttpContext(), Mock.Of<ITempDataProvider>());
        controller.TempData = tempData;

        var fileMock = new Mock<IFormFile>();
        var content = "dummy file content";
        var fileName = "test.pdf";
        var ms = new MemoryStream();
        var writer = new StreamWriter(ms);
        writer.Write(content);
        writer.Flush();
        ms.Position = 0;

        fileMock.Setup(f => f.OpenReadStream()).Returns(ms);
        fileMock.Setup(f => f.FileName).Returns(fileName);
        fileMock.Setup(f => f.Length).Returns(ms.Length);
        fileMock.Setup(f => f.CopyToAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask); // Simulating copy

        var result = await controller.Upload(fileMock.Object, "TestType");

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        redirect.ActionName.Should().Be("Index");
        controller.TempData["Success"].Should().NotBeNull();
        controller.TempData["Success"]!.ToString().Should().Contain("scheduled for AI embedding");

        _backgroundJobClientMock.Verify(x => x.Create(
            It.Is<Job>(job => job.Method.Name == "Handle" && job.Type == typeof(ProcessDocumentCommandHandler)),
            It.IsAny<EnqueuedState>()));
    }

    [Fact]
    public async Task Delete_ValidId_RemovesDocumentAndRedirects()
    {
        var context = GetDbContext();
        context.Documents.Add(new Document { Id = 1, Title = "Doc1", FilePath = "fake.pdf" });
        await context.SaveChangesAsync();
        var controller = new DocumentsController(_backgroundJobClientMock.Object, context, _loggerMock.Object);
        var tempData = new TempDataDictionary(new DefaultHttpContext(), Mock.Of<ITempDataProvider>());
        controller.TempData = tempData;

        var result = await controller.Delete(1);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        redirect.ActionName.Should().Be("Index");
        controller.TempData["Success"].Should().Be("'Doc1' has been deleted.");

        var docs = await context.Documents.ToListAsync();
        docs.Should().BeEmpty();
    }
}

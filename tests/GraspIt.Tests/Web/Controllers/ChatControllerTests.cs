using AutoMapper;
using FluentAssertions;
using GraspIt.Application.DTOs;
using GraspIt.Application.Features.Chat.Queries;
using GraspIt.Core.Entities;
using GraspIt.Infrastructure.Data;
using GraspIt.Web.Controllers;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Moq;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace GraspIt.Tests.Web.Controllers;

public class ChatControllerTests
{
    private readonly Mock<IMediator> _mediatorMock;
    private readonly Mock<IMapper> _mapperMock;

    public ChatControllerTests()
    {
        _mediatorMock = new Mock<IMediator>();
        _mapperMock = new Mock<IMapper>();
    }

    private GraspItDbContext GetDbContext()
    {
        var options = new DbContextOptionsBuilder<GraspItDbContext>()
            .UseInMemoryDatabase(databaseName: System.Guid.NewGuid().ToString())
            .Options;
        return new GraspItDbContext(options);
    }

    [Fact]
    public void Index_ReturnsViewResult()
    {
        var context = GetDbContext();
        var controller = new ChatController(_mediatorMock.Object, context, _mapperMock.Object);

        var result = controller.Index();

        var viewResult = Assert.IsType<ViewResult>(result);
    }

    [Fact]
    public async Task AskQuestion_EmptyQuery_ReturnsBadRequest()
    {
        var controller = new ChatController(_mediatorMock.Object, GetDbContext(), _mapperMock.Object);
        var req = new ChatRequest { Query = "" };

        var result = await controller.AskQuestion(req);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        badRequest.Value.Should().Be("Query cannot be empty.");
    }

    [Fact]
    public async Task AskQuestion_CreatesSession_IfNoneProvided()
    {
        var context = GetDbContext();
        var controller = new ChatController(_mediatorMock.Object, context, _mapperMock.Object);
        var req = new ChatRequest { Query = "Hello World" };

        _mediatorMock.Setup(m => m.Send(It.IsAny<AskQuestionQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("Hi there!");

        var result = await controller.AskQuestion(req);

        var jsonResult = Assert.IsType<JsonResult>(result);
        var session = await context.ChatSessions.FirstOrDefaultAsync();
        session.Should().NotBeNull();
        session.Title.Should().Be("Hello World");
        
        var messages = await context.ChatMessages.ToListAsync();
        messages.Should().HaveCount(2); // user and assistant
        messages[0].Role.Should().Be("user");
        messages[1].Role.Should().Be("assistant");
        messages[1].Content.Should().Be("Hi there!");
    }

    [Fact]
    public async Task GetSessions_ReturnsJsonWithSessions()
    {
        var context = GetDbContext();
        context.ChatSessions.Add(new ChatSession { Id = 1, UserId = 1, Title = "Test" });
        await context.SaveChangesAsync();
        var controller = new ChatController(_mediatorMock.Object, context, _mapperMock.Object);

        var result = await controller.GetSessions();

        var jsonResult = Assert.IsType<JsonResult>(result);
    }

    [Fact]
    public async Task GetMessages_ReturnsJsonWithMessages()
    {
        var context = GetDbContext();
        context.ChatMessages.Add(new ChatMessage { Id = 1, SessionId = 5, Role = "user" });
        await context.SaveChangesAsync();
        var controller = new ChatController(_mediatorMock.Object, context, _mapperMock.Object);

        var result = await controller.GetMessages(5);

        var jsonResult = Assert.IsType<JsonResult>(result);
    }

    [Fact]
    public async Task RenameSession_Valid_UpdatesTitle()
    {
        var context = GetDbContext();
        context.ChatSessions.Add(new ChatSession { Id = 1, Title = "Old Title" });
        await context.SaveChangesAsync();
        var controller = new ChatController(_mediatorMock.Object, context, _mapperMock.Object);

        var result = await controller.RenameSession(new RenameRequest { SessionId = 1, Title = "New Title" });

        Assert.IsType<OkResult>(result);
        var updated = await context.ChatSessions.FindAsync(1);
        updated.Title.Should().Be("New Title");
    }

    [Fact]
    public async Task DeleteSession_Valid_RemovesSession()
    {
        var context = GetDbContext();
        context.ChatSessions.Add(new ChatSession { Id = 1 });
        await context.SaveChangesAsync();
        var controller = new ChatController(_mediatorMock.Object, context, _mapperMock.Object);

        var result = await controller.DeleteSession(new IdRequest { Id = 1 });

        Assert.IsType<OkResult>(result);
        var count = await context.ChatSessions.CountAsync();
        count.Should().Be(0);
    }

    [Fact]
    public async Task MoveSession_Valid_UpdatesFolder()
    {
        var context = GetDbContext();
        context.ChatSessions.Add(new ChatSession { Id = 1 });
        await context.SaveChangesAsync();
        var controller = new ChatController(_mediatorMock.Object, context, _mapperMock.Object);

        var result = await controller.MoveSession(new MoveSessionRequest { SessionId = 1, FolderId = 10 });

        Assert.IsType<OkResult>(result);
        var s = await context.ChatSessions.FindAsync(1);
        s.FolderId.Should().Be(10);
    }

    [Fact]
    public async Task GetFolders_ReturnsJson()
    {
        var context = GetDbContext();
        context.ChatFolders.Add(new ChatFolder { Id = 1, UserId = 1, Name = "Folder1" });
        await context.SaveChangesAsync();
        var controller = new ChatController(_mediatorMock.Object, context, _mapperMock.Object);

        var result = await controller.GetFolders();
        Assert.IsType<JsonResult>(result);
    }

    [Fact]
    public async Task CreateFolder_AddsFolder()
    {
        var context = GetDbContext();
        var controller = new ChatController(_mediatorMock.Object, context, _mapperMock.Object);

        var result = await controller.CreateFolder(new NameRequest { Name = "NewFolder" });

        Assert.IsType<JsonResult>(result);
        var count = await context.ChatFolders.CountAsync();
        count.Should().Be(1);
    }

    [Fact]
    public async Task DeleteFolder_RemovesFolder()
    {
        var context = GetDbContext();
        context.ChatFolders.Add(new ChatFolder { Id = 1 });
        await context.SaveChangesAsync();
        var controller = new ChatController(_mediatorMock.Object, context, _mapperMock.Object);

        var result = await controller.DeleteFolder(new IdRequest { Id = 1 });

        Assert.IsType<OkResult>(result);
        var count = await context.ChatFolders.CountAsync();
        count.Should().Be(0);
    }

    [Fact]
    public async Task GetDocuments_ReturnsJson()
    {
        var context = GetDbContext();
        var controller = new ChatController(_mediatorMock.Object, context, _mapperMock.Object);

        var result = await controller.GetDocuments();
        Assert.IsType<JsonResult>(result);
    }
}

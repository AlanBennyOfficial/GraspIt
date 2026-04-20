using FluentAssertions;
using GraspIt.Application.Features.Exams.Queries;
using GraspIt.Core.Interfaces;
using Moq;

namespace GraspIt.Tests.Application.Features.Exams;

public class GenerateMockExamQueryHandlerTests
{
    private readonly Mock<IVectorDbService> _vectorDbMock;
    private readonly Mock<IAiService> _aiServiceMock;
    private readonly GenerateMockExamQueryHandler _handler;

    public GenerateMockExamQueryHandlerTests()
    {
        _vectorDbMock = new Mock<IVectorDbService>();
        _aiServiceMock = new Mock<IAiService>();
        _handler = new GenerateMockExamQueryHandler(
            _vectorDbMock.Object,
            _aiServiceMock.Object
        );
    }

    // ---- Happy Path ----

    [Fact]
    public async Task Handle_ValidQuery_ReturnsExamContent()
    {
        // Arrange
        _vectorDbMock.Setup(v => v.SearchAsync("Computer Networks", 10))
            .ReturnsAsync("Context about networking...");
        _aiServiceMock.Setup(a => a.GenerateMockExamAsync(
            "Context about networking...", "Standard", "", null))
            .ReturnsAsync("## Exam\n1. What is TCP?\n2. What is UDP?");

        var query = new GenerateMockExamQuery { Topic = "Computer Networks" };

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().Contain("Exam");
        result.Should().Contain("TCP");
    }

    // ---- Vector DB is called with topK = 10 ----

    [Fact]
    public async Task Handle_AlwaysSearchesVectorDbWithTopK10()
    {
        // Arrange
        _vectorDbMock.Setup(v => v.SearchAsync(It.IsAny<string>(), 10)).ReturnsAsync("ctx");
        _aiServiceMock.Setup(a => a.GenerateMockExamAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int?>()))
            .ReturnsAsync("exam");

        var query = new GenerateMockExamQuery { Topic = "ML" };

        // Act
        await _handler.Handle(query, CancellationToken.None);

        // Assert
        _vectorDbMock.Verify(v => v.SearchAsync("ML", 10), Times.Once);
    }

    // ---- MockStyle Passthrough ----

    [Theory]
    [InlineData("Clone Previous Question Papers")]
    [InlineData("Standard Multiple Choice")]
    [InlineData("Short Answer / Essay")]
    [InlineData("Mix match of everything")]
    public async Task Handle_PassesMockStyleToAiService(string style)
    {
        // Arrange
        _vectorDbMock.Setup(v => v.SearchAsync(It.IsAny<string>(), 10)).ReturnsAsync("ctx");
        _aiServiceMock.Setup(a => a.GenerateMockExamAsync(
            It.IsAny<string>(), style, It.IsAny<string>(), It.IsAny<int?>()))
            .ReturnsAsync("exam content");

        var query = new GenerateMockExamQuery { Topic = "Algo", MockStyle = style };

        // Act
        await _handler.Handle(query, CancellationToken.None);

        // Assert
        _aiServiceMock.Verify(a => a.GenerateMockExamAsync(
            It.IsAny<string>(), style, It.IsAny<string>(), It.IsAny<int?>()), Times.Once);
    }

    // ---- FocusAreas Passthrough ----

    [Fact]
    public async Task Handle_PassesFocusAreasToAiService()
    {
        // Arrange
        var focusAreas = "Only Chapter 3 from Textbook";
        _vectorDbMock.Setup(v => v.SearchAsync(It.IsAny<string>(), 10)).ReturnsAsync("ctx");
        _aiServiceMock.Setup(a => a.GenerateMockExamAsync(
            It.IsAny<string>(), It.IsAny<string>(), focusAreas, It.IsAny<int?>()))
            .ReturnsAsync("focused exam");

        var query = new GenerateMockExamQuery
        {
            Topic = "Data Structures",
            FocusAreas = focusAreas
        };

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().Be("focused exam");
        _aiServiceMock.Verify(a => a.GenerateMockExamAsync(
            It.IsAny<string>(), It.IsAny<string>(), focusAreas, It.IsAny<int?>()), Times.Once);
    }

    [Fact]
    public async Task Handle_EmptyFocusAreas_PassesEmptyString()
    {
        // Arrange
        _vectorDbMock.Setup(v => v.SearchAsync(It.IsAny<string>(), 10)).ReturnsAsync("ctx");
        _aiServiceMock.Setup(a => a.GenerateMockExamAsync(
            It.IsAny<string>(), It.IsAny<string>(), "", It.IsAny<int?>()))
            .ReturnsAsync("general exam");

        var query = new GenerateMockExamQuery { Topic = "OS" };

        // Act
        await _handler.Handle(query, CancellationToken.None);

        // Assert
        _aiServiceMock.Verify(a => a.GenerateMockExamAsync(
            It.IsAny<string>(), It.IsAny<string>(), "", It.IsAny<int?>()), Times.Once);
    }

    // ---- Provider ID Passthrough ----

    [Fact]
    public async Task Handle_WithProviderId_PassesToAiService()
    {
        // Arrange
        _vectorDbMock.Setup(v => v.SearchAsync(It.IsAny<string>(), 10)).ReturnsAsync("ctx");
        _aiServiceMock.Setup(a => a.GenerateMockExamAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), 7))
            .ReturnsAsync("exam");

        var query = new GenerateMockExamQuery { Topic = "Math", ProviderId = 7 };

        // Act
        await _handler.Handle(query, CancellationToken.None);

        // Assert
        _aiServiceMock.Verify(a => a.GenerateMockExamAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), 7), Times.Once);
    }

    [Fact]
    public async Task Handle_WithoutProviderId_PassesNullToAiService()
    {
        // Arrange
        _vectorDbMock.Setup(v => v.SearchAsync(It.IsAny<string>(), 10)).ReturnsAsync("ctx");
        _aiServiceMock.Setup(a => a.GenerateMockExamAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), null))
            .ReturnsAsync("exam");

        var query = new GenerateMockExamQuery { Topic = "Physics" };

        // Act
        await _handler.Handle(query, CancellationToken.None);

        // Assert
        _aiServiceMock.Verify(a => a.GenerateMockExamAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), null), Times.Once);
    }

    // ---- Empty Context from VectorDb ----

    [Fact]
    public async Task Handle_EmptyVectorDbContext_StillCallsAiService()
    {
        // Arrange
        _vectorDbMock.Setup(v => v.SearchAsync(It.IsAny<string>(), 10)).ReturnsAsync(string.Empty);
        _aiServiceMock.Setup(a => a.GenerateMockExamAsync(
            string.Empty, It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int?>()))
            .ReturnsAsync("exam from nothing");

        var query = new GenerateMockExamQuery { Topic = "No docs yet" };

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().Be("exam from nothing");
    }

    // ---- All Parameters Forwarded Together ----

    [Fact]
    public async Task Handle_AllParametersForwardedCorrectly()
    {
        // Arrange
        _vectorDbMock.Setup(v => v.SearchAsync("Networking", 10)).ReturnsAsync("network context");
        _aiServiceMock.Setup(a => a.GenerateMockExamAsync(
            "network context", "Clone Previous Question Papers", "Only from Teacher Notes", 5))
            .ReturnsAsync("cloned exam");

        var query = new GenerateMockExamQuery
        {
            Topic = "Networking",
            MockStyle = "Clone Previous Question Papers",
            FocusAreas = "Only from Teacher Notes",
            ProviderId = 5,
            UserId = 1
        };

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().Be("cloned exam");
        _vectorDbMock.Verify(v => v.SearchAsync("Networking", 10), Times.Once);
        _aiServiceMock.Verify(a => a.GenerateMockExamAsync(
            "network context", "Clone Previous Question Papers", "Only from Teacher Notes", 5), Times.Once);
    }
}

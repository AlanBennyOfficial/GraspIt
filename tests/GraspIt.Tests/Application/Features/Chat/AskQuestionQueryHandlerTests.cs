using FluentAssertions;
using GraspIt.Application.Features.Chat.Queries;
using GraspIt.Core.Interfaces;
using Microsoft.Extensions.Caching.Memory;
using Moq;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace GraspIt.Tests.Application.Features.Chat;

public class AskQuestionQueryHandlerTests
{
    private readonly Mock<IVectorDbService> _vectorDbMock;
    private readonly Mock<IAiService> _aiServiceMock;
    private readonly IMemoryCache _cache;
    private readonly AskQuestionQueryHandler _handler;

    public AskQuestionQueryHandlerTests()
    {
        _vectorDbMock = new Mock<IVectorDbService>();
        _aiServiceMock = new Mock<IAiService>();
        _cache = new MemoryCache(new MemoryCacheOptions());
        _handler = new AskQuestionQueryHandler(_vectorDbMock.Object, _aiServiceMock.Object, _cache);
    }

    [Fact]
    public async Task Handle_UsesCache_WhenAvailable()
    {
        // Arrange
        var query = new AskQuestionQuery { Query = "test", ProviderId = 1, UseRag = true };
        string cacheKey = $"Chat_{query.Query.ToLowerInvariant()}_{query.ProviderId}_{query.UseRag}";
        _cache.Set(cacheKey, "Cached response");

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().Be("Cached response");
        _vectorDbMock.Verify(x => x.SearchAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<List<int>?>()), Times.Never);
        _aiServiceMock.Verify(x => x.GenerateResponseAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int?>()), Times.Never);
    }

    [Fact]
    public async Task Handle_CallsAiServiceAndCaches_WhenNotCached_WithRagEnabled_ContextFound()
    {
        // Arrange
        var query = new AskQuestionQuery { Query = "What is C#?", ProviderId = 1, UseRag = true };
        _vectorDbMock.Setup(x => x.SearchAsync("What is C#?", 5, null)).ReturnsAsync("C# is a programming language.");
        _aiServiceMock.Setup(x => x.GenerateResponseAsync(It.Is<string>(s => s.Contains("based on the provided context") && s.Contains("What is C#?")), "C# is a programming language.", 1))
            .ReturnsAsync("Generated response with context");

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().Be("Generated response with context");
        _vectorDbMock.Verify(x => x.SearchAsync("What is C#?", 5, null), Times.Once);
        _aiServiceMock.Verify(x => x.GenerateResponseAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int?>()), Times.Once);

        // Verify it was cached
        string cacheKey = $"Chat_{query.Query.ToLowerInvariant()}_{query.ProviderId}_{query.UseRag}";
        _cache.TryGetValue(cacheKey, out string? cachedResponse);
        cachedResponse.Should().Be("Generated response with context");
    }

    [Fact]
    public async Task Handle_CallsAiServiceAndCaches_WhenNotCached_WithRagEnabled_NoContextFound()
    {
        // Arrange
        var query = new AskQuestionQuery { Query = "What is C#?", ProviderId = 1, UseRag = true };
        _vectorDbMock.Setup(x => x.SearchAsync("What is C#?", 5, null)).ReturnsAsync(string.Empty);
        _aiServiceMock.Setup(x => x.GenerateResponseAsync(It.Is<string>(s => !s.Contains("based on the provided context") && s.Contains("What is C#?")), string.Empty, 1))
            .ReturnsAsync("Generated response without context (but RAG enabled)");

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().Be("Generated response without context (but RAG enabled)");
        _vectorDbMock.Verify(x => x.SearchAsync("What is C#?", 5, null), Times.Once);
        _aiServiceMock.Verify(x => x.GenerateResponseAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int?>()), Times.Once);
    }

    [Fact]
    public async Task Handle_CallsAiServiceAndCaches_WhenNotCached_WithRagDisabled()
    {
        // Arrange
        var query = new AskQuestionQuery { Query = "What is C#?", ProviderId = 1, UseRag = false, DocumentIds = new List<int> { 1, 2 } };
        // Rag disabled, shouldn't call SearchAsync
        _aiServiceMock.Setup(x => x.GenerateResponseAsync(It.Is<string>(s => !s.Contains("based on the provided context") && s.Contains("What is C#?")), string.Empty, 1))
            .ReturnsAsync("Generated response pure generation");

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().Be("Generated response pure generation");
        _vectorDbMock.Verify(x => x.SearchAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<List<int>?>()), Times.Never);
        _aiServiceMock.Verify(x => x.GenerateResponseAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int?>()), Times.Once);
    }
}

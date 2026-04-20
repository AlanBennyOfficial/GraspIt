using FluentAssertions;
using GraspIt.Core.Entities;
using GraspIt.Infrastructure.Services;
using Moq;
using OpenAI.Chat;
using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace GraspIt.Tests.Infrastructure.Services;

public class LocalOpenAIProviderTests
{
    private readonly Mock<ChatClient> _mockChatClient;
    private readonly LocalOpenAIProvider _provider;

    public LocalOpenAIProviderTests()
    {
        _mockChatClient = new Mock<ChatClient>();
        _provider = new LocalOpenAIProvider(_mockChatClient.Object);
    }

    [Fact]
    public async Task GenerateExamQuestionsAsync_WhenException_ReturnsEmptyList()
    {
        _mockChatClient.Setup(x => x.CompleteChatAsync(It.IsAny<IEnumerable<OpenAI.Chat.ChatMessage>>(), It.IsAny<ChatCompletionOptions>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Chat Error"));

        var result = await _provider.GenerateExamQuestionsAsync("ocr", "rag", "instruct");
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GenerateStructuredExamAsync_WhenException_ReturnsNull()
    {
        _mockChatClient.Setup(x => x.CompleteChatAsync(It.IsAny<IEnumerable<OpenAI.Chat.ChatMessage>>(), It.IsAny<ChatCompletionOptions>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Chat Error"));

        var result = await _provider.GenerateStructuredExamAsync("ocr", "rag", "instruct");
        result.Should().BeNull();
    }
}

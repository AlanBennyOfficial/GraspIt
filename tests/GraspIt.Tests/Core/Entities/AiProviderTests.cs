using GraspIt.Core.Entities;
using FluentAssertions;

namespace GraspIt.Tests.Core.Entities;

public class AiProviderTests
{
    [Fact]
    public void AiProvider_DefaultValues_AreCorrect()
    {
        // Arrange & Act
        var provider = new AiProvider();

        // Assert
        provider.Id.Should().Be(0);
        provider.Name.Should().BeEmpty();
        provider.ProviderType.Should().Be("OpenAICompatible");
        provider.Endpoint.Should().BeEmpty();
        provider.ModelId.Should().BeEmpty();
        provider.ApiKey.Should().Be("NotRequired");
        provider.IsDefault.Should().BeFalse();
    }

    [Fact]
    public void AiProvider_SetProperties_RetainsValues()
    {
        // Arrange & Act
        var provider = new AiProvider
        {
            Id = 5,
            Name = "LM Studio",
            ProviderType = "OpenAICompatible",
            Endpoint = "http://localhost:1234/v1",
            ModelId = "llama-3.2",
            ApiKey = "sk-test",
            IsDefault = true
        };

        // Assert
        provider.Id.Should().Be(5);
        provider.Name.Should().Be("LM Studio");
        provider.ProviderType.Should().Be("OpenAICompatible");
        provider.Endpoint.Should().Be("http://localhost:1234/v1");
        provider.ModelId.Should().Be("llama-3.2");
        provider.ApiKey.Should().Be("sk-test");
        provider.IsDefault.Should().BeTrue();
    }

    [Theory]
    [InlineData("OpenAICompatible")]
    [InlineData("AzureOpenAI")]
    [InlineData("Google")]
    [InlineData("")]
    public void AiProvider_ProviderType_AcceptsVariousValues(string providerType)
    {
        var provider = new AiProvider { ProviderType = providerType };
        provider.ProviderType.Should().Be(providerType);
    }

    [Fact]
    public void AiProvider_ApiKey_CanBeSetToNull()
    {
        var provider = new AiProvider { ApiKey = null! };
        provider.ApiKey.Should().BeNull();
    }
}

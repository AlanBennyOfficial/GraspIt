using FluentAssertions;
using GraspIt.Application.DTOs;

namespace GraspIt.Tests.Application.DTOs;

public class AiProviderDtoTests
{
    [Fact]
    public void AiProviderDto_DefaultValues_AreCorrect()
    {
        var dto = new AiProviderDto();

        dto.Id.Should().Be(0);
        dto.Name.Should().BeEmpty();
        dto.ProviderType.Should().BeEmpty();
        dto.Endpoint.Should().BeEmpty();
        dto.ModelId.Should().BeEmpty();
        dto.ApiKey.Should().BeNull();
        dto.IsDefault.Should().BeFalse();
    }

    [Fact]
    public void AiProviderDto_SetProperties_RetainsValues()
    {
        var dto = new AiProviderDto
        {
            Id = 3,
            Name = "Google Gemini",
            ProviderType = "OpenAICompatible",
            Endpoint = "https://generativelanguage.googleapis.com/v1beta/openai/",
            ModelId = "gemini-2.5-pro",
            ApiKey = "AI_KEY",
            IsDefault = false
        };

        dto.Id.Should().Be(3);
        dto.Name.Should().Be("Google Gemini");
        dto.Endpoint.Should().Contain("googleapis.com");
        dto.ModelId.Should().Be("gemini-2.5-pro");
        dto.ApiKey.Should().Be("AI_KEY");
    }

    [Fact]
    public void AiProviderDto_ApiKey_CanBeNull()
    {
        var dto = new AiProviderDto { ApiKey = null };
        dto.ApiKey.Should().BeNull();
    }

    [Fact]
    public void AiProviderDto_ApiKey_CanBeEmptyString()
    {
        var dto = new AiProviderDto { ApiKey = "" };
        dto.ApiKey.Should().BeEmpty();
    }
}

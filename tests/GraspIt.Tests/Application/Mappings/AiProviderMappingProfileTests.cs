using AutoMapper;
using FluentAssertions;
using GraspIt.Application.DTOs;
using GraspIt.Application.Mappings;
using GraspIt.Core.Entities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace GraspIt.Tests.Application.Mappings;

public class AiProviderMappingProfileTests
{
    private readonly IMapper _mapper;
    private static readonly ILoggerFactory _loggerFactory = NullLoggerFactory.Instance;

    public AiProviderMappingProfileTests()
    {
        var config = new MapperConfiguration(cfg =>
        {
            cfg.AddProfile<AiProviderMappingProfile>();
        }, _loggerFactory);
        _mapper = config.CreateMapper();
    }

    [Fact]
    public void MappingProfile_ConfigurationIsValid()
    {
        var config = new MapperConfiguration(cfg =>
        {
            cfg.AddProfile<AiProviderMappingProfile>();
        }, _loggerFactory);

        config.AssertConfigurationIsValid();
    }

    // ---- Entity → DTO ----

    [Fact]
    public void Map_AiProviderToDto_MapsAllProperties()
    {
        var entity = new AiProvider
        {
            Id = 1,
            Name = "LM Studio",
            ProviderType = "OpenAICompatible",
            Endpoint = "http://localhost:1234/v1",
            ModelId = "llama-3.2",
            ApiKey = "sk-test",
            IsDefault = true
        };

        var dto = _mapper.Map<AiProviderDto>(entity);

        dto.Id.Should().Be(1);
        dto.Name.Should().Be("LM Studio");
        dto.ProviderType.Should().Be("OpenAICompatible");
        dto.Endpoint.Should().Be("http://localhost:1234/v1");
        dto.ModelId.Should().Be("llama-3.2");
        dto.ApiKey.Should().Be("sk-test");
        dto.IsDefault.Should().BeTrue();
    }

    // ---- DTO → Entity (ReverseMap) ----

    [Fact]
    public void Map_DtoToAiProvider_MapsAllProperties()
    {
        var dto = new AiProviderDto
        {
            Id = 5,
            Name = "Ollama",
            ProviderType = "OpenAICompatible",
            Endpoint = "http://localhost:11434/v1",
            ModelId = "llama3",
            ApiKey = null,
            IsDefault = false
        };

        var entity = _mapper.Map<AiProvider>(dto);

        entity.Id.Should().Be(5);
        entity.Name.Should().Be("Ollama");
        entity.ProviderType.Should().Be("OpenAICompatible");
        entity.Endpoint.Should().Be("http://localhost:11434/v1");
        entity.ModelId.Should().Be("llama3");
        entity.ApiKey.Should().BeNull();
        entity.IsDefault.Should().BeFalse();
    }

    // ---- Null ApiKey ----

    [Fact]
    public void Map_AiProviderWithNullApiKey_MapsCorrectly()
    {
        var entity = new AiProvider
        {
            Name = "Ollama",
            ApiKey = null!
        };

        var dto = _mapper.Map<AiProviderDto>(entity);
        dto.ApiKey.Should().BeNull();
    }

    // ---- List Mapping ----

    [Fact]
    public void Map_ListOfEntities_ToListOfDtos()
    {
        var entities = new List<AiProvider>
        {
            new() { Id = 1, Name = "Provider A", IsDefault = true },
            new() { Id = 2, Name = "Provider B", IsDefault = false },
            new() { Id = 3, Name = "Provider C", IsDefault = false },
        };

        var dtos = _mapper.Map<List<AiProviderDto>>(entities);

        dtos.Should().HaveCount(3);
        dtos[0].Name.Should().Be("Provider A");
        dtos[0].IsDefault.Should().BeTrue();
        dtos[1].Name.Should().Be("Provider B");
        dtos[2].Name.Should().Be("Provider C");
    }

    // ---- Empty Strings ----

    [Fact]
    public void Map_EntityWithEmptyStrings_MapsCorrectly()
    {
        var entity = new AiProvider(); // all defaults

        var dto = _mapper.Map<AiProviderDto>(entity);

        dto.Name.Should().BeEmpty();
        dto.Endpoint.Should().BeEmpty();
        dto.ModelId.Should().BeEmpty();
    }

    // ---- Round-Trip ----

    [Fact]
    public void Map_RoundTrip_EntityToDtoAndBack_PreservesData()
    {
        var original = new AiProvider
        {
            Id = 10,
            Name = "DeepSeek",
            ProviderType = "OpenAICompatible",
            Endpoint = "https://api.deepseek.com/v1",
            ModelId = "deepseek-reasoner",
            ApiKey = "sk-deep",
            IsDefault = false
        };

        var dto = _mapper.Map<AiProviderDto>(original);
        var backToEntity = _mapper.Map<AiProvider>(dto);

        backToEntity.Id.Should().Be(original.Id);
        backToEntity.Name.Should().Be(original.Name);
        backToEntity.ProviderType.Should().Be(original.ProviderType);
        backToEntity.Endpoint.Should().Be(original.Endpoint);
        backToEntity.ModelId.Should().Be(original.ModelId);
        backToEntity.ApiKey.Should().Be(original.ApiKey);
        backToEntity.IsDefault.Should().Be(original.IsDefault);
    }
}

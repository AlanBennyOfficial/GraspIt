using FluentAssertions;
using GraspIt.Core.Entities;
using GraspIt.Infrastructure.Data;
using GraspIt.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using System;
using System.Threading.Tasks;
using Xunit;

namespace GraspIt.Tests.Infrastructure.Services;

public class AiServiceTests
{
    private GraspItDbContext GetDbContext()
    {
        var options = new DbContextOptionsBuilder<GraspItDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        return new GraspItDbContext(options);
    }

    [Fact]
    public async Task GenerateResponseAsync_NoProviders_ThrowsException()
    {
        var context = GetDbContext();
        var service = new AiService(context);

        var result = await service.GenerateResponseAsync("prompt", "context");
        
        result.Should().Contain("No AI Providers configured in the database.");
    }

    [Fact]
    public async Task GenerateMockExamAsync_WithProvider_ReturnsErrorMessageOnFailure()
    {
        var context = GetDbContext();
        context.AiProviders.Add(new AiProvider 
        { 
            Id = 1, 
            Name = "Test", 
            ProviderType = "OpenAICompatible", 
            Endpoint = "http://invalid", 
            ModelId = "m" 
        });
        await context.SaveChangesAsync();

        var service = new AiService(context);
        var result = await service.GenerateMockExamAsync("context");

        (result.StartsWith("Failed to generate exam") || result.Contains("Error")).Should().BeTrue();
    }
}

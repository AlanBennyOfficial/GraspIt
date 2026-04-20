using FluentAssertions;
using GraspIt.Core.Entities;
using GraspIt.Infrastructure.Data;
using GraspIt.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Moq;
using OpenAI.Embeddings;
using System;
using System.ClientModel;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using Pgvector;

namespace GraspIt.Tests.Infrastructure.Services;

public class VectorServicesTests
{
    private GraspItDbContext GetDbContext()
    {
        var options = new DbContextOptionsBuilder<GraspItDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        return new GraspItDbContext(options);
    }

    [Fact]
    public async Task VectorDbService_UpsertEmbeddingsAsync_CatchesException()
    {
        var context = GetDbContext();
        var mockEmbeddingClient = new Mock<EmbeddingClient>();
        
        mockEmbeddingClient.Setup(x => x.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<EmbeddingGenerationOptions>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Mock error"));

        var service = new VectorDbService(context, mockEmbeddingClient.Object);

        await service.UpsertEmbeddingsAsync("1", "test chunk", "prefix");

        var chunks = await context.DocumentChunks.ToListAsync();
        chunks.Should().BeEmpty();
    }

    [Fact]
    public async Task RagRetrievalService_GetRelevantSyllabusContext_HandlesError()
    {
        var context = GetDbContext();
        var mockEmbeddingClient = new Mock<EmbeddingClient>();
        mockEmbeddingClient.Setup(x => x.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<EmbeddingGenerationOptions>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("API Error"));

        var service = new RagRetrievalService(context, mockEmbeddingClient.Object);
        var result = await service.GetRelevantSyllabusContextAsync("query");

        result.Should().Contain("API Error");
    }

    [Fact]
    public async Task RagSearchService_SearchRelevantChunksAsync_ThrowsOnInMemory()
    {
        var context = GetDbContext();
        var service = new RagSearchService(context);
        var vector = new Vector(new float[] { 0.1f });

        // Ensure we have a document for the filter
        context.Documents.Add(new Document { Id = 1, Subject = "Math", Title = "Test" });
        await context.SaveChangesAsync();

        var action = async () => await service.SearchRelevantChunksAsync(vector, "Math");
        await action.Should().ThrowAsync<InvalidOperationException>();
    }
}

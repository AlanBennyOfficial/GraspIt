using System;
using System.ClientModel;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using OpenAI;
using OpenAI.Embeddings;
using Pgvector;
using Pgvector.EntityFrameworkCore;
using GraspIt.Core.Entities;
using GraspIt.Infrastructure.Data;

namespace GraspIt.Infrastructure.Services;

public interface IRagRetrievalService
{
    Task<string> GetRelevantSyllabusContextAsync(string instructions, int topK = 3);
}

public class RagRetrievalService : IRagRetrievalService
{
    private readonly GraspItDbContext _dbContext;
    private readonly EmbeddingClient _embeddingClient;

    public RagRetrievalService(GraspItDbContext dbContext, EmbeddingClient? embeddingClient = null)
    {
        _dbContext = dbContext;
        if (embeddingClient != null)
        {
            _embeddingClient = embeddingClient;
        }
        else
        {
            var options = new OpenAIClientOptions { Endpoint = new Uri("http://localhost:1234/v1") };
            var client = new OpenAIClient(new ApiKeyCredential("lm-studio-local-key"), options);
            _embeddingClient = client.GetEmbeddingClient("text-embedding-nomic-embed-text-v1.5");
        }
    }

    public async Task<string> GetRelevantSyllabusContextAsync(string instructions, int topK = 3)
    {
        try
        {
            // 1. Synthesize the user instructions into an Embedding Vector 
            // Note: Use official OpenAI SDK GenerateEmbeddingAsync
            ClientResult<OpenAIEmbedding> embeddingResult = await _embeddingClient.GenerateEmbeddingAsync(instructions);
            
            // Normalize floats to pgvector wrapper Vector format
            float[] floats = embeddingResult.Value.ToFloats().ToArray();
            var queryVector = new Vector(floats);

            // 2. Perform Pgvector cosine search in Entity Framework Core
            var relatedChunks = await _dbContext.DocumentChunks
                // Use pgvector CosineDistance function to find smallest trigonometric spread between instruction & chunks
                .OrderBy(c => c.Embedding!.CosineDistance(queryVector))
                .Take(topK)
                .Select(c => c.ChunkText) // extract string payload directly
                .ToListAsync();

            if (!relatedChunks.Any())
            {
                return "No RAG context could be located for the user's instructions.";
            }

            return string.Join("\n\n---\n\n", relatedChunks);
        }
        catch (Exception ex)
        {
            // Graceful degrade if LM Studio has no embedding model loaded
            return $"Error generating or searching embeddings locally: {ex.Message}";
        }
    }
}

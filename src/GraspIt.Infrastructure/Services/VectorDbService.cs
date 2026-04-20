using GraspIt.Core.Interfaces;
using GraspIt.Core.Entities;
using GraspIt.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using OpenAI;
using OpenAI.Embeddings;
using Pgvector;
using Pgvector.EntityFrameworkCore;
using System.ClientModel;

namespace GraspIt.Infrastructure.Services;

public class VectorDbService : IVectorDbService
{
    private readonly GraspItDbContext _dbContext;
    private readonly EmbeddingClient _embeddingClient;

    public VectorDbService(GraspItDbContext dbContext, EmbeddingClient? embeddingClient = null)
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

    public async Task UpsertEmbeddingsAsync(string documentId, string text, string metadataPrefix = "")
    {
        var chunks = text.Split(new[] { "\n\n", "\r\n\r\n" }, StringSplitOptions.RemoveEmptyEntries);
        int docId = int.Parse(documentId);

        foreach (var chunk in chunks)
        {
            if (string.IsNullOrWhiteSpace(chunk)) continue;

            var finalChunks = chunk.Length > 2000
                ? SplitIntoChunks(chunk, 1500)
                : new List<string> { chunk };

            foreach (var subChunk in finalChunks)
            {
                try
                {
                    var chunkToEmbed = subChunk;
                    if (!string.IsNullOrWhiteSpace(metadataPrefix))
                    {
                        chunkToEmbed = $"{metadataPrefix}\n\n{subChunk}";
                    }

                    ClientResult<OpenAIEmbedding> embeddingResult = await _embeddingClient.GenerateEmbeddingAsync(chunkToEmbed);
                    float[] floats = embeddingResult.Value.ToFloats().ToArray();
                    var vector = new Vector(floats);

                    _dbContext.DocumentChunks.Add(new DocumentChunk
                    {
                        DocumentId = docId,
                        ChunkText = chunkToEmbed,
                        Embedding = vector,
                        UnitNumber = 0,
                        HeadingLevel = 0
                    });
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error embedding chunk: {ex.Message}");
                }
            }
        }
        await _dbContext.SaveChangesAsync();
    }

    private List<string> SplitIntoChunks(string text, int length)
    {
        var result = new List<string>();
        for (int i = 0; i < text.Length; i += length)
        {
            result.Add(text.Substring(i, Math.Min(length, text.Length - i)));
        }
        return result;
    }

    public async Task<string> SearchAsync(string query, int topK = 3, List<int>? documentIds = null)
    {
        try
        {
            ClientResult<OpenAIEmbedding> embeddingResult = await _embeddingClient.GenerateEmbeddingAsync(query);
            float[] floats = embeddingResult.Value.ToFloats().ToArray();
            var queryVector = new Vector(floats);

            IQueryable<DocumentChunk> dbQuery = _dbContext.DocumentChunks;

            // Apply document-level filter when user targets specific documents
            if (documentIds != null && documentIds.Count > 0)
            {
                dbQuery = dbQuery.Where(c => documentIds.Contains(c.DocumentId));
            }

            var relatedChunks = await dbQuery
                .OrderBy(c => c.Embedding!.CosineDistance(queryVector))
                .Take(topK)
                .Select(c => c.ChunkText)
                .ToListAsync();

            if (!relatedChunks.Any()) return string.Empty;
            return string.Join("\n\n---\n\n", relatedChunks);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Search error: {ex.Message}");
            return string.Empty;
        }
    }
}

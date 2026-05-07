using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.Extensions.Caching.Memory;
using GraspIt.Core.Interfaces;

namespace GraspIt.Application.Features.Chat.Queries;

public class AskQuestionQueryHandler : IRequestHandler<AskQuestionQuery, string>
{
    private readonly IVectorDbService _vectorDbService;
    private readonly IAiService _aiService;
    private readonly IMemoryCache _cache;

    public AskQuestionQueryHandler(
        IVectorDbService vectorDbService,
        IAiService aiService,
        IMemoryCache cache)
    {
        _vectorDbService = vectorDbService;
        _aiService = aiService;
        _cache = cache;
    }

    public async Task<string> Handle(AskQuestionQuery request, CancellationToken cancellationToken)
    {
        string cacheKey = $"Chat_{request.Query.ToLowerInvariant()}_{request.ProviderId}_{request.UseRag}";
        if (_cache.TryGetValue(cacheKey, out string? cachedResponse) && cachedResponse != null)
        {
            return cachedResponse;
        }

        string contextStr = string.Empty;

        // Only perform RAG retrieval when the toggle is enabled
        if (request.UseRag)
        {
            contextStr = await _vectorDbService.SearchAsync(request.Query, topK: 5, request.DocumentIds);
        }

        if (request.UseWebSearch)
        {
            try
            {
                using var client = new System.Net.Http.HttpClient();
                var searchResponse = await client.GetStringAsync($"https://api.duckduckgo.com/?q={Uri.EscapeDataString(request.Query)}&format=json");
                using var doc = System.Text.Json.JsonDocument.Parse(searchResponse);
                if (doc.RootElement.TryGetProperty("AbstractText", out var abstractTextElement))
                {
                    var abs = abstractTextElement.GetString();
                    if (!string.IsNullOrWhiteSpace(abs))
                    {
                        contextStr += $"\n\n[Web Search Result]: {abs}";
                    }
                }
                if (doc.RootElement.TryGetProperty("RelatedTopics", out var relatedTopics) && relatedTopics.GetArrayLength() > 0)
                {
                    var firstTopic = relatedTopics[0].GetProperty("Text").GetString();
                    if (!string.IsNullOrWhiteSpace(firstTopic))
                    {
                        contextStr += $"\n[Related Web Info]: {firstTopic}";
                    }
                }
            }
            catch
            {
                // Ignore web search failures so it doesn't break chat
            }
        }

        var prompt = string.IsNullOrWhiteSpace(contextStr)
            ? $"Answer the following question:\nQuestion: {request.Query}"
            : $"Answer the following question based on the provided context.\nQuestion: {request.Query}";

        var response = await _aiService.GenerateResponseAsync(prompt, contextStr, request.ProviderId, request.History);

        var cacheOptions = new MemoryCacheEntryOptions()
            .SetSlidingExpiration(TimeSpan.FromMinutes(10));
        _cache.Set(cacheKey, response, cacheOptions);

        return response;
    }
}

using GraspIt.Core.Entities;
using GraspIt.Core.Interfaces;
using GraspIt.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.SemanticKernel;
using Polly;
using Polly.Retry;
using Microsoft.SemanticKernel.Http;
using Microsoft.SemanticKernel.ChatCompletion;

namespace GraspIt.Infrastructure.Services;

public class AiService : IAiService
{
    private readonly GraspItDbContext _dbContext;
    private readonly AsyncRetryPolicy _retryPolicy;

    public AiService(GraspItDbContext dbContext)
    {
        _dbContext = dbContext;
        
        // Setup resilient retry policy: 3 retries with exponential backoff
        _retryPolicy = Policy
            .Handle<Exception>() // Catches Network/Timeout/Rate limits typically thrown by HttpClient
            .WaitAndRetryAsync(3, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)));
    }

    private async Task<Kernel> BuildKernelAsync(int? providerId)
    {
        AiProvider? provider = null;
        if (providerId.HasValue)
        {
            provider = await _dbContext.AiProviders.FindAsync(providerId.Value);
        }
        
        if (provider == null)
        {
            provider = await _dbContext.AiProviders.FirstOrDefaultAsync(p => p.IsDefault) 
                       ?? await _dbContext.AiProviders.FirstOrDefaultAsync();
        }

        if (provider == null)
        {
            throw new Exception("No AI Providers configured in the database.");
        }

        var kernelBuilder = Kernel.CreateBuilder();
        
        if (provider.ProviderType == "OpenAICompatible")
        {
            kernelBuilder.AddOpenAIChatCompletion(
                modelId: provider.ModelId,
                apiKey: provider.ApiKey,
                httpClient: new HttpClient { BaseAddress = new Uri(provider.Endpoint) }
            );
        }
        else if (provider.ProviderType == "AzureOpenAI")
        {
            kernelBuilder.AddAzureOpenAIChatCompletion(
                deploymentName: provider.ModelId,
                endpoint: provider.Endpoint,
                apiKey: provider.ApiKey
            );
        }
        
        return kernelBuilder.Build();
    }

    public async Task<string> GenerateResponseAsync(string prompt, string context, int? providerId = null, List<ChatMessage>? history = null)
    {
        try 
        {
            var kernel = await BuildKernelAsync(providerId);
            var chatCompletion = kernel.GetRequiredService<IChatCompletionService>();
            
            var chatHistory = new ChatHistory();
            chatHistory.AddSystemMessage("You are GraspIt, an AI academic assistant.");
            
            if (history != null)
            {
                foreach(var msg in history.OrderBy(m => m.CreatedAt))
                {
                    if (msg.Role == "user") chatHistory.AddUserMessage(msg.Content);
                    else if (msg.Role == "assistant") chatHistory.AddAssistantMessage(msg.Content);
                }
            }

            var fullPrompt = string.IsNullOrWhiteSpace(context) ? prompt : $"Context: {context}\n\n{prompt}";
            chatHistory.AddUserMessage(fullPrompt);

            var result = await _retryPolicy.ExecuteAsync(async () => 
                await chatCompletion.GetChatMessageContentAsync(chatHistory)
            );
            return result?.Content ?? "No response generated.";
        }
        catch (Exception ex)
        {
            return $"Error generating response: {ex.Message}";
        }
    }

    public async Task<string> GenerateMockExamAsync(string context, string mockStyle = "Standard", string focusAreas = "", int? providerId = null)
    {
        try
        {
            var kernel = await BuildKernelAsync(providerId);
            
            var prompt = $@"You are GraspIt, an intelligent test-generating assistant.
Your task is to generate an exam for the student based EXCLUSIVELY on the provided content.

### Formatting Style Requested:
{mockStyle}

### Focus Areas (if any):
{(string.IsNullOrWhiteSpace(focusAreas) ? "Cover all main topics evenly." : focusAreas)}

### Material Context (Includes Document Types like Syllabus, Textbooks, Notes, Slides, and Previous Exams):
{context}

Please generate the mock exam now, ensuring you structure it in markdown and closely align the difficulty and formatting to the requested style.";
            
            var result = await _retryPolicy.ExecuteAsync(async () => 
                await kernel.InvokePromptAsync(prompt)
            );
            
            return result?.GetValue<string>() ?? "Failed to generate exam.";
        }
        catch (Exception ex)
        {
            return $"Error generating mock exam: {ex.Message}";
        }
    }
}

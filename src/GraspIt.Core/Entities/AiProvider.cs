namespace GraspIt.Core.Entities;

public class AiProvider
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty; // e.g., "LM Studio", "OpenAI Local", "vLLM"
    public string ProviderType { get; set; } = "OpenAICompatible"; // "OpenAICompatible", "AzureOpenAI", etc.
    public string Endpoint { get; set; } = string.Empty; // e.g., "http://localhost:1234/v1"
    public string ModelId { get; set; } = string.Empty; // e.g., "local-model"
    public string ApiKey { get; set; } = "NotRequired";
    public bool IsDefault { get; set; } = false;
}

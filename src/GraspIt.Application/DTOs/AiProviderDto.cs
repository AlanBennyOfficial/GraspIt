namespace GraspIt.Application.DTOs;

public class AiProviderDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string ProviderType { get; set; } = string.Empty;
    public string Endpoint { get; set; } = string.Empty;
    public string ModelId { get; set; } = string.Empty;
    public string? ApiKey { get; set; }
    public bool IsDefault { get; set; }
}

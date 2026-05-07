using GraspIt.Core.Entities;

namespace GraspIt.Core.Interfaces;

public interface IAiService
{
    Task<string> GenerateResponseAsync(string prompt, string context, int? providerId = null, List<ChatMessage>? history = null);
    Task<string> GenerateMockExamAsync(string context, string mockStyle = "Standard", string focusAreas = "", int? providerId = null);
}

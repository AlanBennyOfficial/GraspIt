using MediatR;

namespace GraspIt.Application.Features.Chat.Queries;

public class AskQuestionQuery : IRequest<string>
{
    public string Query { get; set; } = string.Empty;
    public int? ProviderId { get; set; }
    public bool UseRag { get; set; } = true;
    public bool UseWebSearch { get; set; } = false;
    public List<int>? DocumentIds { get; set; }
    public List<GraspIt.Core.Entities.ChatMessage>? History { get; set; }
}

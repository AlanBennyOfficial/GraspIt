using MediatR;

namespace GraspIt.Application.Features.Chat.Queries;

public class AskQuestionQuery : IRequest<string>
{
    public string Query { get; set; } = string.Empty;
    public int? ProviderId { get; set; }
    public bool UseRag { get; set; } = true;
    public List<int>? DocumentIds { get; set; }
}

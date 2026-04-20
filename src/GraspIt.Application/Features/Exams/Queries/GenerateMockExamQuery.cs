using MediatR;

namespace GraspIt.Application.Features.Exams.Queries;

public class GenerateMockExamQuery : IRequest<string>
{
    public int UserId { get; set; }
    public string Topic { get; set; } = string.Empty;
    public string MockStyle { get; set; } = "Standard";
    public string FocusAreas { get; set; } = string.Empty;
    public int? ProviderId { get; set; }
}

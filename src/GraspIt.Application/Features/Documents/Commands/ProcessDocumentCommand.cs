using MediatR;

namespace GraspIt.Application.Features.Documents.Commands;

public class ProcessDocumentCommand : IRequest
{
    public string FilePath { get; set; } = string.Empty;
    public string DocumentType { get; set; } = "Other";
    public int UserId { get; set; }
}

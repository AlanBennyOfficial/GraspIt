using System.Threading;
using System.Threading.Tasks;
using MediatR;
using GraspIt.Core.Interfaces;

namespace GraspIt.Application.Features.Exams.Queries;

public class GenerateMockExamQueryHandler : IRequestHandler<GenerateMockExamQuery, string>
{
    private readonly IVectorDbService _vectorDbService;
    private readonly IAiService _aiService;

    public GenerateMockExamQueryHandler(
        IVectorDbService vectorDbService,
        IAiService aiService)
    {
        _vectorDbService = vectorDbService;
        _aiService = aiService;
    }

    public async Task<string> Handle(GenerateMockExamQuery request, CancellationToken cancellationToken)
    {
        // Retrieve relevant documents for the topic combining context from all sources
        var context = await _vectorDbService.SearchAsync(request.Topic, topK: 10);
        
        // Use AI to generate mock exam using the new style formatting
        var examContent = await _aiService.GenerateMockExamAsync(context, request.MockStyle, request.FocusAreas, request.ProviderId);

        return examContent;
    }
}

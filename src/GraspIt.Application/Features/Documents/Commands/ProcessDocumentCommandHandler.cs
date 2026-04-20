using System.IO;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using GraspIt.Core.Entities;
using GraspIt.Core.Interfaces;

namespace GraspIt.Application.Features.Documents.Commands;

public class ProcessDocumentCommandHandler : IRequestHandler<ProcessDocumentCommand>
{
    private readonly IPdfParserService _pdfParserService;
    private readonly IRepository<Document> _documentRepository;
    private readonly IVectorDbService _vectorDbService;

    public ProcessDocumentCommandHandler(
        IPdfParserService pdfParserService,
        IRepository<Document> documentRepository,
        IVectorDbService vectorDbService)
    {
        _pdfParserService = pdfParserService;
        _documentRepository = documentRepository;
        _vectorDbService = vectorDbService;
    }

    public async Task Handle(ProcessDocumentCommand request, CancellationToken cancellationToken)
    {
        // 1. Extract text from PDF
        var text = await _pdfParserService.ExtractTextAsync(request.FilePath);
        var docName = Path.GetFileName(request.FilePath);
        
        // 2. Save Document metadata
        var document = new Document 
        { 
            Title = docName, 
            FilePath = request.FilePath, 
            DocumentType = request.DocumentType,
            UserId = request.UserId 
        };
        await _documentRepository.AddAsync(document);

        // Prepend Document Metadata into the chunk for RAG contextualization
        var metadataPrefix = $"[Source Document Type: {request.DocumentType}]\n[Source Title: {docName}]";

        // 3. Upsert into Vector DB
        await _vectorDbService.UpsertEmbeddingsAsync(document.Id.ToString(), text, metadataPrefix);
    }
}

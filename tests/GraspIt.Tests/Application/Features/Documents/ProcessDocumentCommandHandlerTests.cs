using FluentAssertions;
using GraspIt.Application.Features.Documents.Commands;
using GraspIt.Core.Entities;
using GraspIt.Core.Interfaces;
using Moq;

namespace GraspIt.Tests.Application.Features.Documents;

public class ProcessDocumentCommandHandlerTests
{
    private readonly Mock<IPdfParserService> _pdfParserMock;
    private readonly Mock<IRepository<Document>> _documentRepoMock;
    private readonly Mock<IVectorDbService> _vectorDbMock;
    private readonly ProcessDocumentCommandHandler _handler;

    public ProcessDocumentCommandHandlerTests()
    {
        _pdfParserMock = new Mock<IPdfParserService>();
        _documentRepoMock = new Mock<IRepository<Document>>();
        _vectorDbMock = new Mock<IVectorDbService>();
        _handler = new ProcessDocumentCommandHandler(
            _pdfParserMock.Object,
            _documentRepoMock.Object,
            _vectorDbMock.Object
        );
    }

    // ---- Happy Path ----

    [Fact]
    public async Task Handle_ValidCommand_ExtractsTextAndSavesAndUpserts()
    {
        // Arrange
        _pdfParserMock.Setup(p => p.ExtractTextAsync("/uploads/test.pdf"))
            .ReturnsAsync("Extracted PDF content here.");
        _documentRepoMock.Setup(r => r.AddAsync(It.IsAny<Document>()))
            .ReturnsAsync((Document d) => d);

        var cmd = new ProcessDocumentCommand
        {
            FilePath = "/uploads/test.pdf",
            DocumentType = "Textbook",
            UserId = 1
        };

        // Act
        await _handler.Handle(cmd, CancellationToken.None);

        // Assert
        _pdfParserMock.Verify(p => p.ExtractTextAsync("/uploads/test.pdf"), Times.Once);
        _documentRepoMock.Verify(r => r.AddAsync(It.Is<Document>(d =>
            d.Title == "test.pdf" &&
            d.FilePath == "/uploads/test.pdf" &&
            d.DocumentType == "Textbook" &&
            d.UserId == 1
        )), Times.Once);
        _vectorDbMock.Verify(v => v.UpsertEmbeddingsAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.Is<string>(meta => meta.Contains("[Source Document Type: Textbook]"))
        ), Times.Once);
    }

    // ---- Document Title Extraction ----

    [Fact]
    public async Task Handle_ExtractsTitleFromFilePath()
    {
        // Arrange
        _pdfParserMock.Setup(p => p.ExtractTextAsync(It.IsAny<string>())).ReturnsAsync("content");
        _documentRepoMock.Setup(r => r.AddAsync(It.IsAny<Document>())).ReturnsAsync((Document d) => d);

        var cmd = new ProcessDocumentCommand
        {
            FilePath = "C:\\Users\\student\\uploads\\DataStructures_Chapter5.pdf",
            UserId = 2
        };

        // Act
        await _handler.Handle(cmd, CancellationToken.None);

        // Assert
        _documentRepoMock.Verify(r => r.AddAsync(It.Is<Document>(d =>
            d.Title == "DataStructures_Chapter5.pdf"
        )), Times.Once);
    }

    // ---- Embedded Text Metadata Prefix ----

    [Fact]
    public async Task Handle_PrependsDocumentMetadataToEmbeddedText()
    {
        // Arrange
        _pdfParserMock.Setup(p => p.ExtractTextAsync("/uploads/syllabus.pdf"))
            .ReturnsAsync("Course outline contents");
        _documentRepoMock.Setup(r => r.AddAsync(It.IsAny<Document>())).ReturnsAsync((Document d) => d);

        string capturedText = string.Empty;
        string capturedMetadata = string.Empty;
        _vectorDbMock.Setup(v => v.UpsertEmbeddingsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .Callback<string, string, string>((id, text, meta) => { capturedText = text; capturedMetadata = meta; })
            .Returns(Task.CompletedTask);

        var cmd = new ProcessDocumentCommand
        {
            FilePath = "/uploads/syllabus.pdf",
            DocumentType = "Syllabus",
            UserId = 1
        };

        // Act
        await _handler.Handle(cmd, CancellationToken.None);

        // Assert
        capturedMetadata.Should().StartWith("[Source Document Type: Syllabus]");
        capturedMetadata.Should().Contain("[Source Title: syllabus.pdf]");
        capturedText.Should().Contain("Course outline contents");
    }

    [Theory]
    [InlineData("Previous Question Paper")]
    [InlineData("Teacher Notes")]
    [InlineData("Presentation Slides")]
    public async Task Handle_EmbeddedText_ContainsCorrectDocumentType(string docType)
    {
        // Arrange
        _pdfParserMock.Setup(p => p.ExtractTextAsync(It.IsAny<string>())).ReturnsAsync("content");
        _documentRepoMock.Setup(r => r.AddAsync(It.IsAny<Document>())).ReturnsAsync((Document d) => d);

        string capturedMetadata = string.Empty;
        _vectorDbMock.Setup(v => v.UpsertEmbeddingsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .Callback<string, string, string>((id, text, meta) => capturedMetadata = meta)
            .Returns(Task.CompletedTask);

        var cmd = new ProcessDocumentCommand
        {
            FilePath = "/uploads/doc.pdf",
            DocumentType = docType,
            UserId = 1
        };

        // Act
        await _handler.Handle(cmd, CancellationToken.None);

        // Assert
        capturedMetadata.Should().Contain($"[Source Document Type: {docType}]");
    }

    // ---- Default DocumentType ----

    [Fact]
    public async Task Handle_DefaultDocumentType_IsOther()
    {
        // Arrange
        _pdfParserMock.Setup(p => p.ExtractTextAsync(It.IsAny<string>())).ReturnsAsync("content");
        _documentRepoMock.Setup(r => r.AddAsync(It.IsAny<Document>())).ReturnsAsync((Document d) => d);

        var cmd = new ProcessDocumentCommand
        {
            FilePath = "/uploads/random.pdf",
            UserId = 1
            // DocumentType omitted — should default to "Other"
        };

        // Act
        await _handler.Handle(cmd, CancellationToken.None);

        // Assert
        _documentRepoMock.Verify(r => r.AddAsync(It.Is<Document>(d =>
            d.DocumentType == "Other"
        )), Times.Once);
        _vectorDbMock.Verify(v => v.UpsertEmbeddingsAsync(It.IsAny<string>(),
            It.IsAny<string>(),
            It.Is<string>(meta => meta.Contains("[Source Document Type: Other]"))
        ), Times.Once);
    }

    // ---- Call Order Verification ----

    [Fact]
    public async Task Handle_CallsServicesInCorrectOrder()
    {
        // Arrange
        var callOrder = new List<string>();

        _pdfParserMock.Setup(p => p.ExtractTextAsync(It.IsAny<string>()))
            .Callback(() => callOrder.Add("ExtractText"))
            .ReturnsAsync("text");
        _documentRepoMock.Setup(r => r.AddAsync(It.IsAny<Document>()))
            .Callback(() => callOrder.Add("AddDocument"))
            .ReturnsAsync((Document d) => d);
        _vectorDbMock.Setup(v => v.UpsertEmbeddingsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .Callback(() => callOrder.Add("UpsertEmbeddings"))
            .Returns(Task.CompletedTask);

        var cmd = new ProcessDocumentCommand { FilePath = "/test.pdf", UserId = 1 };

        // Act
        await _handler.Handle(cmd, CancellationToken.None);

        // Assert - Must be: Extract → Save → Upsert
        callOrder.Should().ContainInOrder("ExtractText", "AddDocument", "UpsertEmbeddings");
    }

    // ---- Empty PDF Text ----

    [Fact]
    public async Task Handle_EmptyPdfText_StillSavesDocumentAndUpserts()
    {
        // Arrange
        _pdfParserMock.Setup(p => p.ExtractTextAsync(It.IsAny<string>())).ReturnsAsync(string.Empty);
        _documentRepoMock.Setup(r => r.AddAsync(It.IsAny<Document>())).ReturnsAsync((Document d) => d);

        var cmd = new ProcessDocumentCommand { FilePath = "/empty.pdf", UserId = 1 };

        // Act
        await _handler.Handle(cmd, CancellationToken.None);

        // Assert - even with empty content, the pipeline should complete
        _documentRepoMock.Verify(r => r.AddAsync(It.IsAny<Document>()), Times.Once);
        _vectorDbMock.Verify(v => v.UpsertEmbeddingsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Once);
    }
}

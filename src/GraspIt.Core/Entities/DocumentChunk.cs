using Pgvector;

namespace GraspIt.Core.Entities;

public class DocumentChunk
{
    public int Id { get; set; }
    
    // Foreign Key to the Parent Document
    public int DocumentId { get; set; }
    public Document Document { get; set; } = null!;

    // Metadata for filtering
    public int UnitNumber { get; set; }
    public int HeadingLevel { get; set; }
    
    // The actual text content
    public string ChunkText { get; set; } = string.Empty;
    
    // The pgvector embedding representation
    // Common embedding dimensions like OpenAI text-embedding-3-small use 1536
    [System.ComponentModel.DataAnnotations.Schema.Column(TypeName = "vector(768)")]
    public Vector? Embedding { get; set; }
}

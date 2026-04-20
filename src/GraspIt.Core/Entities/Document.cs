using System;
using System.Collections.Generic;

namespace GraspIt.Core.Entities;

public class Document
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public string DocumentType { get; set; } = "Other"; // e.g. Textbook, Notes, Syllabus, PreviousExam, Slides
    public string Subject { get; set; } = string.Empty;
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
    
    public int UserId { get; set; }
    public User User { get; set; } = null!;

    // Navigation property for the advanced RAG Parent-Child relationship
    public ICollection<DocumentChunk> DocumentChunks { get; set; } = new List<DocumentChunk>();
}

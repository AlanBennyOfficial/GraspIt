using GraspIt.Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace GraspIt.Infrastructure.Data;

public class GraspItDbContext : DbContext
{
    public GraspItDbContext(DbContextOptions<GraspItDbContext> options) : base(options) { }

    public DbSet<User> Users { get; set; } = null!;
    public DbSet<Document> Documents { get; set; } = null!;
    public DbSet<DocumentChunk> DocumentChunks { get; set; } = null!;
    public DbSet<MockExam> MockExams { get; set; } = null!;
    public DbSet<Question> Questions { get; set; } = null!;
    public DbSet<AiProvider> AiProviders { get; set; } = null!;
    public DbSet<ChatFolder> ChatFolders { get; set; } = null!;
    public DbSet<ChatSession> ChatSessions { get; set; } = null!;
    public DbSet<ChatMessage> ChatMessages { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.HasPostgresExtension("vector");

        // User relationships
        modelBuilder.Entity<User>()
            .HasMany(u => u.Documents)
            .WithOne(d => d.User)
            .HasForeignKey(d => d.UserId);

        modelBuilder.Entity<User>()
            .HasMany(u => u.MockExams)
            .WithOne(m => m.User)
            .HasForeignKey(m => m.UserId);

        modelBuilder.Entity<User>()
            .HasMany(u => u.ChatSessions)
            .WithOne(s => s.User)
            .HasForeignKey(s => s.UserId);

        modelBuilder.Entity<User>()
            .HasMany(u => u.ChatFolders)
            .WithOne(f => f.User)
            .HasForeignKey(f => f.UserId);

        // MockExam -> Questions
        modelBuilder.Entity<MockExam>()
            .HasMany(m => m.Questions)
            .WithOne(q => q.MockExam)
            .HasForeignKey(q => q.MockExamId);

        // Document -> DocumentChunks (cascade delete)
        modelBuilder.Entity<Document>()
            .HasMany(d => d.DocumentChunks)
            .WithOne(c => c.Document)
            .HasForeignKey(c => c.DocumentId)
            .OnDelete(DeleteBehavior.Cascade);

        // ChatFolder -> ChatSessions (set null on delete)
        modelBuilder.Entity<ChatFolder>()
            .HasMany(f => f.Sessions)
            .WithOne(s => s.Folder)
            .HasForeignKey(s => s.FolderId)
            .OnDelete(DeleteBehavior.SetNull);

        // ChatSession -> ChatMessages (cascade delete)
        modelBuilder.Entity<ChatSession>()
            .HasMany(s => s.Messages)
            .WithOne(m => m.Session)
            .HasForeignKey(m => m.SessionId)
            .OnDelete(DeleteBehavior.Cascade);

        if (Database.ProviderName?.Contains("InMemory") == true)
        {
            modelBuilder.Entity<DocumentChunk>().Ignore(c => c.Embedding);
        }
        else
        {
            modelBuilder.Entity<DocumentChunk>()
                .HasIndex(c => c.Embedding)
                .HasMethod("hnsw")
                .HasOperators("vector_cosine_ops");
        }
    }
}

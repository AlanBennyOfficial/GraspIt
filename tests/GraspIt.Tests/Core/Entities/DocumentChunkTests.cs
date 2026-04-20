using GraspIt.Core.Entities;
using Xunit;
using Pgvector;

namespace GraspIt.Tests.Core.Entities
{
    public class DocumentChunkTests
    {
        [Fact]
        public void DocumentChunk_Initialization_SetsProperties()
        {
            var vector = new Vector(new float[] { 0.1f, 0.2f, 0.3f });
            var chunk = new DocumentChunk
            {
                Id = 1,
                DocumentId = 10,
                ChunkText = "Test chunk",
                UnitNumber = 2,
                HeadingLevel = 1,
                Embedding = vector
            };

            Assert.Equal(1, chunk.Id);
            Assert.Equal(10, chunk.DocumentId);
            Assert.Equal("Test chunk", chunk.ChunkText);
            Assert.Equal(2, chunk.UnitNumber);
            Assert.Equal(1, chunk.HeadingLevel);
            Assert.Equal(vector, chunk.Embedding);
        }
    }
}

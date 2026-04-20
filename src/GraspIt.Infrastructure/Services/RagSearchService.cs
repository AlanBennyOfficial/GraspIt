using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using GraspIt.Core.Entities;
using GraspIt.Infrastructure.Data;
using Pgvector;
using Pgvector.EntityFrameworkCore;

namespace GraspIt.Infrastructure.Services;

public class RagSearchService
{
    private readonly GraspItDbContext _dbContext;

    public RagSearchService(GraspItDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <summary>
    /// ADVANCED RAG PIPELINE
    /// Demonstrates filtering, vector similarity search, and parent-child retrieval via Npgsql EF Core. 
    /// </summary>
    /// <param name="userQueryVector">The embedding generated from the User's query (e.g., using text-embedding-ada-002)</param>
    /// <param name="subjectFilter">The Subject Metadata to pre-filter on, so we avoid measuring distance against irrelevant data.</param>
    /// <param name="topK">The number of semantically related chunks to return.</param>
    public async Task<List<DocumentChunk>> SearchRelevantChunksAsync(Vector userQueryVector, string subjectFilter, int topK = 5)
    {
        // Execute the vectorized search! 
        return await _dbContext.DocumentChunks
            // 1. FILTERING: (This is called Metadata pre-filtering) 
            // We use standard LINQ to constrain the search space drastically reducing computational cost
            // This translates to a standard SQL 'WHERE c.Document.Subject == subjectFilter'
            .Where(c => c.Document.Subject == subjectFilter)
            
            // 2. VECTOR SEARCH: c.Embedding.CosineDistance(userQueryVector) is translated by Pgvector.EntityFrameworkCore 
            // directly to the PostgreSQL `<=>` operator (cosine distance). 
            .OrderBy(c => c.Embedding!.CosineDistance(userQueryVector))

            // 3. LIMIT MATCHES: Translate mapping into `LIMIT X` on the SQL Query
            .Take(topK)
            
            // 4. PARENT-CHILD CONTEXT: Hydrate the Document object directly with EF `.Include()` 
            // so our AI pipeline can look at `result.Document.Title` or pull the entire `FilePath`. 
            // This is crucial to advanced RAG: retrieve small semantic details, pull large parental contexts!
            .Include(c => c.Document)
            
            .ToListAsync();
    }
}

namespace GraspIt.Core.Interfaces;

public interface IVectorDbService
{
    Task UpsertEmbeddingsAsync(string documentId, string text, string metadataPrefix = "");
    Task<string> SearchAsync(string query, int topK = 3, List<int>? documentIds = null);
}

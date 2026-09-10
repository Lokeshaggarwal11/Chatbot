using KnowledgeAssistant.Api.Models;

namespace KnowledgeAssistant.Api.Services;

public class RetrievedChunkResult
{
    public string DocumentId { get; set; } = string.Empty;
    public string ChunkId { get; set; } = string.Empty;
    public string DocumentTitle { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string Department { get; set; } = string.Empty;
    public int ChunkIndex { get; set; }
    public int PageNumber { get; set; }
    public string ChunkText { get; set; } = string.Empty;
    public double RelevanceScore { get; set; }
}

public interface IEmbeddingService
{
    Task<float[]> GenerateEmbeddingAsync(string text);
    Task<List<float[]>> GenerateEmbeddingsAsync(List<string> texts);
}

public interface IVectorStore
{
    Task IndexChunksAsync(List<DocumentChunk> chunks);
    Task<List<RetrievedChunkResult>> SearchAsync(float[] queryEmbedding, string queryText, int topK, double minScore);
    Task DeleteDocumentChunksAsync(string documentId);
}

public interface ILLMService
{
    Task<string> GenerateGroundedAnswerAsync(string question, List<RetrievedChunkResult> chunks);
}

public interface IDocumentService
{
    Task<Document> IngestDocumentAsync(DocumentUploadRequest request);
    Task<Document> IngestPdfStreamAsync(Stream stream, string fileName, string title, string department, string version);
    Task<List<Document>> ImportPdfsFromFolderAsync(string? folderPath = null);
    Task<bool> DeleteDocumentAsync(string id);
    Task<int> PurgeAllDocumentsAsync();
    Task<List<DocumentResponseDto>> GetDocumentsAsync();
    Task<Document?> GetDocumentAsync(string id);
    Task<bool> ReindexDocumentAsync(string id);
    Task ProcessDocumentChunkingAndIndexingAsync(string documentId);
    Task<object> GetChunksJsonAsync();
    Task<string> ExportChunksToJsonFileAsync();
    string? GetDocumentFilePath(string id);
    string? FindPdfFile(string fileName);
}

public interface IRAGService
{
    Task<ChatResponse> ExecuteQueryAsync(ChatRequest request);
}

public interface IAuditService
{
    Task LogAccessEventAsync(string question, List<RetrievedChunkResult> retrievedChunks, long durationMs);
    Task<List<AuditLog>> GetAuditLogsAsync(int page = 1, int pageSize = 50);
}

public record IngestionJobTask(string JobId, string DocumentId, string DocumentTitle);

public interface IIngestionQueue
{
    ValueTask EnqueueAsync(string documentId, string documentTitle);
    ValueTask<IngestionJobTask> DequeueAsync(CancellationToken cancellationToken);
    Task<List<IngestionJob>> GetStatusListAsync();
}

namespace KnowledgeAssistant.Api.Models;

public class Document
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Title { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public string Department { get; set; } = "General";
    public string Version { get; set; } = "1.0";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public bool IsActive { get; set; } = true;
    public string? Content { get; set; }

    public List<DocumentChunk> Chunks { get; set; } = new();
}

public class DocumentChunk
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string DocumentId { get; set; } = string.Empty;
    public Document? Document { get; set; }
    public string ChunkText { get; set; } = string.Empty;
    public int ChunkIndex { get; set; }
    public int PageNumber { get; set; } = 1;
    public int TokenCount { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    // In-memory or indexed embeddings
    public float[]? Embedding { get; set; }
}

public class AuditLog
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Question { get; set; } = string.Empty;
    public string DocumentsUsed { get; set; } = string.Empty; // JSON array of document titles & chunk IDs
    public int RetrievedChunkCount { get; set; }
    public long ExecutionTimeMs { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class IngestionJob
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string DocumentId { get; set; } = string.Empty;
    public string DocumentTitle { get; set; } = string.Empty;
    public string Status { get; set; } = "Queued"; // Queued, Processing, Completed, Failed
    public string? ErrorMessage { get; set; }
    public int ChunksProcessed { get; set; }
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }
}

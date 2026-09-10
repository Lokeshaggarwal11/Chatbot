namespace KnowledgeAssistant.Api.Models;

public class ChatRequest
{
    public string Question { get; set; } = string.Empty;
    public int TopK { get; set; } = 5;
    public double MinRelevanceScore { get; set; } = 0.35;
}

public class StageTiming
{
    public string StageName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public long DurationMs { get; set; }
    public double Percentage { get; set; }
}

public class PerformanceMetrics
{
    public long TotalDurationMs { get; set; }
    public long EmbeddingDurationMs { get; set; }
    public long VectorSearchDurationMs { get; set; }
    public long LlmGenerationDurationMs { get; set; }
    public long PostProcessingDurationMs { get; set; }
    public List<StageTiming> Stages { get; set; } = new();
}

public class ChatResponse
{
    public string MessageId { get; set; } = Guid.NewGuid().ToString();
    public string Question { get; set; } = string.Empty;
    public string Answer { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public long ExecutionTimeMs { get; set; }
    public PerformanceMetrics PerformanceMetrics { get; set; } = new();
    public Dictionary<string, long> StageTimingsMs { get; set; } = new();
}

public class FeedbackRequest
{
    public string Feedback { get; set; } = string.Empty; // "positive" | "negative"
    public string? Comment { get; set; }
}

public class DocumentUploadRequest
{
    public string Title { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string Source { get; set; } = "Direct Upload";
    public string Department { get; set; } = "General";
    public string Version { get; set; } = "1.0";
    public string Content { get; set; } = string.Empty;
}

public class DocumentResponseDto
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public string Department { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public int ChunkCount { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class AISettingsDto
{
    public string Provider { get; set; } = "LocalEngine"; // "AzureOpenAI" or "LocalEngine"
    public string? AzureOpenAIEndpoint { get; set; }
    public string? AzureOpenAIApiKey { get; set; }
    public string? AzureOpenAIDeploymentName { get; set; }
    public string? AzureOpenAIEmbeddingDeployment { get; set; }
    public string? AzureSearchEndpoint { get; set; }
    public string? AzureSearchApiKey { get; set; }
    public string? AzureSearchIndexName { get; set; }
}

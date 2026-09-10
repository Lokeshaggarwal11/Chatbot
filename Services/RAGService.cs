using System.Diagnostics;
using KnowledgeAssistant.Api.Data;
using KnowledgeAssistant.Api.Models;

namespace KnowledgeAssistant.Api.Services;

public class RAGService : IRAGService
{
    private readonly IEmbeddingService _embeddingService;
    private readonly IVectorStore _vectorStore;
    private readonly ILLMService _llmService;
    private readonly IAuditService _auditService;
    private readonly IDocumentStore _documentStore;
    private readonly ILogger<RAGService> _logger;

    public RAGService(
        IEmbeddingService embeddingService,
        IVectorStore vectorStore,
        ILLMService llmService,
        IAuditService auditService,
        IDocumentStore documentStore,
        ILogger<RAGService> logger)
    {
        _embeddingService = embeddingService;
        _vectorStore = vectorStore;
        _llmService = llmService;
        _auditService = auditService;
        _documentStore = documentStore;
        _logger = logger;
    }

    public async Task<ChatResponse> ExecuteQueryAsync(ChatRequest request)
    {
        var totalStopwatch = Stopwatch.StartNew();
        var stageStopwatch = Stopwatch.StartNew();

        // 1. Generate Query Embedding
        stageStopwatch.Restart();
        var queryEmbedding = await _embeddingService.GenerateEmbeddingAsync(request.Question);
        long embeddingMs = stageStopwatch.ElapsedMilliseconds;

        // 2. Vector / Hybrid Search (FAISS)
        stageStopwatch.Restart();
        var retrievedChunks = await _vectorStore.SearchAsync(
            queryEmbedding,
            request.Question,
            topK: request.TopK > 0 ? Math.Min(request.TopK, 5) : 5,
            minScore: request.MinRelevanceScore > 0 ? request.MinRelevanceScore : 0.35
        );
        long vectorSearchMs = stageStopwatch.ElapsedMilliseconds;

        // 3. LLM Grounded Answer Generation
        stageStopwatch.Restart();
        string answer;
        if (retrievedChunks.Count == 0)
        {
            answer = "No relevant information found in the knowledge base.";
        }
        else
        {
            answer = await _llmService.GenerateGroundedAnswerAsync(request.Question, retrievedChunks);
        }
        long llmGenerationMs = stageStopwatch.ElapsedMilliseconds;

        // 4. Audit Logging & Finalization
        stageStopwatch.Restart();
        totalStopwatch.Stop();
        long executionTimeMs = totalStopwatch.ElapsedMilliseconds;

        await _auditService.LogAccessEventAsync(
            request.Question,
            retrievedChunks,
            executionTimeMs
        );
        long postProcessingMs = stageStopwatch.ElapsedMilliseconds;

        var stages = new List<StageTiming>
        {
            new StageTiming { StageName = "Embedding Generation", Description = "Ollama BAAI/bge-small-en-v1.5 vector encoding", DurationMs = embeddingMs },
            new StageTiming { StageName = "Vector Search (FAISS)", Description = "Cosine similarity index search & Top-K ranking", DurationMs = vectorSearchMs },
            new StageTiming { StageName = "LLM Generation", Description = "Ollama Qwen 2.5 (1.5B) grounded answer generation", DurationMs = llmGenerationMs },
            new StageTiming { StageName = "Post-Process & Logging", Description = "Query logging and cleanup", DurationMs = postProcessingMs }
        };

        long sumMs = Math.Max(1, stages.Sum(s => s.DurationMs));
        foreach (var stage in stages)
        {
            stage.Percentage = Math.Round(((double)stage.DurationMs / sumMs) * 100.0, 1);
        }

        var performanceMetrics = new PerformanceMetrics
        {
            TotalDurationMs = executionTimeMs,
            EmbeddingDurationMs = embeddingMs,
            VectorSearchDurationMs = vectorSearchMs,
            LlmGenerationDurationMs = llmGenerationMs,
            PostProcessingDurationMs = postProcessingMs,
            Stages = stages
        };

        var stageTimingsDict = new Dictionary<string, long>
        {
            { "embeddingMs", embeddingMs },
            { "vectorSearchMs", vectorSearchMs },
            { "llmGenerationMs", llmGenerationMs },
            { "postProcessingMs", postProcessingMs },
            { "totalExecutionMs", executionTimeMs }
        };

        return new ChatResponse
        {
            MessageId = Guid.NewGuid().ToString(),
            Question = request.Question,
            Answer = answer,
            Timestamp = DateTime.UtcNow,
            ExecutionTimeMs = executionTimeMs,
            PerformanceMetrics = performanceMetrics,
            StageTimingsMs = stageTimingsDict
        };
    }
}

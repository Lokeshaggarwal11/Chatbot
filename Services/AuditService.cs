using System.Text.Json;
using KnowledgeAssistant.Api.Data;
using KnowledgeAssistant.Api.Models;

namespace KnowledgeAssistant.Api.Services;

public class AuditService : IAuditService
{
    private readonly IDocumentStore _documentStore;
    private readonly ILogger<AuditService> _logger;

    public AuditService(IDocumentStore documentStore, ILogger<AuditService> logger)
    {
        _documentStore = documentStore;
        _logger = logger;
    }

    public async Task LogAccessEventAsync(
        string question,
        List<RetrievedChunkResult> retrievedChunks,
        long durationMs)
    {
        var docsUsed = retrievedChunks.Select(a => new
        {
            documentId = a.DocumentId,
            title = a.DocumentTitle,
            chunkId = a.ChunkId,
            pageNumber = a.PageNumber,
            score = a.RelevanceScore
        }).ToList();

        var log = new AuditLog
        {
            Question = question,
            DocumentsUsed = JsonSerializer.Serialize(docsUsed),
            RetrievedChunkCount = retrievedChunks.Count,
            ExecutionTimeMs = durationMs,
            CreatedAt = DateTime.UtcNow
        };

        await _documentStore.AddAuditLogAsync(log);

        _logger.LogInformation("Query Log: '{Question}' | Retrieved: {Count} chunks | Time: {Ms}ms",
            question, retrievedChunks.Count, durationMs);
    }

    public async Task<List<AuditLog>> GetAuditLogsAsync(int page = 1, int pageSize = 50)
    {
        return await _documentStore.GetAuditLogsAsync(page, pageSize);
    }
}

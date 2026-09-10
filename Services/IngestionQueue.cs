using System.Threading.Channels;
using KnowledgeAssistant.Api.Data;
using KnowledgeAssistant.Api.Models;

namespace KnowledgeAssistant.Api.Services;

public class IngestionQueue : IIngestionQueue
{
    private readonly Channel<IngestionJobTask> _channel;
    private readonly IDocumentStore _documentStore;
    private readonly ILogger<IngestionQueue> _logger;

    public IngestionQueue(IDocumentStore documentStore, ILogger<IngestionQueue> logger)
    {
        _documentStore = documentStore;
        _logger = logger;
        
        var options = new BoundedChannelOptions(100)
        {
            FullMode = BoundedChannelFullMode.Wait
        };
        _channel = Channel.CreateBounded<IngestionJobTask>(options);
    }

    public async ValueTask EnqueueAsync(string documentId, string documentTitle)
    {
        var job = new IngestionJob
        {
            DocumentId = documentId,
            DocumentTitle = documentTitle,
            Status = "Queued",
            StartedAt = DateTime.UtcNow
        };

        await _documentStore.AddOrUpdateIngestionJobAsync(job);
        await _channel.Writer.WriteAsync(new IngestionJobTask(job.Id, documentId, documentTitle));
        _logger.LogInformation("Queued ingestion job {JobId} for document {DocumentTitle}", job.Id, documentTitle);
    }

    public ValueTask<IngestionJobTask> DequeueAsync(CancellationToken cancellationToken)
    {
        return _channel.Reader.ReadAsync(cancellationToken);
    }

    public async Task<List<IngestionJob>> GetStatusListAsync()
    {
        return await _documentStore.GetIngestionJobsAsync(50);
    }
}

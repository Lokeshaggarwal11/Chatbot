using KnowledgeAssistant.Api.Data;

namespace KnowledgeAssistant.Api.Services;

public class BackgroundIngestionWorker : BackgroundService
{
    private readonly IIngestionQueue _queue;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<BackgroundIngestionWorker> _logger;

    public BackgroundIngestionWorker(
        IIngestionQueue queue,
        IServiceScopeFactory scopeFactory,
        ILogger<BackgroundIngestionWorker> logger)
    {
        _queue = queue;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Background Document Ingestion Worker started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var task = await _queue.DequeueAsync(stoppingToken);
                _logger.LogInformation("Processing ingestion job {JobId} for document {Title}", task.JobId, task.DocumentTitle);

                using var scope = _scopeFactory.CreateScope();
                var documentStore = scope.ServiceProvider.GetRequiredService<IDocumentStore>();
                var docService = scope.ServiceProvider.GetRequiredService<IDocumentService>();

                var job = await documentStore.GetIngestionJobByIdAsync(task.JobId);
                if (job != null)
                {
                    job.Status = "Processing";
                    await documentStore.AddOrUpdateIngestionJobAsync(job);
                }

                try
                {
                    await docService.ProcessDocumentChunkingAndIndexingAsync(task.DocumentId);
                    await docService.ExportChunksToJsonFileAsync();

                    if (job != null)
                    {
                        var chunks = await documentStore.GetChunksByDocumentIdAsync(task.DocumentId);
                        job.Status = "Completed";
                        job.ChunksProcessed = chunks.Count;
                        job.CompletedAt = DateTime.UtcNow;
                        await documentStore.AddOrUpdateIngestionJobAsync(job);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing ingestion job {JobId}", task.JobId);
                    if (job != null)
                    {
                        job.Status = "Failed";
                        job.ErrorMessage = ex.Message;
                        job.CompletedAt = DateTime.UtcNow;
                        await documentStore.AddOrUpdateIngestionJobAsync(job);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error in background ingestion worker loop");
                await Task.Delay(1000, stoppingToken);
            }
        }

        _logger.LogInformation("Background Document Ingestion Worker stopped");
    }
}

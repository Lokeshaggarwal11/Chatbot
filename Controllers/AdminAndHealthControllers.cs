using KnowledgeAssistant.Api.Models;
using KnowledgeAssistant.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace KnowledgeAssistant.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AdminController : ControllerBase
{
    private readonly IIngestionQueue _queue;
    private readonly IAuditService _auditService;
    private readonly ILogger<AdminController> _logger;

    public AdminController(IIngestionQueue queue, IAuditService auditService, ILogger<AdminController> logger)
    {
        _queue = queue;
        _auditService = auditService;
        _logger = logger;
    }

    /// <summary>
    /// GET /api/admin/indexing/status - View background indexing queue and job status
    /// </summary>
    [HttpGet("indexing/status")]
    public async Task<ActionResult<List<IngestionJob>>> GetIndexingStatus()
    {
        var jobs = await _queue.GetStatusListAsync();
        return Ok(jobs);
    }

    /// <summary>
    /// GET /api/admin/audit-logs - View authorized audit access records
    /// </summary>
    [HttpGet("audit-logs")]
    public async Task<ActionResult<List<AuditLog>>> GetAuditLogs([FromQuery] int page = 1, [FromQuery] int pageSize = 50)
    {
        var logs = await _auditService.GetAuditLogsAsync(page, pageSize);
        return Ok(logs);
    }
}

[ApiController]
[Route("[controller]")]
public class HealthController : ControllerBase
{
    /// <summary>
    /// GET /health - Service health check
    /// </summary>
    [HttpGet]
    public IActionResult HealthCheck()
    {
        return Ok(new
        {
            status = "Healthy",
            timestamp = DateTime.UtcNow,
            service = "KnowledgeAssistant.Api",
            version = "1.0.0",
            features = new[] { "RAG", "ACL-PreFiltering", "VectorSearch", "AsyncIngestion", "AuditLogging" }
        });
    }
}

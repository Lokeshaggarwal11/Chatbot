using KnowledgeAssistant.Api.Models;
using KnowledgeAssistant.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace KnowledgeAssistant.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DocumentsController : ControllerBase
{
    private readonly IDocumentService _docService;
    private readonly ILogger<DocumentsController> _logger;

    public DocumentsController(IDocumentService docService, ILogger<DocumentsController> logger)
    {
        _docService = docService;
        _logger = logger;
    }

    /// <summary>
    /// POST /api/documents - Create/upload document metadata & markdown content
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<DocumentResponseDto>> CreateDocument([FromBody] DocumentUploadRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Title) || string.IsNullOrWhiteSpace(request.Content))
        {
            return BadRequest(new { error = "Title and Content are required" });
        }

        var doc = await _docService.IngestDocumentAsync(request);
        return CreatedAtAction(nameof(GetDocument), new { id = doc.Id }, new DocumentResponseDto
        {
            Id = doc.Id,
            Title = doc.Title,
            FileName = doc.FileName,
            Source = doc.Source,
            Department = doc.Department,
            Version = doc.Version,
            ChunkCount = doc.Chunks.Count,
            IsActive = doc.IsActive,
            CreatedAt = doc.CreatedAt,
            UpdatedAt = doc.UpdatedAt
        });
    }

    /// <summary>
    /// POST /api/documents/upload-pdf - Upload and extract a PDF document
    /// </summary>
    [HttpPost("upload-pdf")]
    public async Task<ActionResult<DocumentResponseDto>> UploadPdfDocument(
        [FromForm] IFormFile file,
        [FromForm] string? title,
        [FromForm] string? department,
        [FromForm] string? version = "1.0")
    {
        if (file == null || file.Length == 0)
        {
            return BadRequest(new { error = "No file uploaded or file is empty" });
        }

        if (!file.FileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new { error = "Only PDF (.pdf) files are supported by this endpoint" });
        }

        try
        {
            // Save uploaded file to documents directory so it can be viewed and downloaded
            var documentsFolder = Path.Combine(Directory.GetCurrentDirectory(), "documents");
            if (!Directory.Exists(documentsFolder)) Directory.CreateDirectory(documentsFolder);
            var filePath = Path.Combine(documentsFolder, file.FileName);
            using (var fileSaveStream = System.IO.File.Create(filePath))
            {
                await file.CopyToAsync(fileSaveStream);
            }

            using var stream = System.IO.File.OpenRead(filePath);
            var doc = await _docService.IngestPdfStreamAsync(
                stream,
                file.FileName,
                title ?? "",
                department ?? "General",
                version ?? "1.0");

            return Ok(new DocumentResponseDto
            {
                Id = doc.Id,
                Title = doc.Title,
                FileName = doc.FileName,
                Source = doc.Source,
                Department = doc.Department,
                Version = doc.Version,
                ChunkCount = doc.Chunks.Count,
                IsActive = doc.IsActive,
                CreatedAt = doc.CreatedAt,
                UpdatedAt = doc.UpdatedAt
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to ingest uploaded PDF file {FileName}", file.FileName);
            return StatusCode(500, new { error = $"PDF Ingestion Error: {ex.Message}" });
        }
    }

    /// <summary>
    /// POST /api/documents/scan-folder - Scan backend/documents folder for new PDFs
    /// </summary>
    [HttpPost("scan-folder")]
    public async Task<ActionResult<object>> ScanFolder()
    {
        try
        {
            var imported = await _docService.ImportPdfsFromFolderAsync();
            return Ok(new
            {
                message = $"Successfully scanned folder and imported {imported.Count} new PDF documents.",
                importedCount = imported.Count,
                documents = imported.Select(d => new { d.Id, d.Title, d.FileName })
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Folder scan failed");
            return StatusCode(500, new { error = $"Folder scan error: {ex.Message}" });
        }
    }

    /// <summary>
    /// DELETE /api/documents/{id} - Delete a document and its chunks
    /// </summary>
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteDocument(string id)
    {
        var success = await _docService.DeleteDocumentAsync(id);
        if (!success)
        {
            return NotFound(new { error = "Document not found" });
        }

        return Ok(new { message = $"Document {id} and its vector chunks were deleted successfully." });
    }

    /// <summary>
    /// POST /api/documents/purge-all - Purge all seeded/existing documents and chunks
    /// </summary>
    [HttpPost("purge-all")]
    public async Task<IActionResult> PurgeAllDocuments()
    {
        var count = await _docService.PurgeAllDocumentsAsync();
        return Ok(new { message = $"Successfully purged {count} documents and all indexed vectors.", purgedCount = count });
    }

    /// <summary>
    /// GET /api/documents - List all active documents in knowledge base
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<List<DocumentResponseDto>>> ListDocuments()
    {
        var docs = await _docService.GetDocumentsAsync();
        return Ok(docs);
    }

    /// <summary>
    /// GET /api/documents/{id} - Get document details & chunks
    /// </summary>
    [HttpGet("{id}")]
    public async Task<ActionResult<Document>> GetDocument(string id)
    {
        var doc = await _docService.GetDocumentAsync(id);
        if (doc == null)
        {
            return NotFound(new { error = "Document not found" });
        }

        return Ok(doc);
    }

    /// <summary>
    /// POST /api/documents/{id}/reindex - Re-index a document
    /// </summary>
    [HttpPost("{id}/reindex")]
    public async Task<IActionResult> ReindexDocument(string id)
    {
        var success = await _docService.ReindexDocumentAsync(id);
        if (!success)
        {
            return NotFound(new { error = "Document not found for re-indexing" });
        }
        return Accepted(new { status = "Document queued for re-indexing" });
    }

    /// <summary>
    /// GET /api/documents/{id}/file - Serve physical PDF file for inline viewing and jumping to specific page
    /// </summary>
    [HttpGet("{id}/file")]
    public IActionResult GetDocumentFile(string id)
    {
        var filePath = _docService.GetDocumentFilePath(id);
        if (string.IsNullOrEmpty(filePath) || !System.IO.File.Exists(filePath))
        {
            return NotFound(new { error = $"PDF file for document '{id}' was not found on the server." });
        }

        var fileName = Path.GetFileName(filePath);
        Response.Headers.Append("Content-Disposition", $"inline; filename=\"{fileName}\"");
        return PhysicalFile(filePath, "application/pdf", enableRangeProcessing: true);
    }

    /// <summary>
    /// GET /api/documents/file/{fileName} - Serve a PDF file by file name directly
    /// </summary>
    [HttpGet("file/{fileName}")]
    public IActionResult GetFileByName(string fileName)
    {
        var filePath = _docService.FindPdfFile(fileName);
        if (string.IsNullOrEmpty(filePath) || !System.IO.File.Exists(filePath))
        {
            return NotFound(new { error = $"PDF file '{fileName}' was not found on the server." });
        }

        var name = Path.GetFileName(filePath);
        Response.Headers.Append("Content-Disposition", $"inline; filename=\"{name}\"");
        return PhysicalFile(filePath, "application/pdf", enableRangeProcessing: true);
    }

    /// <summary>
    /// GET /api/documents/chunks - Return all document chunks as structured JSON
    /// </summary>
    [HttpGet("chunks")]
    public async Task<IActionResult> GetAllChunksJson([FromQuery] bool download = false)
    {
        var data = await _docService.GetChunksJsonAsync();
        if (download)
        {
            var json = System.Text.Json.JsonSerializer.Serialize(data, new System.Text.Json.JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase
            });
            var bytes = System.Text.Encoding.UTF8.GetBytes(json);
            return File(bytes, "application/json", "chunks.json");
        }
        return Ok(data);
    }
}

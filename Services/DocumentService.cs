using System.Text.Json;
using System.Text.RegularExpressions;
using KnowledgeAssistant.Api.Data;
using KnowledgeAssistant.Api.Models;

namespace KnowledgeAssistant.Api.Services;

public class DocumentService : IDocumentService
{
    private readonly IDocumentStore _documentStore;
    private readonly IEmbeddingService _embeddingService;
    private readonly IVectorStore _vectorStore;
    private readonly IIngestionQueue _ingestionQueue;
    private readonly IPdfExtractionService _pdfExtractor;
    private readonly IWebHostEnvironment _env;
    private readonly ILogger<DocumentService> _logger;

    public DocumentService(
        IDocumentStore documentStore,
        IEmbeddingService embeddingService,
        IVectorStore vectorStore,
        IIngestionQueue ingestionQueue,
        IPdfExtractionService pdfExtractor,
        IWebHostEnvironment env,
        ILogger<DocumentService> logger)
    {
        _documentStore = documentStore;
        _embeddingService = embeddingService;
        _vectorStore = vectorStore;
        _ingestionQueue = ingestionQueue;
        _pdfExtractor = pdfExtractor;
        _env = env;
        _logger = logger;
    }

    public async Task<Document> IngestDocumentAsync(DocumentUploadRequest request)
    {
        var doc = new Document
        {
            Title = request.Title,
            FileName = string.IsNullOrWhiteSpace(request.FileName) ? $"{request.Title.ToLowerInvariant().Replace(" ", "-")}.md" : request.FileName,
            Source = request.Source,
            Department = request.Department,
            Version = request.Version,
            Content = request.Content,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            IsActive = true
        };

        await _documentStore.AddOrUpdateDocumentAsync(doc);

        // Enqueue async ingestion for chunking & embedding
        await _ingestionQueue.EnqueueAsync(doc.Id, doc.Title);

        return doc;
    }

    public async Task<Document> IngestPdfStreamAsync(
        Stream stream,
        string fileName,
        string title,
        string department,
        string version)
    {
        var extractedPages = _pdfExtractor.ExtractPages(stream);
        if (extractedPages.Count == 0)
        {
            throw new InvalidOperationException("No readable text found in PDF document.");
        }

        // Save physical PDF file to backend/documents
        var documentsFolder = Path.Combine(Directory.GetCurrentDirectory(), "documents");
        if (!Directory.Exists(documentsFolder))
        {
            Directory.CreateDirectory(documentsFolder);
        }

        var savedFilePath = Path.Combine(documentsFolder, fileName);
        if (stream.CanSeek)
        {
            stream.Position = 0;
            using var fileOut = File.Create(savedFilePath);
            await stream.CopyToAsync(fileOut);
        }

        // Format document content with page boundary tags
        var fullContent = string.Join("\n\n", extractedPages.Select(p => $"<!-- Page {p.PageNumber} -->\n{p.Text}"));

        var doc = new Document
        {
            Title = string.IsNullOrWhiteSpace(title) ? Path.GetFileNameWithoutExtension(fileName) : title,
            FileName = fileName,
            Source = "PDF Upload",
            Department = department,
            Version = string.IsNullOrWhiteSpace(version) ? "1.0" : version,
            Content = fullContent,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            IsActive = true
        };

        await _documentStore.AddOrUpdateDocumentAsync(doc);

        // Enqueue async ingestion for chunking & embedding
        await _ingestionQueue.EnqueueAsync(doc.Id, doc.Title);

        return doc;
    }

    public async Task<List<Document>> ImportPdfsFromFolderAsync(string? folderPath = null)
    {
        var targetFolder = folderPath;
        if (string.IsNullOrWhiteSpace(targetFolder))
        {
            targetFolder = Path.Combine(Directory.GetCurrentDirectory(), "documents");
        }

        if (!Directory.Exists(targetFolder))
        {
            Directory.CreateDirectory(targetFolder);
            _logger.LogInformation("Created documents folder at {Path}", targetFolder);
            return new List<Document>();
        }

        var pdfFiles = Directory.GetFiles(targetFolder, "*.pdf", SearchOption.AllDirectories);
        var importedDocs = new List<Document>();

        foreach (var pdfPath in pdfFiles)
        {
            try
            {
                var fileName = Path.GetFileName(pdfPath);
                
                // Check if already in store
                var existing = await _documentStore.FindDocumentByFileNameAsync(fileName);
                if (existing != null)
                {
                    _logger.LogInformation("Skipping already imported document {FileName}", fileName);
                    continue;
                }

                using var fileStream = File.OpenRead(pdfPath);
                var docTitle = Path.GetFileNameWithoutExtension(fileName).Replace("-", " ").Replace("_", " ");
                var doc = await IngestPdfStreamAsync(
                    fileStream,
                    fileName,
                    docTitle,
                    "General",
                    "1.0");

                importedDocs.Add(doc);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to import PDF file {Path}", pdfPath);
            }
        }

        return importedDocs;
    }

    public async Task<bool> DeleteDocumentAsync(string id)
    {
        var doc = await _documentStore.GetDocumentByIdAsync(id);
        if (doc == null) return false;

        // Remove chunks from vector store
        await _vectorStore.DeleteDocumentChunksAsync(id);

        await _documentStore.DeleteDocumentAsync(id);
        await ExportChunksToJsonFileAsync();

        _logger.LogInformation("Successfully deleted document {DocId} ({Title}) and its chunks", id, doc.Title);
        return true;
    }

    public async Task<int> PurgeAllDocumentsAsync()
    {
        var docs = await _documentStore.GetAllDocumentsAsync();
        int count = docs.Count;

        foreach (var doc in docs)
        {
            await _vectorStore.DeleteDocumentChunksAsync(doc.Id);
        }

        await _documentStore.PurgeAllAsync();
        await ExportChunksToJsonFileAsync();

        _logger.LogInformation("Purged all {Count} documents and vector chunks from knowledge base", count);
        return count;
    }

    public async Task<List<DocumentResponseDto>> GetDocumentsAsync()
    {
        var allDocs = await _documentStore.GetAllDocumentsAsync();
        var docs = allDocs.Where(d => d.IsActive).OrderByDescending(d => d.UpdatedAt).ToList();

        return docs.Select(d => new DocumentResponseDto
        {
            Id = d.Id,
            Title = d.Title,
            FileName = d.FileName,
            Source = d.Source,
            Department = d.Department,
            Version = d.Version,
            ChunkCount = d.Chunks.Count,
            IsActive = d.IsActive,
            CreatedAt = d.CreatedAt,
            UpdatedAt = d.UpdatedAt
        }).ToList();
    }

    public async Task<Document?> GetDocumentAsync(string id)
    {
        var doc = await _documentStore.GetDocumentByIdAsync(id);
        if (doc == null || !doc.IsActive) return null;

        return doc;
    }

    public async Task<bool> ReindexDocumentAsync(string id)
    {
        var doc = await _documentStore.GetDocumentByIdAsync(id);
        if (doc == null) return false;

        await _ingestionQueue.EnqueueAsync(doc.Id, doc.Title);
        return true;
    }

    public string? GetDocumentFilePath(string id)
    {
        var doc = _documentStore.GetDocumentByIdAsync(id).GetAwaiter().GetResult();
        if (doc == null || string.IsNullOrWhiteSpace(doc.FileName)) return null;

        return FindPdfFile(doc.FileName);
    }

    public string? FindPdfFile(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName)) return null;

        var documentsFolder = Path.Combine(Directory.GetCurrentDirectory(), "documents");
        var directPath = Path.Combine(documentsFolder, fileName);
        if (File.Exists(directPath)) return directPath;

        if (Directory.Exists(documentsFolder))
        {
            var match = Directory.GetFiles(documentsFolder, fileName, SearchOption.AllDirectories).FirstOrDefault();
            if (match != null) return match;
        }

        var parentPath = Path.Combine(Directory.GetCurrentDirectory(), fileName);
        if (File.Exists(parentPath)) return parentPath;

        var grandParentPath = Path.Combine(Directory.GetCurrentDirectory(), "..", fileName);
        if (File.Exists(grandParentPath)) return grandParentPath;

        // Try fuzzy match on title / filename without extension
        var nameWithoutExt = Path.GetFileNameWithoutExtension(fileName);
        if (Directory.Exists(documentsFolder))
        {
            var fuzzyMatch = Directory.GetFiles(documentsFolder, "*.pdf", SearchOption.AllDirectories)
                .FirstOrDefault(f => Path.GetFileNameWithoutExtension(f).Replace("-", " ").Trim()
                    .Equals(nameWithoutExt.Replace("-", " ").Trim(), StringComparison.OrdinalIgnoreCase));
            if (fuzzyMatch != null) return fuzzyMatch;
        }

        return null;
    }

    public async Task ProcessDocumentChunkingAndIndexingAsync(string documentId)
    {
        var doc = await _documentStore.GetDocumentByIdAsync(documentId);
        if (doc == null || string.IsNullOrWhiteSpace(doc.Content)) return;

        // Extract page segments if present
        var pageSegments = ParsePagesFromContent(doc.Content);

        // Clear existing chunks in store & vector store
        await _documentStore.RemoveChunksForDocumentAsync(documentId);
        await _vectorStore.DeleteDocumentChunksAsync(documentId);

        var chunksToInsert = new List<DocumentChunk>();
        int chunkGlobalIndex = 1;

        foreach (var (pageNumber, pageText) in pageSegments)
        {
            var normalized = NormalizeText(pageText);
            if (string.IsNullOrWhiteSpace(normalized)) continue;

            var rawChunks = CreateChunks(normalized, targetTokenSize: 65, overlapTokens: 12);

            for (int i = 0; i < rawChunks.Count; i++)
            {
                var chunkText = rawChunks[i];
                var embedding = await _embeddingService.GenerateEmbeddingAsync(chunkText);

                var chunk = new DocumentChunk
                {
                    DocumentId = documentId,
                    Document = doc,
                    ChunkText = chunkText,
                    ChunkIndex = chunkGlobalIndex++,
                    PageNumber = pageNumber,
                    TokenCount = EstimateTokenCount(chunkText),
                    Embedding = embedding,
                    CreatedAt = DateTime.UtcNow
                };

                chunksToInsert.Add(chunk);
            }
        }

        await _documentStore.SetChunksForDocumentAsync(documentId, chunksToInsert);
        doc.UpdatedAt = DateTime.UtcNow;

        // Index chunks in vector store
        await _vectorStore.IndexChunksAsync(chunksToInsert);
        _logger.LogInformation("Processed and indexed {Count} chunks for document {Title} ({Id})", chunksToInsert.Count, doc.Title, doc.Id);

        // Export/update chunks.json on disk
        await ExportChunksToJsonFileAsync();
    }

    private static List<(int PageNumber, string PageText)> ParsePagesFromContent(string content)
    {
        var result = new List<(int, string)>();
        var pageRegex = new Regex(@"<!-- Page (\d+) -->", RegexOptions.Compiled);
        var matches = pageRegex.Matches(content);

        if (matches.Count == 0)
        {
            result.Add((1, content));
            return result;
        }

        for (int i = 0; i < matches.Count; i++)
        {
            int pageNum = int.Parse(matches[i].Groups[1].Value);
            int startIndex = matches[i].Index + matches[i].Length;
            int endIndex = (i + 1 < matches.Count) ? matches[i + 1].Index : content.Length;
            
            var text = content.Substring(startIndex, endIndex - startIndex).Trim();
            if (!string.IsNullOrWhiteSpace(text))
            {
                result.Add((pageNum, text));
            }
        }

        return result.Count > 0 ? result : new List<(int, string)> { (1, content) };
    }

    private static string NormalizeText(string text)
    {
        var normalized = Regex.Replace(text, @"\r\n|\r", "\n");
        normalized = Regex.Replace(normalized, @"\n{3,}", "\n\n");
        return normalized.Trim();
    }

    private static List<string> CreateChunks(string text, int targetTokenSize = 65, int overlapTokens = 12)
    {
        var clean = text.Trim();
        if (string.IsNullOrWhiteSpace(clean)) return new List<string>();

        // Split on sentences, bullet points, and newlines
        var sentences = Regex.Split(clean, @"(?<=[.!?])\s+|(?=[\u27A2\u2756\u2705\u2022\u25C6\u279C\u27A4\u25CF\u25AA\u25BA\u2714\u2713\u2740\u2023\u2043\u2219•])|\n+")
            .Select(s => s.Trim())
            .Where(s => s.Length > 0)
            .ToList();

        var units = new List<string>();
        foreach (var s in sentences)
        {
            if (EstimateTokenCount(s) > 85)
            {
                var words = s.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                var sub = new List<string>();
                int subTokens = 0;
                foreach (var w in words)
                {
                    int wt = EstimateTokenCount(w + " ");
                    if (subTokens + wt > targetTokenSize && sub.Count > 0)
                    {
                        units.Add(string.Join(" ", sub));
                        sub.Clear();
                        subTokens = 0;
                    }
                    sub.Add(w);
                    subTokens += wt;
                }
                if (sub.Count > 0)
                {
                    units.Add(string.Join(" ", sub));
                }
            }
            else
            {
                units.Add(s);
            }
        }

        var chunks = new List<string>();
        var currentChunk = new List<string>();
        int currentTokenCount = 0;

        foreach (var u in units)
        {
            int uTokens = EstimateTokenCount(u);
            if (currentTokenCount + uTokens > targetTokenSize && currentChunk.Count > 0)
            {
                chunks.Add(string.Join(" ", currentChunk));

                var last = currentChunk.Last();
                currentChunk.Clear();
                if (EstimateTokenCount(last) <= overlapTokens)
                {
                    currentChunk.Add(last);
                    currentTokenCount = EstimateTokenCount(last);
                }
                else
                {
                    currentTokenCount = 0;
                }
            }

            currentChunk.Add(u);
            currentTokenCount += uTokens;
        }

        if (currentChunk.Count > 0)
        {
            chunks.Add(string.Join(" ", currentChunk));
        }

        return chunks.Count > 0 ? chunks : new List<string> { text };
    }

    private static int EstimateTokenCount(string text)
    {
        return (int)Math.Ceiling(text.Length / 4.0);
    }

    public async Task<object> GetChunksJsonAsync()
    {
        var chunks = await _documentStore.GetAllChunksAsync();
        var allDocs = await _documentStore.GetAllDocumentsAsync();

        var sortedChunks = chunks
            .OrderBy(c => c.Document != null ? c.Document.Title : "")
            .ThenBy(c => c.PageNumber)
            .ThenBy(c => c.ChunkIndex)
            .ToList();

        var documentCount = allDocs.Count;

        var chunkItems = sortedChunks.Select(c => new
        {
            id = c.Id,
            documentId = c.DocumentId,
            documentTitle = c.Document?.Title ?? "Unknown Document",
            fileName = c.Document?.FileName ?? "",
            department = c.Document?.Department ?? "General",
            chunkIndex = c.ChunkIndex,
            pageNumber = c.PageNumber,
            tokenCount = c.TokenCount,
            createdAt = c.CreatedAt.ToString("o"),
            chunkText = c.ChunkText,
            vector = c.Embedding ?? Array.Empty<float>()
        }).ToList();

        return new
        {
            generatedAt = DateTime.UtcNow.ToString("o"),
            totalDocuments = documentCount,
            totalChunks = sortedChunks.Count,
            chunks = chunkItems
        };
    }

    public async Task<string> ExportChunksToJsonFileAsync()
    {
        try
        {
            var data = await GetChunksJsonAsync();
            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };
            var json = JsonSerializer.Serialize(data, options);

            // 1. Save in backend directory
            var backendDir = Directory.GetCurrentDirectory();
            var backendFilePath = Path.Combine(backendDir, "chunks.json");
            await File.WriteAllTextAsync(backendFilePath, json);

            // 2. Also save in wwwroot directory for static access and direct downloads
            var wwwrootDir = Path.Combine(backendDir, "wwwroot");
            if (Directory.Exists(wwwrootDir))
            {
                var wwwrootFilePath = Path.Combine(wwwrootDir, "chunks.json");
                await File.WriteAllTextAsync(wwwrootFilePath, json);
            }

            _logger.LogInformation("Exported chunks JSON file with all document chunks to {Path}", backendFilePath);
            return backendFilePath;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to export chunks.json file");
            return string.Empty;
        }
    }
}

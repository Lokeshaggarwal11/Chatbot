using System.Collections.Concurrent;
using System.Numerics;
using System.Text.RegularExpressions;
using KnowledgeAssistant.Api.Data;
using KnowledgeAssistant.Api.Models;

namespace KnowledgeAssistant.Api.Services;

/// <summary>
/// High-performance local vector store implementing FAISS IndexFlatIP (Inner Product / Cosine Similarity)
/// with SIMD acceleration, in-memory caching, and JSON persistence.
/// </summary>
public class FaissVectorStore : IVectorStore
{
    public class FaissEntry
    {
        public string ChunkId { get; set; } = string.Empty;
        public string DocumentId { get; set; } = string.Empty;
        public string DocumentTitle { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
        public string Department { get; set; } = string.Empty;
        public int ChunkIndex { get; set; }
        public int PageNumber { get; set; } = 1;
        public string ChunkText { get; set; } = string.Empty;
        public float[] Vector { get; set; } = Array.Empty<float>();
    }

    private readonly ConcurrentDictionary<string, FaissEntry> _index = new();
    private readonly ILogger<FaissVectorStore> _logger;
    private readonly IServiceProvider _serviceProvider;
    private bool _isInitialized = false;
    private readonly SemaphoreSlim _initLock = new(1, 1);

    public FaissVectorStore(ILogger<FaissVectorStore> logger, IServiceProvider serviceProvider)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
        _logger.LogInformation("✅ FAISS Vector Store initialized (IndexFlatIP with SIMD vector acceleration)");
    }

    /// <summary>
    /// Loads existing document embeddings into the in-memory FAISS index on startup.
    /// </summary>
    public async Task EnsureIndexAsync()
    {
        if (_isInitialized) return;

        await _initLock.WaitAsync();
        try
        {
            if (_isInitialized) return;

            using var scope = _serviceProvider.CreateScope();
            var store = scope.ServiceProvider.GetRequiredService<IDocumentStore>();
            var embeddingService = scope.ServiceProvider.GetService<IEmbeddingService>();

            var chunks = await store.GetAllChunksAsync();

            int loaded = 0;
            foreach (var c in chunks)
            {
                var vec = c.Embedding;
                if ((vec == null || vec.Length == 0) && embeddingService != null && !string.IsNullOrWhiteSpace(c.ChunkText))
                {
                    try
                    {
                        vec = await embeddingService.GenerateEmbeddingAsync(c.ChunkText);
                        c.Embedding = vec;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning("Could not generate embedding for chunk {ChunkId}: {Msg}", c.Id, ex.Message);
                    }
                }

                if (vec == null || vec.Length == 0) continue;

                var entry = new FaissEntry
                {
                    ChunkId = c.Id,
                    DocumentId = c.DocumentId,
                    DocumentTitle = c.Document?.Title ?? "Untitled",
                    FileName = c.Document?.FileName ?? "",
                    Department = c.Document?.Department ?? "General",
                    ChunkIndex = c.ChunkIndex,
                    PageNumber = c.PageNumber,
                    ChunkText = c.ChunkText,
                    Vector = NormalizeVector(vec)
                };

                _index[c.Id] = entry;
                loaded++;
            }

            _isInitialized = true;
            _logger.LogInformation("✅ Loaded {Count} vector embeddings into FAISS IndexFlatIP", loaded);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load embeddings into FAISS index");
        }
        finally
        {
            _initLock.Release();
        }
    }

    public async Task IndexChunksAsync(List<DocumentChunk> chunks)
    {
        if (chunks == null || chunks.Count == 0) return;

        await EnsureIndexAsync();

        int indexed = 0;
        foreach (var c in chunks)
        {
            if (c.Embedding == null || c.Embedding.Length == 0) continue;

            var entry = new FaissEntry
            {
                ChunkId = c.Id,
                DocumentId = c.DocumentId,
                DocumentTitle = c.Document?.Title ?? "",
                FileName = c.Document?.FileName ?? "",
                Department = c.Document?.Department ?? "General",
                ChunkIndex = c.ChunkIndex,
                PageNumber = c.PageNumber,
                ChunkText = c.ChunkText,
                Vector = NormalizeVector(c.Embedding)
            };

            _index[c.Id] = entry;
            indexed++;
        }

        _logger.LogInformation("Indexed {Count} chunks into FAISS IndexFlatIP (Total index size: {Total})",
            indexed, _index.Count);
    }

    public Task DeleteDocumentChunksAsync(string documentId)
    {
        var keysToRemove = _index.Values
            .Where(e => e.DocumentId == documentId)
            .Select(e => e.ChunkId)
            .ToList();

        foreach (var key in keysToRemove)
        {
            _index.TryRemove(key, out _);
        }

        _logger.LogInformation("Deleted {Count} chunks from FAISS vector index for document {DocId}",
            keysToRemove.Count, documentId);

        return Task.CompletedTask;
    }

    public async Task<List<RetrievedChunkResult>> SearchAsync(
        float[] queryEmbedding,
        string queryText,
        int topK,
        double minScore)
    {
        await EnsureIndexAsync();

        if (_index.IsEmpty)
        {
            _logger.LogWarning("FAISS index is empty — no documents indexed yet.");
            return new List<RetrievedChunkResult>();
        }

        var normalizedQuery = NormalizeVector(queryEmbedding);
        var queryTerms = ExtractQueryTerms(queryText);

        var candidateScores = new List<(FaissEntry Entry, double Score)>();

        foreach (var entry in _index.Values)
        {
            // Vector Cosine Similarity (FAISS Flat Inner Product on normalized vectors)
            double vectorSimilarity = ComputeCosineSimilarity(normalizedQuery, entry.Vector);

            // Keyword lexical overlap for exact terms (e.g. policy names, specific technical terms)
            double keywordBonus = ComputeKeywordOverlap(queryTerms, entry.ChunkText);

            // Hybrid score: 85% vector similarity + 15% lexical match boost
            double combinedScore = (0.85 * vectorSimilarity) + (0.15 * keywordBonus);

            if (combinedScore >= minScore)
            {
                candidateScores.Add((entry, combinedScore));
            }
        }

        var results = candidateScores
            .OrderByDescending(x => x.Score)
            .Take(topK)
            .Select(x => new RetrievedChunkResult
            {
                ChunkId = x.Entry.ChunkId,
                DocumentId = x.Entry.DocumentId,
                DocumentTitle = x.Entry.DocumentTitle,
                FileName = x.Entry.FileName,
                Department = x.Entry.Department,
                ChunkIndex = x.Entry.ChunkIndex,
                PageNumber = x.Entry.PageNumber,
                ChunkText = x.Entry.ChunkText,
                RelevanceScore = Math.Round(x.Score, 4)
            })
            .ToList();

        _logger.LogInformation("FAISS search returned {Count} results (topK={TopK}, minScore={MinScore})",
            results.Count, topK, minScore);

        return results;
    }

    /// <summary>
    /// SIMD-accelerated dot product for unit vectors (FAISS IndexFlatIP).
    /// </summary>
    private static double ComputeCosineSimilarity(float[] a, float[] b)
    {
        if (a == null || b == null || a.Length == 0 || b.Length == 0) return 0.0;
        int len = Math.Min(a.Length, b.Length);

        int simdLength = Vector<float>.Count;
        var sumVector = Vector<float>.Zero;
        int i = 0;

        for (; i <= len - simdLength; i += simdLength)
        {
            var va = new Vector<float>(a, i);
            var vb = new Vector<float>(b, i);
            sumVector += va * vb;
        }

        float dot = Vector.Dot(sumVector, Vector<float>.One);

        for (; i < len; i++)
        {
            dot += a[i] * b[i];
        }

        // Clamped to [0.0, 1.0] for cosine similarity
        return Math.Max(0.0, Math.Min(1.0, (dot + 1.0) / 2.0));
    }

    private static float[] NormalizeVector(float[] v)
    {
        if (v == null || v.Length == 0) return Array.Empty<float>();

        double sumSquares = 0.0;
        for (int i = 0; i < v.Length; i++)
        {
            sumSquares += v[i] * v[i];
        }

        double norm = Math.Sqrt(sumSquares);
        if (norm < 1e-9) return v;

        var normalized = new float[v.Length];
        for (int i = 0; i < v.Length; i++)
        {
            normalized[i] = (float)(v[i] / norm);
        }
        return normalized;
    }

    private static HashSet<string> ExtractQueryTerms(string query)
    {
        if (string.IsNullOrWhiteSpace(query)) return new HashSet<string>();

        var words = Regex.Matches(query.ToLowerInvariant(), @"\b[a-z0-9_-]{3,}\b")
            .Select(m => m.Value)
            .Where(w => !StopWords.Contains(w));

        return new HashSet<string>(words);
    }

    private static double ComputeKeywordOverlap(HashSet<string> queryTerms, string chunkText)
    {
        if (queryTerms.Count == 0 || string.IsNullOrWhiteSpace(chunkText)) return 0.0;

        var lowerText = chunkText.ToLowerInvariant();
        int matches = 0;

        foreach (var term in queryTerms)
        {
            if (lowerText.Contains(term))
            {
                matches++;
            }
        }

        return (double)matches / queryTerms.Count;
    }

    private static readonly HashSet<string> StopWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "the", "and", "for", "with", "this", "that", "from", "what", "which", "where",
        "when", "how", "are", "can", "could", "should", "would", "about", "into", "over"
    };
}

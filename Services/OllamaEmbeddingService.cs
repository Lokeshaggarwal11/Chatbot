using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using KnowledgeAssistant.Api.Services;

namespace KnowledgeAssistant.Api.Services;

public class OllamaEmbeddingService : IEmbeddingService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<OllamaEmbeddingService> _logger;
    private readonly string _endpoint;
    private readonly string _modelName;

    public OllamaEmbeddingService(IConfiguration configuration, ILogger<OllamaEmbeddingService> logger, HttpClient httpClient)
    {
        _logger = logger;
        _httpClient = httpClient;
        _endpoint = configuration["Ollama:Endpoint"]?.TrimEnd('/') ?? "http://localhost:11434";
        _modelName = configuration["Ollama:EmbeddingModel"] ?? "bge-small-en-v1.5";
        _httpClient.BaseAddress = new Uri(_endpoint);
        _httpClient.Timeout = TimeSpan.FromSeconds(120);

        _logger.LogInformation("OllamaEmbeddingService initialized with endpoint: {Endpoint}, model: {Model}", _endpoint, _modelName);
    }

    public async Task<float[]> GenerateEmbeddingAsync(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return Array.Empty<float>();
        }

        try
        {
            // Primary method: /api/embed (Ollama >= 0.1.34)
            var embedPayload = new { model = _modelName, input = text };
            var response = await _httpClient.PostAsJsonAsync("/api/embed", embedPayload);

            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<OllamaEmbedResponse>();
                if (result?.Embeddings != null && result.Embeddings.Count > 0)
                {
                    return result.Embeddings[0];
                }
            }

            // Fallback method: /api/embeddings (legacy endpoint)
            var legacyPayload = new { model = _modelName, prompt = text };
            var legacyResponse = await _httpClient.PostAsJsonAsync("/api/embeddings", legacyPayload);
            if (legacyResponse.IsSuccessStatusCode)
            {
                var legacyResult = await legacyResponse.Content.ReadFromJsonAsync<OllamaLegacyEmbedResponse>();
                if (legacyResult?.Embedding != null && legacyResult.Embedding.Length > 0)
                {
                    return legacyResult.Embedding;
                }
            }

            var error = await response.Content.ReadAsStringAsync();
            throw new InvalidOperationException($"Ollama embedding failed for model '{_modelName}' at {_endpoint}: {response.StatusCode} - {error}. Please ensure model is pulled: `ollama pull {_modelName}`");
        }
        catch (Exception ex) when (ex is not InvalidOperationException)
        {
            _logger.LogError(ex, "Failed to call Ollama embedding API at {Endpoint} for model {Model}", _endpoint, _modelName);
            throw new InvalidOperationException($"Cannot connect to Ollama embedding service at {_endpoint}. Please ensure Ollama is running and model '{_modelName}' is pulled (`ollama pull {_modelName}`). Details: {ex.Message}", ex);
        }
    }

    public async Task<List<float[]>> GenerateEmbeddingsAsync(List<string> texts)
    {
        if (texts == null || texts.Count == 0)
        {
            return new List<float[]>();
        }

        try
        {
            // Try batch embed API
            var embedPayload = new { model = _modelName, input = texts };
            var response = await _httpClient.PostAsJsonAsync("/api/embed", embedPayload);

            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<OllamaEmbedResponse>();
                if (result?.Embeddings != null && result.Embeddings.Count == texts.Count)
                {
                    return result.Embeddings;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Batch embedding failed with Ollama /api/embed, falling back to sequential embedding.");
        }

        // Sequential fallback
        var list = new List<float[]>(texts.Count);
        foreach (var text in texts)
        {
            var emb = await GenerateEmbeddingAsync(text);
            list.Add(emb);
        }
        return list;
    }

    private class OllamaEmbedResponse
    {
        [JsonPropertyName("model")]
        public string? Model { get; set; }

        [JsonPropertyName("embeddings")]
        public List<float[]>? Embeddings { get; set; }
    }

    private class OllamaLegacyEmbedResponse
    {
        [JsonPropertyName("embedding")]
        public float[]? Embedding { get; set; }
    }
}

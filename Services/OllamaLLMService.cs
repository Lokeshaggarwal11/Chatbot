using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using KnowledgeAssistant.Api.Models;
using KnowledgeAssistant.Api.Services;

namespace KnowledgeAssistant.Api.Services;

public class OllamaLLMService : ILLMService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<OllamaLLMService> _logger;
    private readonly string _endpoint;
    private readonly string _modelName;
    private readonly double _temperature;
    private readonly int _contextWindow;

    public OllamaLLMService(IConfiguration configuration, ILogger<OllamaLLMService> logger, HttpClient httpClient)
    {
        _logger = logger;
        _httpClient = httpClient;
        _endpoint = configuration["Ollama:Endpoint"]?.TrimEnd('/') ?? "http://localhost:11434";
        _modelName = configuration["Ollama:LLMModel"] ?? "qwen2.5:1.5b";
        _temperature = configuration.GetValue<double>("Ollama:Temperature", 0.1);
        _contextWindow = configuration.GetValue<int>("Ollama:ContextWindow", 4096);

        _httpClient.BaseAddress = new Uri(_endpoint);
        _httpClient.Timeout = TimeSpan.FromSeconds(180);

        _logger.LogInformation("OllamaLLMService initialized with endpoint: {Endpoint}, model: {Model}, ctx: {ContextWindow}",
            _endpoint, _modelName, _contextWindow);
    }

    public async Task<string> GenerateGroundedAnswerAsync(
        string question,
        List<RetrievedChunkResult> chunks)
    {
        if (string.IsNullOrWhiteSpace(question))
        {
            return "Please provide a valid question.";
        }

        if (chunks == null || chunks.Count == 0)
        {
            return "No relevant information found in the knowledge base.";
        }

        var systemPrompt = BuildSystemPrompt();
        var userPrompt = BuildUserPrompt(question, chunks);

        try
        {
            var payload = new OllamaChatRequest
            {
                Model = _modelName,
                Messages = new List<OllamaChatMessage>
                {
                    new OllamaChatMessage { Role = "system", Content = systemPrompt },
                    new OllamaChatMessage { Role = "user", Content = userPrompt }
                },
                Stream = false,
                Options = new OllamaChatOptions
                {
                    Temperature = _temperature,
                    NumCtx = _contextWindow
                }
            };

            var response = await _httpClient.PostAsJsonAsync("/api/chat", payload);

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                throw new InvalidOperationException($"Ollama chat generation failed for model '{_modelName}' at {_endpoint}: {response.StatusCode} - {error}. Please verify the model is pulled (`ollama pull {_modelName}`).");
            }

            var result = await response.Content.ReadFromJsonAsync<OllamaChatResponse>();
            var answer = result?.Message?.Content?.Trim();

            if (string.IsNullOrWhiteSpace(answer))
            {
                return "No answer could be generated from the available knowledge base context.";
            }

            return answer;
        }
        catch (Exception ex) when (ex is not InvalidOperationException)
        {
            _logger.LogError(ex, "Failed to call Ollama chat API at {Endpoint} for model {Model}", _endpoint, _modelName);
            throw new InvalidOperationException($"Cannot connect to Ollama service at {_endpoint}. Please verify Ollama is running and model '{_modelName}' is pulled (`ollama pull {_modelName}`). Details: {ex.Message}", ex);
        }
    }

    private static string BuildSystemPrompt()
    {
        return """
            You are an enterprise Knowledge Assistant.
            RULES:
            1. Answer accurately using ONLY the context provided.
            2. Do NOT hallucinate facts not in the context.
            3. Provide ONLY the direct, clear answer to the question. Do NOT include document names, source citations, page numbers, or references.
            4. If the context lacks information to answer the question, reply: "No relevant information found in the knowledge base."
            5. Keep answers concise, direct, and well-structured.
            """;
    }

    private static string BuildUserPrompt(string question, List<RetrievedChunkResult> chunks)
    {
        var contextBuilder = new System.Text.StringBuilder();
        for (int i = 0; i < chunks.Count; i++)
        {
            var chunk = chunks[i];
            contextBuilder.AppendLine($"{chunk.ChunkText.Trim()}");
        }

        return $"""
            CONTEXT:
            {contextBuilder}

            QUESTION: {question}

            Provide a concise, direct answer based strictly on the context above. Output only the answer without source citations, document titles, or page numbers.
            """;
    }

    private class OllamaChatRequest
    {
        [JsonPropertyName("model")]
        public string Model { get; set; } = string.Empty;

        [JsonPropertyName("messages")]
        public List<OllamaChatMessage> Messages { get; set; } = new();

        [JsonPropertyName("stream")]
        public bool Stream { get; set; } = false;

        [JsonPropertyName("options")]
        public OllamaChatOptions? Options { get; set; }
    }

    private class OllamaChatMessage
    {
        [JsonPropertyName("role")]
        public string Role { get; set; } = string.Empty;

        [JsonPropertyName("content")]
        public string Content { get; set; } = string.Empty;
    }

    private class OllamaChatOptions
    {
        [JsonPropertyName("temperature")]
        public double Temperature { get; set; } = 0.1;

        [JsonPropertyName("num_ctx")]
        public int NumCtx { get; set; } = 4096;
    }

    private class OllamaChatResponse
    {
        [JsonPropertyName("model")]
        public string? Model { get; set; }

        [JsonPropertyName("message")]
        public OllamaChatMessage? Message { get; set; }

        [JsonPropertyName("done")]
        public bool Done { get; set; }
    }
}

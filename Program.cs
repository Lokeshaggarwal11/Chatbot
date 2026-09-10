using System.Text.Json.Serialization;
using KnowledgeAssistant.Api.Data;
using KnowledgeAssistant.Api.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers().AddJsonOptions(options =>
{
    options.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
});
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// In-Memory Document & Chunk Store (Zero-SQL, backed by chunks.json)
builder.Services.AddSingleton<IDocumentStore, InMemoryDocumentStore>();

// CORS for Frontend
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

// ─── Local AI Stack: Ollama + FAISS ───
// Embeddings: BAAI/bge-small-en-v1.5 via Ollama
builder.Services.AddHttpClient<IEmbeddingService, OllamaEmbeddingService>();

// Vector Store: FAISS IndexFlatIP (SIMD cosine similarity, in-memory + JSON persistence)
builder.Services.AddSingleton<FaissVectorStore>();
builder.Services.AddSingleton<IVectorStore>(sp => sp.GetRequiredService<FaissVectorStore>());

// LLM: Qwen 2.5 (1.5B) via Ollama
builder.Services.AddHttpClient<ILLMService, OllamaLLMService>();

// Background Ingestion Pipeline
builder.Services.AddSingleton<IIngestionQueue, IngestionQueue>();
builder.Services.AddHostedService<BackgroundIngestionWorker>();

// Application Services
builder.Services.AddScoped<IPdfExtractionService, PdfExtractionService>();
builder.Services.AddScoped<IDocumentService, DocumentService>();
builder.Services.AddScoped<IRAGService, RAGService>();
builder.Services.AddScoped<IAuditService, AuditService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "Internal Knowledge Assistant v1");
    c.RoutePrefix = "swagger";
});

app.UseCors("AllowAll");
app.UseDefaultFiles();
app.UseStaticFiles();
app.MapControllers();

// Initialize In-Memory Store, FAISS index, and chunks.json
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        await SeedData.InitializeAsync(services);
    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "An error occurred during knowledge store initialization.");
    }
}

app.Run();

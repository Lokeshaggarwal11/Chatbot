using KnowledgeAssistant.Api.Models;
using KnowledgeAssistant.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace KnowledgeAssistant.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ChatController : ControllerBase
{
    private readonly IRAGService _ragService;
    private readonly ILogger<ChatController> _logger;

    public ChatController(IRAGService ragService, ILogger<ChatController> logger)
    {
        _ragService = ragService;
        _logger = logger;
    }

    /// <summary>
    /// POST /api/chat - Ask a knowledge question with authorization-aware RAG (stateless)
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<ChatResponse>> AskQuestion([FromBody] ChatRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Question))
        {
            return BadRequest(new { error = "Question cannot be empty" });
        }

        var response = await _ragService.ExecuteQueryAsync(request);
        return Ok(response);
    }
}

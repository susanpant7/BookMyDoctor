using Microsoft.AspNetCore.Mvc;

namespace BookMyDoctor.Features.Knowledge;

// Development-only endpoints for inspecting what the RAG pipeline sees.
[ApiController]
[Route("api/knowledge")]
public sealed class KnowledgeController : ControllerBase
{
    private readonly KnowledgeSources _sources;
    private readonly IEmbedder _embedder;
    private readonly IVectorStore _store;
    private readonly IKnowledgeRetriever _retriever;
    private readonly IWebHostEnvironment _environment;

    public KnowledgeController(
        KnowledgeSources sources,
        IEmbedder embedder,
        IVectorStore store,
        IKnowledgeRetriever retriever,
        IWebHostEnvironment environment)
    {
        _sources = sources;
        _embedder = embedder;
        _store = store;
        _retriever = retriever;
        _environment = environment;
    }

    // Shows which chunks a question retrieves, with their similarity scores.
    [HttpGet("search")]
    public async Task<IActionResult> Search([FromQuery] string q, CancellationToken cancellationToken)
    {
        if (!_environment.IsDevelopment())
        {
            return NotFound();
        }

        var results = await _retriever.SearchAsync(q, cancellationToken);
        return Ok(new
        {
            query = q,
            indexedDocuments = _store.Count,
            results = results.Select(result => new { score = Math.Round(result.Score, 3), result.Document.Id, result.Document.Text })
        });
    }

    [HttpGet("documents")]
    public ActionResult<IReadOnlyList<KnowledgeDocument>> GetDocuments() =>
        _environment.IsDevelopment() ? Ok(_sources.GetDocuments()) : NotFound();

    // Shows what a query looks like as a vector: its length and first few numbers.
    [HttpGet("embed")]
    public async Task<IActionResult> Embed([FromQuery] string text, CancellationToken cancellationToken)
    {
        if (!_environment.IsDevelopment())
        {
            return NotFound();
        }

        var vector = await _embedder.EmbedQueryAsync(text, cancellationToken);
        return Ok(new { text, dimensions = vector.Length, preview = vector.Take(8) });
    }
}

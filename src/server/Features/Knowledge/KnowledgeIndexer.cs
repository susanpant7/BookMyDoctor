using System.Diagnostics;

namespace BookMyDoctor.Features.Knowledge;

// Builds the vector index in the background when the server starts, so the API is usable straight away.
// Until it finishes, retrieval simply returns nothing. Retries if Ollama isn't reachable yet.
public sealed class KnowledgeIndexer : BackgroundService
{
    private static readonly TimeSpan MaxRetryDelay = TimeSpan.FromMinutes(5);

    private readonly KnowledgeSources _sources;
    private readonly IEmbedder _embedder;
    private readonly IVectorStore _store;
    private readonly ILogger<KnowledgeIndexer> _logger;

    public KnowledgeIndexer(KnowledgeSources sources, IEmbedder embedder, IVectorStore store, ILogger<KnowledgeIndexer> logger)
    {
        _sources = sources;
        _embedder = embedder;
        _store = store;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Yield();   // let the server finish starting before doing any work
        var retryDelay = TimeSpan.FromSeconds(5);
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await IndexAsync(stoppingToken);
                return;
            }
            catch (Exception ex) when ((ex is HttpRequestException or TaskCanceledException) && !stoppingToken.IsCancellationRequested)
            {
                _logger.LogWarning("Knowledge indexing failed ({Error}); retrying in {Delay}s. Is Ollama running?",
                    ex.Message, retryDelay.TotalSeconds);
                await Task.Delay(retryDelay, stoppingToken);
                retryDelay = TimeSpan.FromTicks(Math.Min(retryDelay.Ticks * 2, MaxRetryDelay.Ticks));
            }
        }
    }

    internal async Task IndexAsync(CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        var documents = _sources.GetDocuments();
        var vectors = await _embedder.EmbedDocumentsAsync(documents.Select(document => document.Text).ToList(), cancellationToken);
        _store.Upsert(documents.Zip(vectors).ToList());
        _logger.LogInformation("Indexed {Count} knowledge documents in {Seconds:F1}s", documents.Count, stopwatch.Elapsed.TotalSeconds);
    }
}

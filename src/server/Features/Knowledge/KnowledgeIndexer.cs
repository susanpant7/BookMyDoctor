namespace BookMyDoctor.Features.Knowledge;

// Makes doctor-settings.md, specialty-settings.md and hospital-settings.md in the vector store match the database in the background when the
// server starts, so the API is usable straight away. Retries if Ollama isn't reachable yet. Knowledge files are uploaded by an admin instead.
public sealed class KnowledgeIndexer : BackgroundService
{
    private static readonly TimeSpan MaxRetryDelay = TimeSpan.FromMinutes(5);

    private readonly IKnowledgeService _knowledge;
    private readonly ILogger<KnowledgeIndexer> _logger;

    public KnowledgeIndexer(IKnowledgeService knowledge, ILogger<KnowledgeIndexer> logger)
    {
        _knowledge = knowledge;
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
                await _knowledge.SyncSettingsAsync(stoppingToken);
                return;
            }
            catch (Exception ex) when ((ex is HttpRequestException or TaskCanceledException) && !stoppingToken.IsCancellationRequested)
            {
                _logger.LogWarning("Syncing the generated settings files failed ({Error}); retrying in {Delay}s. Is Ollama running?",
                    ex.Message, retryDelay.TotalSeconds);
                await Task.Delay(retryDelay, stoppingToken);
                retryDelay = TimeSpan.FromTicks(Math.Min(retryDelay.Ticks * 2, MaxRetryDelay.Ticks));
            }
        }
    }
}

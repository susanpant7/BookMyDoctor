using BookMyDoctor.Features.Settings;

namespace BookMyDoctor.Features.Knowledge;

public interface IKnowledgeRetriever
{
    // The knowledge chunks most relevant to the text, best first. Empty while the index is still being built.
    Task<IReadOnlyList<SearchResult>> SearchAsync(string text, CancellationToken cancellationToken = default);
}

public sealed class KnowledgeRetriever : IKnowledgeRetriever
{
    private readonly IEmbedder _embedder;
    private readonly IVectorStore _store;
    private readonly IAiSettingsProvider _settings;

    public KnowledgeRetriever(IEmbedder embedder, IVectorStore store, IAiSettingsProvider settings)
    {
        _embedder = embedder;
        _store = store;
        _settings = settings;
    }

    public async Task<IReadOnlyList<SearchResult>> SearchAsync(string text, CancellationToken cancellationToken = default)
    {
        if (_store.Count == 0 || string.IsNullOrWhiteSpace(text))
        {
            return [];
        }

        var queryVector = await _embedder.EmbedQueryAsync(text, cancellationToken);
        var settings = _settings.Get();
        var results = _store.Search(queryVector, settings.RetrievalTop, settings.RetrievalMinScore);
        return results.Count == 0
            ? results
            : results.Where(result => result.Score >= results[0].Score - settings.RetrievalMaxScoreGap).ToList();
    }
}

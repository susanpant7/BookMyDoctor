using Microsoft.Extensions.Options;

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
    private readonly KnowledgeOptions _options;

    public KnowledgeRetriever(IEmbedder embedder, IVectorStore store, IOptions<KnowledgeOptions> options)
    {
        _embedder = embedder;
        _store = store;
        _options = options.Value;
    }

    public async Task<IReadOnlyList<SearchResult>> SearchAsync(string text, CancellationToken cancellationToken = default)
    {
        if (_store.Count == 0 || string.IsNullOrWhiteSpace(text))
        {
            return [];
        }

        var queryVector = await _embedder.EmbedQueryAsync(text, cancellationToken);
        var results = _store.Search(queryVector, _options.Top, _options.MinScore);
        return results.Count == 0
            ? results
            : results.Where(result => result.Score >= results[0].Score - _options.MaxScoreGap).ToList();
    }
}

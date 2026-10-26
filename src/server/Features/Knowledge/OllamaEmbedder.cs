using BookMyDoctor.Features.Chats;
using Microsoft.Extensions.Options;

namespace BookMyDoctor.Features.Knowledge;

public sealed class OllamaEmbedder : IEmbedder
{
    // nomic-embed-text expects these task prefixes; without them retrieval is noticeably worse.
    internal const string DocumentPrefix = "search_document: ";
    internal const string QueryPrefix = "search_query: ";
    internal const int BatchSize = 32;

    private readonly HttpClient _http;
    private readonly OllamaOptions _options;

    public OllamaEmbedder(HttpClient http, IOptions<OllamaOptions> options)
    {
        _http = http;
        _options = options.Value;
    }

    public async Task<IReadOnlyList<float[]>> EmbedDocumentsAsync(IReadOnlyList<string> texts, CancellationToken cancellationToken = default)
    {
        var vectors = new List<float[]>(texts.Count);
        foreach (var batch in texts.Chunk(BatchSize))
        {
            vectors.AddRange(await EmbedAsync(batch.Select(text => DocumentPrefix + text).ToList(), cancellationToken));
        }
        return vectors;
    }

    public async Task<float[]> EmbedQueryAsync(string text, CancellationToken cancellationToken = default) =>
        (await EmbedAsync([QueryPrefix + text], cancellationToken))[0];

    private async Task<float[][]> EmbedAsync(IReadOnlyList<string> input, CancellationToken cancellationToken)
    {
        var response = await _http.PostAsJsonAsync("/api/embed", new OllamaEmbedRequest(_options.EmbeddingModel, input), cancellationToken);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<OllamaEmbedResponse>(cancellationToken);
        if (result is null || result.Embeddings.Length != input.Count)
        {
            throw new InvalidOperationException($"Ollama returned {result?.Embeddings.Length ?? 0} embeddings for {input.Count} texts.");
        }
        return result.Embeddings;
    }
}

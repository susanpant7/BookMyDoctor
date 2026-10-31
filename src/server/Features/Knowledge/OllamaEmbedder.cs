using BookMyDoctor.Features.Chats;
using BookMyDoctor.Features.Settings;

namespace BookMyDoctor.Features.Knowledge;

public sealed class OllamaEmbedder : IEmbedder
{
    internal const int BatchSize = 32;

    private readonly HttpClient _http;
    private readonly IAiSettingsProvider _settings;

    public OllamaEmbedder(HttpClient http, IAiSettingsProvider settings)
    {
        _http = http;
        _settings = settings;
    }

    public async Task<IReadOnlyList<float[]>> EmbedDocumentsAsync(IReadOnlyList<string> texts, CancellationToken cancellationToken = default)
    {
        var settings = _settings.Get();
        var vectors = new List<float[]>(texts.Count);
        foreach (var batch in texts.Chunk(BatchSize))
        {
            vectors.AddRange(await EmbedAsync(batch.Select(text => AiSettings.WithPrefix(settings.EmbeddingDocumentPrefix, text)).ToList(), cancellationToken));
        }
        return vectors;
    }

    public async Task<float[]> EmbedQueryAsync(string text, CancellationToken cancellationToken = default) =>
        (await EmbedAsync([AiSettings.WithPrefix(_settings.Get().EmbeddingQueryPrefix, text)], cancellationToken))[0];

    private async Task<float[][]> EmbedAsync(IReadOnlyList<string> input, CancellationToken cancellationToken)
    {
        var response = await _http.PostAsJsonAsync("/api/embed", new OllamaEmbedRequest(_settings.Get().EmbeddingModel, input), cancellationToken);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<OllamaEmbedResponse>(cancellationToken);
        if (result is null || result.Embeddings.Length != input.Count)
        {
            throw new InvalidOperationException($"Ollama returned {result?.Embeddings.Length ?? 0} embeddings for {input.Count} texts.");
        }
        return result.Embeddings;
    }
}

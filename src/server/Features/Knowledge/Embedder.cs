using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using MyHospitalAI.Features.Chats;
using MyHospitalAI.Features.Settings;

namespace MyHospitalAI.Features.Knowledge;

// Turns text into vectors with the service at the "Embedding API address" setting: Ollama's own API for an address
// with no path (http://localhost:11434), or the OpenAI embeddings format (POST {address}/embeddings with a Bearer
// key) for an online service such as Gemini or Mistral. The key is EMBEDDING_API_KEY, or AI_API_KEY when that is
// not set (the same service for chat and embeddings needs only one key).
public sealed class Embedder : IEmbedder
{
    internal const int BatchSize = 32;

    private readonly HttpClient _http;
    private readonly IAiSettingsProvider _settings;
    private readonly IConfiguration _configuration;
    private readonly ILogger<Embedder> _logger;

    public Embedder(HttpClient http, IAiSettingsProvider settings, IConfiguration configuration, ILogger<Embedder> logger)
    {
        _http = http;
        _settings = settings;
        _configuration = configuration;
        _logger = logger;
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
        var settings = _settings.Get();
        var vectors = AiProviders.IsOllamaApi(settings.EmbeddingBaseUrl)
            ? await EmbedWithOllamaAsync(settings, input, cancellationToken)
            : await EmbedOnlineAsync(settings, input, cancellationToken);
        if (vectors.Length != input.Count)
        {
            throw new InvalidOperationException($"The embedding service returned {vectors.Length} embeddings for {input.Count} texts.");
        }
        return vectors;
    }

    private async Task<float[][]> EmbedWithOllamaAsync(AiSettings settings, IReadOnlyList<string> input, CancellationToken cancellationToken)
    {
        using var response = await _http.PostAsJsonAsync(
            $"{settings.EmbeddingBaseUrl}/api/embed", new OllamaEmbedRequest(settings.EmbeddingModel, input), cancellationToken);
        await ThrowIfFailedAsync(response, settings, "If the model is missing, run: ollama pull " + settings.EmbeddingModel, cancellationToken);
        var result = await response.Content.ReadFromJsonAsync<OllamaEmbedResponse>(cancellationToken);
        return result?.Embeddings ?? [];
    }

    private async Task<float[][]> EmbedOnlineAsync(AiSettings settings, IReadOnlyList<string> input, CancellationToken cancellationToken)
    {
        var key = new[] { AiProviders.EmbeddingApiKeyName, AiProviders.ApiKeyName }
            .Select(name => _configuration[name])
            .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
        if (key is null)
        {
            throw new HttpRequestException(
                $"The Embedding API address is an online service, but neither {AiProviders.EmbeddingApiKeyName} nor {AiProviders.ApiKeyName} is set in the .env file.");
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, $"{settings.EmbeddingBaseUrl}/embeddings")
        {
            // Sent whole (with a Content-Length) rather than streamed, which every service accepts.
            Content = new StringContent(JsonSerializer.Serialize(new { model = settings.EmbeddingModel, input }), Encoding.UTF8, "application/json")
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", key);
        using var response = await _http.SendAsync(request, cancellationToken);
        await ThrowIfFailedAsync(response, settings, "Check the embedding model, the Embedding API address and the key.", cancellationToken);

        // The vectors come in a "data" list, each with the position of its text.
        var result = await response.Content.ReadFromJsonAsync<OnlineEmbedResponse>(cancellationToken);
        return (result?.Data ?? []).OrderBy(item => item.Index).Select(item => item.Embedding).ToArray();
    }

    // The message is shown on the Knowledge page, so it says what to fix. The key is never part of it.
    private async Task ThrowIfFailedAsync(HttpResponseMessage response, AiSettings settings, string hint, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var detail = await response.Content.ReadAsStringAsync(cancellationToken);
        _logger.LogWarning("Embedding service {Address} returned {Status} for model {Model}: {Detail}",
            settings.EmbeddingBaseUrl, (int)response.StatusCode, settings.EmbeddingModel, detail.Length > 500 ? detail[..500] : detail);
        throw new HttpRequestException(
            $"The embedding service returned {(int)response.StatusCode} for model {settings.EmbeddingModel}. {hint}", null, response.StatusCode);
    }

    private sealed record OnlineEmbedResponse(IReadOnlyList<OnlineEmbedding>? Data);

    private sealed record OnlineEmbedding(int Index, float[] Embedding);
}

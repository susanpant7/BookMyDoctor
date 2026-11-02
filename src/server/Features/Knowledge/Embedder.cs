using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using MyHospitalAI.Features.Chats;
using MyHospitalAI.Features.Settings;

namespace MyHospitalAI.Features.Knowledge;

// Turns text into vectors with the service at the "Embedding API address" setting: Ollama's own API for an address
// with no path (http://localhost:11434), or the OpenAI embeddings format (POST {address}/embeddings with a Bearer
// key) for an online service such as Gemini or Mistral. The key is the .env variable named by the "Embedding API key
// name" setting (see AiProviders.KeyNameInUse).
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

    public Task<IReadOnlyList<float[]>> EmbedDocumentsAsync(IReadOnlyList<string> texts, CancellationToken cancellationToken = default) =>
        EmbedDocumentsAsync(texts, _settings.Get(), cancellationToken);

    public async Task<IReadOnlyList<float[]>> EmbedDocumentsAsync(
        IReadOnlyList<string> texts, AiSettings settings, CancellationToken cancellationToken = default)
    {
        var vectors = new List<float[]>(texts.Count);
        foreach (var batch in texts.Chunk(BatchSize))
        {
            vectors.AddRange(await EmbedAsync(
                settings, batch.Select(text => AiSettings.WithPrefix(settings.EmbeddingDocumentPrefix, text)).ToList(), cancellationToken));
        }
        return vectors;
    }

    public async Task<float[]> EmbedQueryAsync(string text, CancellationToken cancellationToken = default)
    {
        var settings = _settings.Get();
        return (await EmbedAsync(settings, [AiSettings.WithPrefix(settings.EmbeddingQueryPrefix, text)], cancellationToken))[0];
    }

    private async Task<float[][]> EmbedAsync(AiSettings settings, IReadOnlyList<string> input, CancellationToken cancellationToken)
    {
        if (settings.EmbeddingBaseUrl.Length == 0 || settings.EmbeddingModel.Length == 0)
        {
            throw new HttpRequestException(
                "No embedding model is selected yet. Choose one on Settings → AI assistant, then load the knowledge files.");
        }

        float[][] vectors;
        try
        {
            vectors = await EmbedOnceAsync(settings, input, cancellationToken);
        }
        catch (HttpRequestException ex) when (ex.InnerException is not null
                                              || ex.StatusCode is HttpStatusCode.TooManyRequests or HttpStatusCode.ServiceUnavailable)
        {
            // A free online service drops a request now and then, or is briefly over its limit: try once more.
            _logger.LogInformation("The embedding service failed ({Error}); trying once more", ex.Message);
            await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
            vectors = await EmbedOnceAsync(settings, input, cancellationToken);
        }
        if (vectors.Length != input.Count)
        {
            throw new InvalidOperationException($"The embedding service returned {vectors.Length} embeddings for {input.Count} texts.");
        }
        return vectors;
    }

    private Task<float[][]> EmbedOnceAsync(AiSettings settings, IReadOnlyList<string> input, CancellationToken cancellationToken) =>
        AiProviders.IsOllamaApi(settings.EmbeddingBaseUrl)
            ? EmbedWithOllamaAsync(settings, input, cancellationToken)
            : EmbedOnlineAsync(settings, input, cancellationToken);

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
        var keyName = AiProviders.KeyNameInUse(settings.EmbeddingApiKeyName, settings.EmbeddingBaseUrl);
        var key = AiProviders.ReadKey(_configuration, keyName)
            ?? throw new HttpRequestException($"The embedding model is an online service, but it has no key. {AiProviders.MissingKeyMessage(keyName)}");

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
            $"The embedding service returned {(int)response.StatusCode} for model {settings.EmbeddingModel} ({AiProviders.ErrorDetail(detail)}). {hint}",
            null, response.StatusCode);
    }

    private sealed record OnlineEmbedResponse(IReadOnlyList<OnlineEmbedding>? Data);

    private sealed record OnlineEmbedding(int Index, float[] Embedding);
}

using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using MyHospitalAI.Features.Chats;
using MyHospitalAI.Features.Knowledge;

namespace MyHospitalAI.Features.Settings;

// A chat or embedding model to list, test or switch to: where it runs, its name there, the name of its key's .env
// variable and (embeddings) its prefixes.
public sealed record ModelChoice(
    string Kind, string BaseUrl, string Model, string? ApiKeyName = null, string? QueryPrefix = null, string? DocumentPrefix = null);

// The models the service at an address offers for our key.
public sealed record AvailableAiModels(string BaseUrl, IReadOnlyList<string> Models);

// How long a model took to answer a test request (a tiny question, so this includes any thinking).
public sealed record TestModelResult(long Milliseconds);

// What "Use" did: for an embedding model, how many knowledge sections were embedded again with it.
public sealed record UseModelResult(int Reembedded);

// Asks a service which models it has, tests a model with a tiny request, and switches to a model. A chat model is
// only used when it answered the test; for an embedding model all the knowledge is embedded with it first, and the
// settings are only saved when that worked. A key is only sent to an address that is already known (the models in
// use, the saved models and the services on the Settings page), so it can't be sent to any address typed in.
public sealed class AiModelSwitcher
{
    private readonly HttpClient _http;
    private readonly IConfiguration _configuration;
    private readonly ISettingsService _settings;
    private readonly AiModelsService _models;
    private readonly IChatModel _chat;
    private readonly IEmbedder _embedder;
    private readonly IKnowledgeService _knowledge;
    private readonly ILogger<AiModelSwitcher> _logger;

    public AiModelSwitcher(
        HttpClient http, IConfiguration configuration, ISettingsService settings, AiModelsService models, IChatModel chat,
        IEmbedder embedder, IKnowledgeService knowledge, ILogger<AiModelSwitcher> logger)
    {
        _http = http;
        _configuration = configuration;
        _settings = settings;
        _models = models;
        _chat = chat;
        _embedder = embedder;
        _knowledge = knowledge;
        _logger = logger;
    }

    // The services shown on the Settings page, for each kind.
    public static IReadOnlyList<AiModelDefault> Services(string kind) =>
        AiModelsService.CheckedKind(kind) == AiModelKinds.Chat ? AiProviders.ChatModels : AiProviders.EmbeddingModels;

    // Ollama's own /api/tags, or the /models list of an online service. Throws HttpRequestException with what went wrong.
    public async Task<AvailableAiModels> ListAsync(string kind, string baseUrl, string? keyName, CancellationToken cancellationToken)
    {
        kind = AiModelsService.CheckedKind(kind);
        baseUrl = EnsureKnown(baseUrl);
        keyName = CheckedKeyName(keyName);
        var keyInUse = AiProviders.KeyNameInUse(_configuration, kind == AiModelKinds.Embedding, keyName, baseUrl);

        var ollama = AiProviders.IsOllamaApi(baseUrl);
        using var request = new HttpRequestMessage(HttpMethod.Get, ollama ? $"{baseUrl}/api/tags" : $"{baseUrl}/models");
        if (!ollama)
        {
            var key = AiProviders.ReadKey(_configuration, keyInUse)
                ?? throw new HttpRequestException($"{keyInUse} is not set in the .env file. Set it and restart the server.");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", key);
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(20));
        HttpResponseMessage response;
        try
        {
            response = await _http.SendAsync(request, timeout.Token);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException && !cancellationToken.IsCancellationRequested)
        {
            throw new HttpRequestException(
                $"Can't reach {baseUrl}." + (ollama ? " Is Ollama running?" : ""), ex);
        }

        using (response)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                throw new HttpRequestException(
                    (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden
                        || body.Contains("API key", StringComparison.OrdinalIgnoreCase)
                        ? $"The service refused the key ({(int)response.StatusCode}): {keyInUse} in the .env file must be a whole, valid key for this service. "
                        : $"The service answered {(int)response.StatusCode}. ") + AiProviders.ErrorDetail(body),
                    null, response.StatusCode);
            }
            return new AvailableAiModels(baseUrl, ReadNames(body, ollama, kind));
        }
    }

    // Sends one tiny request to the model and saves nothing; returns how long it took. Throws HttpRequestException
    // with the service's reason.
    public async Task<TestModelResult> TestAsync(ModelChoice choice, CancellationToken cancellationToken)
    {
        var (kind, settings) = Checked(choice);
        EnsureKnown(choice.BaseUrl);
        var clock = Stopwatch.StartNew();
        try
        {
            if (kind == AiModelKinds.Chat)
            {
                await _chat.ChatAsync([new ModelMessage("user", "Reply with the single word OK.")], [], settings, null);
            }
            else
            {
                await _embedder.EmbedDocumentsAsync(["A test sentence."], settings, cancellationToken);
            }
            return new TestModelResult(clock.ElapsedMilliseconds);
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new HttpRequestException($"{choice.Model} did not answer in time. The service may be busy: try again, or use another model.");
        }
    }

    // Chat: tests the model, then saves it. Embedding: tests it, embeds all the knowledge with it, then saves it.
    // When anything fails, the model in use is not changed.
    public async Task<UseModelResult> UseAsync(ModelChoice choice, CancellationToken cancellationToken)
    {
        var (kind, settings) = Checked(choice);
        await TestAsync(choice, cancellationToken);

        if (kind == AiModelKinds.Chat)
        {
            _settings.Update(SettingKeys.ChatBaseUrl, settings.ChatBaseUrl);
            _settings.Update(SettingKeys.ChatModel, settings.ChatModel);
            _settings.Update(SettingKeys.ChatApiKeyName, settings.ChatApiKeyName);
            _logger.LogInformation("The chat model is now {Model} at {Address}", settings.ChatModel, settings.ChatBaseUrl);
            return new UseModelResult(0);
        }

        var reembedded = await _knowledge.ReembedAllAsync(settings, cancellationToken);
        _settings.Update(SettingKeys.EmbeddingBaseUrl, settings.EmbeddingBaseUrl);
        _settings.Update(SettingKeys.EmbeddingModel, settings.EmbeddingModel);
        _settings.Update(SettingKeys.EmbeddingApiKeyName, settings.EmbeddingApiKeyName);
        _settings.Update(SettingKeys.EmbeddingQueryPrefix, settings.EmbeddingQueryPrefix);
        _settings.Update(SettingKeys.EmbeddingDocumentPrefix, settings.EmbeddingDocumentPrefix);
        _logger.LogInformation("The embedding model is now {Model} at {Address}", settings.EmbeddingModel, settings.EmbeddingBaseUrl);
        return new UseModelResult(reembedded);
    }

    // The current settings with the chosen model in place of the one in use.
    private (string Kind, AiSettings Settings) Checked(ModelChoice choice)
    {
        var kind = AiModelsService.CheckedKind(choice.Kind);
        var baseUrl = AiModelsService.CheckedBaseUrl(choice.BaseUrl);
        var model = choice.Model?.Trim() ?? "";
        if (model.Length is 0 or > 200)
        {
            throw new ArgumentException("Model is required and can be up to 200 characters.");
        }

        var keyName = CheckedKeyName(choice.ApiKeyName);
        var current = _settings.Get();
        return (kind, kind == AiModelKinds.Chat
            ? current with { ChatBaseUrl = baseUrl, ChatModel = model, ChatApiKeyName = keyName }
            : current with
            {
                EmbeddingBaseUrl = baseUrl,
                EmbeddingModel = model,
                EmbeddingApiKeyName = keyName,
                EmbeddingQueryPrefix = choice.QueryPrefix?.Trim() ?? "",
                EmbeddingDocumentPrefix = choice.DocumentPrefix?.Trim() ?? ""
            });
    }

    private string EnsureKnown(string baseUrl)
    {
        baseUrl = AiModelsService.CheckedBaseUrl(baseUrl);
        bool Same(string other) => string.Equals(other.TrimEnd('/'), baseUrl, StringComparison.OrdinalIgnoreCase);

        var current = _settings.Get();
        var known = Same(current.ChatBaseUrl) || Same(current.EmbeddingBaseUrl)
            || AiProviders.ChatModels.Concat(AiProviders.EmbeddingModels).Any(service => Same(service.BaseUrl))
            || _models.GetAll(AiModelKinds.Chat).Concat(_models.GetAll(AiModelKinds.Embedding)).Any(model => Same(model.BaseUrl));
        return known
            ? baseUrl
            : throw new ArgumentException("That address is not one of the saved models or services. Add it as a model first.");
    }

    private static string CheckedKeyName(string? keyName)
    {
        keyName = keyName?.Trim() ?? "";
        return keyName.Length == 0 || AiProviders.IsValidKeyName(keyName)
            ? keyName
            : throw new ArgumentException("API key name must be empty or a variable name that ends in API_KEY, e.g. GEMINI_API_KEY.");
    }

    // Ollama: {"models":[{"name":"qwen2.5:7b"}]}. OpenAI format: {"data":[{"id":"…"}]}; Gemini puts "models/" before each name.
    private static List<string> ReadNames(string json, bool ollama, string kind)
    {
        using var document = JsonDocument.Parse(json);
        if (!document.RootElement.TryGetProperty(ollama ? "models" : "data", out var items) || items.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var names = items.EnumerateArray()
            .Select(item => item.TryGetProperty(ollama ? "name" : "id", out var name) ? name.GetString() : null)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Select(name => name!.StartsWith("models/", StringComparison.Ordinal) ? name["models/".Length..] : name)
            .Distinct()
            .Order()
            .ToList();

        // A service lists all its models. For the chat, leave out the ones that can't chat; for the embeddings, keep the
        // ones that look like embedding models, or all of them when none do.
        bool Has(string name, string[] words) => words.Any(word => name.Contains(word, StringComparison.OrdinalIgnoreCase));
        if (kind == AiModelKinds.Chat)
        {
            return names.Where(name => !Has(name, NotForChat)).ToList();
        }
        var embedding = names.Where(name => Has(name, ForEmbedding)).ToList();
        return embedding.Count > 0 ? embedding : names;
    }

    private static readonly string[] NotForChat = ["embed", "guard", "whisper", "tts", "orpheus", "imagen", "veo", "aqa"];
    private static readonly string[] ForEmbedding = ["embed", "bge", "minilm", "e5-", "gte-"];
}

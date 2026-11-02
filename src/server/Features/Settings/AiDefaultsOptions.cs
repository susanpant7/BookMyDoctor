using MyHospitalAI.Features.Chats;

namespace MyHospitalAI.Features.Settings;

// The starting chat and embedding models, from configuration (environment variables AiDefaults__ChatBaseUrl,
// AiDefaults__ChatModel, AiDefaults__EmbeddingBaseUrl and AiDefaults__EmbeddingModel). They are what a new database
// uses until the settings are saved, and what "Load default AI assistant data" saves. Without them no chat or
// embedding model is selected, and an admin chooses them on the Settings page. Changing them later does not change
// saved settings: use the Settings page for that.
// When an address is one of the services on the Settings page (see AiProviders), the rest of that service is filled
// in too: its key name, its model (unless one is given) and, for embeddings, its prefixes.
public sealed class AiDefaultsOptions
{
    public const string SectionName = "AiDefaults";

    // Where the chat model runs: Ollama (http://localhost:11434, or http://host.docker.internal:11434 from a Docker
    // container), or an online service's address up to /chat/completions.
    public string ChatBaseUrl { get; init; } = "";

    public string ChatModel { get; init; } = "";

    public string EmbeddingBaseUrl { get; init; } = "";

    public string EmbeddingModel { get; init; } = "";

    public IReadOnlyDictionary<string, string> ToOverrides()
    {
        var overrides = new Dictionary<string, string>();
        Add(overrides, false, ChatBaseUrl, ChatModel);
        Add(overrides, true, EmbeddingBaseUrl, EmbeddingModel);
        return overrides;
    }

    private static void Add(Dictionary<string, string> overrides, bool embedding, string baseUrl, string model)
    {
        baseUrl = baseUrl.Trim().TrimEnd('/');
        model = model.Trim();
        // A value that is not a web address is left out, so a typo here can't break the chat.
        if (!(Uri.TryCreate(baseUrl, UriKind.Absolute, out var url) && url.Scheme is "http" or "https"))
        {
            return;
        }

        var service = (embedding ? AiProviders.EmbeddingModels : AiProviders.ChatModels)
            .FirstOrDefault(service => string.Equals(service.BaseUrl, baseUrl, StringComparison.OrdinalIgnoreCase));
        model = model.Length > 0 ? model : service?.Model ?? "";
        if (model.Length == 0)
        {
            return;
        }

        var prefix = embedding ? "embedding" : "chat";
        overrides[$"{prefix}.base_url"] = baseUrl;
        overrides[$"{prefix}.model"] = model;
        overrides[$"{prefix}.api_key_name"] = service?.ApiKeyName ?? "";
        // A service's prefixes belong to its own model.
        if (embedding && service is not null && service.Model == model)
        {
            overrides[SettingKeys.EmbeddingQueryPrefix] = service.QueryPrefix;
            overrides[SettingKeys.EmbeddingDocumentPrefix] = service.DocumentPrefix;
        }
    }
}

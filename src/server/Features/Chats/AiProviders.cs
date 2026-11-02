using System.Text.Json;

namespace MyHospitalAI.Features.Chats;

// A chat or embedding model as the Settings page lists it: where it runs, its name there, the name of the .env
// variable with its key (empty for Ollama) and notes for the admin. The prefixes only apply to embedding models.
public sealed record AiModelDefault(
    string Name, string BaseUrl, string Model, string ApiKeyName, string Notes, string QueryPrefix = "", string DocumentPrefix = "");

// The services listed on the Settings page, and how a model finds its key. Each service has its own key in the .env
// file (GEMINI_API_KEY, GROQ_API_KEY, ...); a model stores only the NAME of that variable, never the key.
public static class AiProviders
{
    // Reads a key by its .env name. In the configuration a double underscore is a colon (Some__ApiKey is Some:ApiKey).
    // Null when it is not set or the name is not a key name.
    public static string? ReadKey(IConfiguration configuration, string name) =>
        IsValidKeyName(name) && configuration[name.Replace("__", ":")] is { } key && !string.IsNullOrWhiteSpace(key) ? key : null;

    // A key name is a variable name ending in API_KEY or ApiKey. Anything else (the admin password, Jwt__Key) is
    // refused, so a saved model can't make the server send another secret to a service.
    public static bool IsValidKeyName(string name) =>
        name.Length is > 0 and <= 100
        && char.IsAsciiLetter(name[0])
        && name.All(c => char.IsAsciiLetterOrDigit(c) || c == '_')
        && (name.EndsWith("API_KEY", StringComparison.Ordinal) || name.EndsWith("ApiKey", StringComparison.Ordinal));

    // The variable a model reads: the name it stores, else the key name of the service at the same address (a model at
    // Gemini's address reads GEMINI_API_KEY). Empty when neither has one.
    public static string KeyNameInUse(string? keyName, string baseUrl)
    {
        if (!string.IsNullOrWhiteSpace(keyName))
        {
            return keyName.Trim();
        }
        return ChatModels.Concat(EmbeddingModels).FirstOrDefault(service =>
            service.ApiKeyName.Length > 0
            && string.Equals(service.BaseUrl, baseUrl.TrimEnd('/'), StringComparison.OrdinalIgnoreCase))?.ApiKeyName ?? "";
    }

    // What to tell the admin when an online model has no key to send.
    public static string MissingKeyMessage(string keyName) => keyName.Length == 0
        ? "This model has no API key name: edit it on the Settings page and give the name of the .env variable with its key."
        : $"{keyName} is not set in the .env file. Set it and restart the server.";

    // Any other service that speaks the OpenAI chat format works too. The model must support tool calling.
    public static IReadOnlyList<AiModelDefault> ChatModels { get; } =
    [
        new("Ollama – Qwen 2.5 7B", "http://localhost:11434", "qwen2.5:7b", "",
            "Free, runs on this computer; the assistant's rules were tested with it. Pull it first: ollama pull qwen2.5:7b. In Docker Compose the address is http://ollama:11434. https://ollama.com/download"),
        new("Google Gemini Flash", "https://generativelanguage.googleapis.com/v1beta/openai", "gemini-3.8-flash", "GEMINI_API_KEY",
            "Free tier with a daily limit. Often busy (error 503 \"high demand\"): then try Flash-Lite. Key: https://aistudio.google.com/apikey"),
        new("Google Gemini Flash-Lite", "https://generativelanguage.googleapis.com/v1beta/openai", "gemini-flash-lite-latest", "GEMINI_API_KEY",
            "Free tier with a higher daily limit than Flash; fast, and less often busy. Same key as Gemini Flash. Key: https://aistudio.google.com/apikey"),
        new("xAI Grok", "https://api.x.ai/v1", "grok-4-1-fast-non-reasoning", "XAI_API_KEY",
            "Paid per use; new accounts sometimes get free credits. Key: https://console.x.ai"),
        new("Groq – GPT-OSS 20B", "https://api.groq.com/openai/v1", "openai/gpt-oss-20b", "GROQ_API_KEY",
            "Free tier with per-minute and daily limits. Very fast. Which models a key can use differs: use View models, and Test one before you select it. Key: https://console.groq.com/keys"),
        new("OpenRouter – Llama 3.3 70B (free)", "https://openrouter.ai/api/v1", "meta-llama/llama-3.3-70b-instruct:free", "OPENROUTER_API_KEY",
            "Many models with one key; models ending in :free cost nothing but have a small daily limit. Key: https://openrouter.ai/keys"),
        new("Mistral Small", "https://api.mistral.ai/v1", "mistral-small-latest", "MISTRAL_API_KEY",
            "Free \"Experiment\" plan with rate limits. Key: https://console.mistral.ai/api-keys"),
        new("Cerebras – GPT-OSS 120B", "https://api.cerebras.ai/v1", "gpt-oss-120b", "CEREBRAS_API_KEY",
            "Free tier with a daily token limit. Very fast. Key: https://cloud.cerebras.ai"),
        new("Cloudflare Workers AI – Llama 3.3 70B", "https://api.cloudflare.com/client/v4/accounts/<ACCOUNT_ID>/ai/v1",
            "@cf/meta/llama-3.3-70b-instruct-fp8-fast", "CLOUDFLARE_API_KEY",
            "A free daily allowance. Edit a copy: replace <ACCOUNT_ID> in the address with your account id, and use an API token with Workers AI access. Key: https://dash.cloudflare.com/profile/api-tokens")
    ];

    // Services with an embeddings endpoint. Most chat-only services (Groq, Grok, Cerebras) have none.
    public static IReadOnlyList<AiModelDefault> EmbeddingModels { get; } =
    [
        new("Ollama – Nomic Embed", "http://localhost:11434", "nomic-embed-text", "",
            "Free, runs on this computer. Pull it first: ollama pull nomic-embed-text. https://ollama.com/download",
            "search_query:", "search_document:"),
        new("Google Gemini Embedding", "https://generativelanguage.googleapis.com/v1beta/openai", "gemini-embedding-001", "GEMINI_API_KEY",
            "Free tier with rate limits. The same key as Gemini chat works. Key: https://aistudio.google.com/apikey"),
        new("Mistral Embed", "https://api.mistral.ai/v1", "mistral-embed", "MISTRAL_API_KEY",
            "Free \"Experiment\" plan with rate limits. The same key as Mistral chat works. Key: https://console.mistral.ai/api-keys"),
        new("OpenAI Embedding Small", "https://api.openai.com/v1", "text-embedding-3-small", "OPENAI_API_KEY",
            "Paid, but very cheap: embedding all the knowledge costs a fraction of a cent. Key: https://platform.openai.com/api-keys"),
        new("Cloudflare Workers AI – BGE M3", "https://api.cloudflare.com/client/v4/accounts/<ACCOUNT_ID>/ai/v1",
            "@cf/baai/bge-m3", "CLOUDFLARE_API_KEY",
            "A free daily allowance. Edit a copy: replace <ACCOUNT_ID> in the address with your account id. Key: https://dash.cloudflare.com/profile/api-tokens")
    ];

    // The service's own reason from an error reply, shortened, to show to the admin: {"error":{"message":…}} (Gemini
    // wraps it in a list), or the start of the text. Services don't echo the key back, so it is not part of it.
    public static string ErrorDetail(string body)
    {
        try
        {
            using var document = JsonDocument.Parse(body);
            var root = document.RootElement.ValueKind == JsonValueKind.Array && document.RootElement.GetArrayLength() > 0
                ? document.RootElement[0]
                : document.RootElement;
            if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty("error", out var error))
            {
                var message = error.ValueKind == JsonValueKind.String
                    ? error.GetString()
                    : error.ValueKind == JsonValueKind.Object && error.TryGetProperty("message", out var text) ? text.GetString() : null;
                if (!string.IsNullOrWhiteSpace(message))
                {
                    return Shorten(message.Trim());
                }
            }
        }
        catch (JsonException)
        {
        }
        return Shorten(body.Trim());
    }

    private static string Shorten(string text) => text.Length > 300 ? text[..300] + "…" : text;

    // An address with no path (http://localhost:11434) is Ollama's own API, which can keep the model loaded and set
    // the context window. An address with a path (…/v1, …/openai) is called with the OpenAI format.
    public static bool IsOllamaApi(string baseUrl) =>
        Uri.TryCreate(baseUrl, UriKind.Absolute, out var url) && url.AbsolutePath.Trim('/').Length == 0;
}

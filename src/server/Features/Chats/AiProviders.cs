namespace MyHospitalAI.Features.Chats;

// A service that runs the chat model. Every provider except Ollama is called the same way (the OpenAI chat format:
// POST {BaseUrl}/chat/completions with a Bearer key), so adding one is one line here.
// ApiKeyName is the environment variable (or .env line) that holds the key; keys are never stored in the database.
public sealed record AiProvider(
    string Id, string Name, string BaseUrl, string? ApiKeyName, string DefaultModel, string FreeTier, string SignUpUrl)
{
    public bool IsLocal => ApiKeyName is null;
}

public static class AiProviders
{
    public const string Ollama = "ollama";

    public static IReadOnlyList<AiProvider> All { get; } =
    [
        new(Ollama, "Ollama (on this computer)", "", null, "qwen2.5:7b",
            "Free, runs on your own machine. Needs Ollama running and the model pulled (ollama pull <model>).",
            "https://ollama.com/download"),
        new("gemini", "Google Gemini", "https://generativelanguage.googleapis.com/v1beta/openai", "GEMINI_API_KEY", "gemini-flash-latest",
            "Free tier for the Flash and Flash-Lite models, with a daily limit.",
            "https://aistudio.google.com/apikey"),
        new("grok", "xAI Grok", "https://api.x.ai/v1", "XAI_API_KEY", "grok-4-1-fast-non-reasoning",
            "Paid per use; new accounts sometimes get free credits.",
            "https://console.x.ai"),
        new("groq", "Groq", "https://api.groq.com/openai/v1", "GROQ_API_KEY", "llama-3.3-70b-versatile",
            "Free tier with per-minute and daily limits. Very fast.",
            "https://console.groq.com/keys"),
        new("openrouter", "OpenRouter", "https://openrouter.ai/api/v1", "OPENROUTER_API_KEY", "meta-llama/llama-3.3-70b-instruct:free",
            "Many models with one key; models ending in :free cost nothing but have a small daily limit.",
            "https://openrouter.ai/keys"),
        new("mistral", "Mistral", "https://api.mistral.ai/v1", "MISTRAL_API_KEY", "mistral-small-latest",
            "Free \"Experiment\" plan with rate limits.",
            "https://console.mistral.ai/api-keys"),
        new("cerebras", "Cerebras", "https://api.cerebras.ai/v1", "CEREBRAS_API_KEY", "gpt-oss-120b",
            "Free tier with a daily token limit. Very fast.",
            "https://cloud.cerebras.ai")
    ];

    public static AiProvider? Find(string id) =>
        All.FirstOrDefault(provider => provider.Id.Equals(id, StringComparison.OrdinalIgnoreCase));

    // The Settings key of the model used with this provider, so each provider remembers its own model.
    public static string ModelSettingKey(AiProvider provider) => $"model.{provider.Id}";
}

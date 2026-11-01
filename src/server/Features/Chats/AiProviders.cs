namespace MyHospitalAI.Features.Chats;

// Known services that can run the chat model, shown as notes on the Settings page so switching is easy: set the
// "Chat API address" and "Chat model" settings to one row's values, and put that service's key in AI_API_KEY in
// the .env file. Any other service that speaks the OpenAI chat format works too.
public sealed record AiProvider(string Name, string BaseUrl, string ExampleModel, bool NeedsApiKey, string FreeTier, string SignUpUrl);

// A service that can turn text into vectors for the knowledge search, with the prefixes its model expects.
public sealed record EmbeddingProvider(
    string Name, string BaseUrl, string ExampleModel, string QueryPrefix, string DocumentPrefix, bool NeedsApiKey, string FreeTier, string SignUpUrl);

public static class AiProviders
{
    // The .env line (or environment variable) with the key for the chat API. Never stored in the database.
    public const string ApiKeyName = "AI_API_KEY";

    // The key for an online embedding service. When it is not set, AI_API_KEY is used.
    public const string EmbeddingApiKeyName = "EMBEDDING_API_KEY";

    public static IReadOnlyList<AiProvider> All { get; } =
    [
        new("Ollama (on this computer)", "http://localhost:11434", "qwen2.5:7b", false,
            "Free, runs on your own machine. Pull the model first: ollama pull <model>. In Docker Compose the address is http://ollama:11434.",
            "https://ollama.com/download"),
        new("Google Gemini", "https://generativelanguage.googleapis.com/v1beta/openai", "gemini-flash-latest", true,
            "Free tier for the Flash and Flash-Lite models, with a daily limit.",
            "https://aistudio.google.com/apikey"),
        new("xAI Grok", "https://api.x.ai/v1", "grok-4-1-fast-non-reasoning", true,
            "Paid per use; new accounts sometimes get free credits.",
            "https://console.x.ai"),
        new("Groq", "https://api.groq.com/openai/v1", "llama-3.3-70b-versatile", true,
            "Free tier with per-minute and daily limits. Very fast.",
            "https://console.groq.com/keys"),
        new("OpenRouter", "https://openrouter.ai/api/v1", "meta-llama/llama-3.3-70b-instruct:free", true,
            "Many models with one key; models ending in :free cost nothing but have a small daily limit.",
            "https://openrouter.ai/keys"),
        new("Mistral", "https://api.mistral.ai/v1", "mistral-small-latest", true,
            "Free \"Experiment\" plan with rate limits.",
            "https://console.mistral.ai/api-keys"),
        new("Cerebras", "https://api.cerebras.ai/v1", "gpt-oss-120b", true,
            "Free tier with a daily token limit. Very fast.",
            "https://cloud.cerebras.ai")
    ];

    // Services with an embeddings endpoint. Most chat-only services (Groq, Grok, Cerebras) have none.
    public static IReadOnlyList<EmbeddingProvider> Embedding { get; } =
    [
        new("Ollama (on this computer)", "http://localhost:11434", "nomic-embed-text", "search_query:", "search_document:", false,
            "Free, runs on your own machine. Pull the model first: ollama pull nomic-embed-text.",
            "https://ollama.com/download"),
        new("Google Gemini", "https://generativelanguage.googleapis.com/v1beta/openai", "gemini-embedding-001", "", "", true,
            "Free tier with rate limits. The same key as Gemini chat works.",
            "https://aistudio.google.com/apikey"),
        new("Mistral", "https://api.mistral.ai/v1", "mistral-embed", "", "", true,
            "Free \"Experiment\" plan with rate limits. The same key as Mistral chat works.",
            "https://console.mistral.ai/api-keys"),
        new("OpenAI", "https://api.openai.com/v1", "text-embedding-3-small", "", "", true,
            "Paid, but very cheap: loading all the knowledge costs a fraction of a cent.",
            "https://platform.openai.com/api-keys")
    ];

    // An address with no path (http://localhost:11434) is Ollama's own API, which can keep the model loaded and set
    // the context window. An address with a path (…/v1, …/openai) is called with the OpenAI chat format.
    public static bool IsOllamaApi(string baseUrl) =>
        Uri.TryCreate(baseUrl, UriKind.Absolute, out var url) && url.AbsolutePath.Trim('/').Length == 0;
}

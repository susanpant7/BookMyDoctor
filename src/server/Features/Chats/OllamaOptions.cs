namespace BookMyDoctor.Features.Chats;

// Where Ollama runs. The models and how they are used (prompt, temperature, history, retrieval, keep-alive) are on the
// Settings page.
public sealed class OllamaOptions
{
    public const string SectionName = "Ollama";

    public string BaseUrl { get; init; } = "http://localhost:11434";
}

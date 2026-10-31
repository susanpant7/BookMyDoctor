namespace BookMyDoctor.Features.Chats;

// Where Ollama runs. The models and how they are used (temperature, history, retrieval) are on the Settings page.
public sealed class OllamaOptions
{
    public const string SectionName = "Ollama";

    public string BaseUrl { get; init; } = "http://localhost:11434";

    // How long Ollama keeps the model loaded after a request, so a pause doesn't cost a reload.
    public string KeepAlive { get; init; } = "30m";
}

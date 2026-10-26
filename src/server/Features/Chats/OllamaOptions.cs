namespace BookMyDoctor.Features.Chats;

public sealed class OllamaOptions
{
    public const string SectionName = "Ollama";

    public string BaseUrl { get; init; } = "http://localhost:11434";
    public string Model { get; init; } = "";
    public string EmbeddingModel { get; init; } = "";

    // Tokens the model can see at once (system prompt + tools + history + retrieved knowledge).
    // Ollama's default is 4096 and it silently drops the start of longer prompts.
    public int ContextSize { get; init; } = 8192;

    // How long Ollama keeps the model loaded after a request, so a pause doesn't cost a reload.
    public string KeepAlive { get; init; } = "30m";

    // Most recent chat messages sent to the model. Older ones are dropped to keep prompts small and fast.
    public int MaxHistoryMessages { get; init; } = 12;

    // Randomness of the replies (Ollama's default is 0.8). Low values make the model follow its rules and call
    // tools more consistently instead of guessing.
    public double Temperature { get; init; } = 0.2;

    // How many recent patient messages keep their tool calls and results in the history sent to the model,
    // so follow-up questions ("who is he?", "how much?") still have the real data.
    public int ToolHistoryTurns { get; init; } = 3;
}

namespace BookMyDoctor.Features.Chats;

public sealed record OllamaMessage(string Role, string Content);

public sealed record OllamaChatRequest(string Model, IReadOnlyList<OllamaMessage> Messages, bool Stream);

public sealed record OllamaChatResponse(OllamaMessage Message, bool Done);

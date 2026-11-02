using MyHospitalAI.Features.Settings;

namespace MyHospitalAI.Features.Chats;

// Sends the conversation to a chat model and returns its next message: an answer, or tool calls (any text the model
// wrote before deciding to call a tool is kept in Content). While no tool call has appeared, `onText` gets the
// text so far, so the reply can be shown as it is written. Throws HttpRequestException when the model can't be
// reached or refuses the request; the chat then shows the "assistant is unavailable" reply.
public interface IChatModel
{
    Task<ModelMessage> ChatAsync(
        IReadOnlyList<ModelMessage> messages, IReadOnlyList<ModelTool> tools, AiSettings settings, Func<string, Task>? onText);
}

// Sends each request to Ollama's own API or to an OpenAI-format service, by the "Chat API address" setting, so a
// change on the Settings page applies to the next message.
public sealed class ChatModelRouter : IChatModel
{
    private readonly OllamaChatModel _ollama;
    private readonly OnlineChatModel _online;

    public ChatModelRouter(OllamaChatModel ollama, OnlineChatModel online)
    {
        _ollama = ollama;
        _online = online;
    }

    public Task<ModelMessage> ChatAsync(
        IReadOnlyList<ModelMessage> messages, IReadOnlyList<ModelTool> tools, AiSettings settings, Func<string, Task>? onText)
    {
        // The chat then shows the "assistant is unavailable" reply.
        if (settings.ChatBaseUrl.Length == 0 || settings.ChatModel.Length == 0)
        {
            throw new HttpRequestException("No chat model is selected yet. Choose one on Settings → AI assistant.");
        }
        return (AiProviders.IsOllamaApi(settings.ChatBaseUrl) ? (IChatModel)_ollama : _online).ChatAsync(messages, tools, settings, onText);
    }
}

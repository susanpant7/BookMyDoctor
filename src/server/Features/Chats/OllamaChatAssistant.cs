using BookMyDoctor.Persistence;
using Microsoft.Extensions.Options;

namespace BookMyDoctor.Features.Chats;

public sealed class OllamaChatAssistant : IChatAssistant
{
    private readonly HttpClient _http;
    private readonly OllamaOptions _options;
    private readonly IChatService _chatService;
    private readonly IUsersRepo _usersRepo;

    public OllamaChatAssistant(HttpClient http, IOptions<OllamaOptions> options, IChatService chatService, IUsersRepo usersRepo)
    {
        _http = http;
        _options = options.Value;
        _chatService = chatService;
        _usersRepo = usersRepo;
    }

    public async Task<string> ReplyAsync(ChatMessage patientMessage)
    {
        var name = _usersRepo.GetById(patientMessage.PatientId)?.Name ?? "there";

        // 1. Instructions for the model: who it is and how to behave.
        var messages = new List<OllamaMessage>
        {
            new("system",
                $"You are Vaidya Mitra, a friendly assistant for the BookMyDoctor app. " +
                $"You are talking to a patient named {name}. Help them find and book a doctor. " +
                $"Keep replies short. Do not give medical diagnoses.")
        };

        // 2. The whole conversation so far, so the model remembers context.
        //    (The patient's new message was already saved by the hub, so it's included here.)
        foreach (var message in _chatService.GetMessages(patientMessage.PatientId))
        {
            messages.Add(new(message.Sender == "patient" ? "user" : "assistant", message.Text));
        }

        // 3. Ask Ollama for a reply.
        var request = new OllamaChatRequest(_options.Model, messages, Stream: false);
        var response = await _http.PostAsJsonAsync("/api/chat", request);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<OllamaChatResponse>();
        return result?.Message.Content.Trim() ?? "Sorry, I couldn't think of a reply.";
    }
}

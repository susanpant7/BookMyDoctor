using BookMyDoctor.Persistence;

namespace BookMyDoctor.Features.Chats;

// Placeholder assistant; replace with the AI booking agent later.
public sealed class GreetingChatAssistant : IChatAssistant
{
    private readonly IUsersRepo _usersRepo;

    public GreetingChatAssistant(IUsersRepo usersRepo)
    {
        _usersRepo = usersRepo;
    }

    public Task<string> ReplyAsync(ChatMessage patientMessage)
    {
        var name = _usersRepo.GetById(patientMessage.PatientId)?.Name ?? "there";
        return Task.FromResult($"Hello {name}. How can I help you today? @{DateTimeOffset.UtcNow:HH:mm:ss}");
    }
}

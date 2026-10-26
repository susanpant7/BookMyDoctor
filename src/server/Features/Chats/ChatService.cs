using BookMyDoctor.Persistence;

namespace BookMyDoctor.Features.Chats;

public sealed class ChatService : IChatService
{
    private readonly IChatsRepo _chatsRepo;

    public ChatService(IChatsRepo chatsRepo)
    {
        _chatsRepo = chatsRepo;
    }

    public IReadOnlyList<ChatMessage> GetMessages(Guid patientId) =>
        _chatsRepo.GetMessages(patientId);

    public ChatMessage Send(SendMessageRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Text))
        {
            throw new ArgumentException("Message text is required.");
        }

        var sender = request.Sender.Equals("assistant", StringComparison.OrdinalIgnoreCase) ? "assistant" : "patient";
        var message = new ChatMessage(
            Guid.NewGuid(),
            request.PatientId,
            sender,
            request.Text.Trim(),
            DateTimeOffset.UtcNow);

        _chatsRepo.Add(message);
        return message;
    }
}

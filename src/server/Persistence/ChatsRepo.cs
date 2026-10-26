using BookMyDoctor.Features.Chats;

namespace BookMyDoctor.Persistence;

public sealed class ChatsRepo : IChatsRepo
{
    private readonly List<ChatMessage> _messages = [];

    public IReadOnlyList<ChatMessage> GetMessages(Guid patientId) =>
        _messages
            .Where(message => message.PatientId == patientId)
            .OrderBy(message => message.SentAt)
            .ToList();

    public void Add(ChatMessage message) => _messages.Add(message);
}

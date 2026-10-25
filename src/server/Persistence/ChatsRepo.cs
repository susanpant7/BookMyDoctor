using BookMyDoctor.Features.Chats;

namespace BookMyDoctor.Persistence;

public sealed class ChatsRepo : IChatsRepo
{
    private readonly List<ChatMessage> _messages = [.. StaticData.ChatMessages];

    public IReadOnlyList<ChatMessage> GetMessages(Guid doctorId, Guid patientId) =>
        _messages
            .Where(message => message.DoctorId == doctorId && message.PatientId == patientId)
            .OrderBy(message => message.SentAt)
            .ToList();

    public void Add(ChatMessage message) => _messages.Add(message);
}

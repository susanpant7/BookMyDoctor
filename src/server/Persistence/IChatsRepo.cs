using BookMyDoctor.Features.Chats;

namespace BookMyDoctor.Persistence;

public interface IChatsRepo
{
    IReadOnlyList<ChatMessage> GetMessages(Guid doctorId, Guid patientId);
    void Add(ChatMessage message);
}

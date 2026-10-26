using BookMyDoctor.Features.Chats;

namespace BookMyDoctor.Persistence;

public interface IChatsRepo
{
    IReadOnlyList<ChatMessage> GetMessages(Guid patientId);
    void Add(ChatMessage message);
}

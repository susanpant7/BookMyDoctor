using MyHospitalAI.Features.Chats;

namespace MyHospitalAI.Persistence;

public interface IChatsRepo
{
    IReadOnlyList<ChatMessage> GetMessages(Guid patientId);
    void Add(ChatMessage message);
}

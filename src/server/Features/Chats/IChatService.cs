namespace BookMyDoctor.Features.Chats;

public interface IChatService
{
    IReadOnlyList<ChatMessage> GetMessages(Guid patientId);
    ChatMessage Save(SaveMessageRequest request);
}

namespace BookMyDoctor.Features.Chats;

public interface IChatService
{
    IReadOnlyList<ChatMessage> GetMessages(Guid patientId);
    ChatMessage Send(SendMessageRequest request);
}

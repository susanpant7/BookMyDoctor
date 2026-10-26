namespace BookMyDoctor.Features.Chats;

public interface IChatAssistant
{
    Task<string> ReplyAsync(ChatMessage patientMessage);
}

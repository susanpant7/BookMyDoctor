using BookMyDoctor.Features.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace BookMyDoctor.Features.Chats;

[Authorize]
public sealed class ChatHub : Hub
{
    private readonly IChatService _chatService;
    private readonly IChatAssistant _assistant;

    public ChatHub(IChatService chatService, IChatAssistant assistant)
    {
        _chatService = chatService;
        _assistant = assistant;
    }

    private Guid PatientId => Context.User!.GetUserId();

    // Client calls this after connecting, to start receiving messages for the signed-in patient's chat.
    public Task JoinChat() =>
        Groups.AddToGroupAsync(Context.ConnectionId, GroupName(PatientId));

    // Client calls this to send a message; every tab in the patient's chat receives it via "ReceiveMessage",
    // followed by the assistant's reply.
    public async Task SendMessage(NewMessageRequest request)
    {
        var patientId = PatientId;
        ChatMessage message;
        try
        {
            message = _chatService.Send(new SendMessageRequest(patientId, "patient", request.Text));
        }
        catch (ArgumentException ex)
        {
            throw new HubException(ex.Message);
        }

        var group = Clients.Group(GroupName(patientId));
        await group.SendAsync("ReceiveMessage", message);

        var replyText = await _assistant.ReplyAsync(message);
        var reply = _chatService.Send(new SendMessageRequest(patientId, "assistant", replyText));
        await group.SendAsync("ReceiveMessage", reply);
    }

    private static string GroupName(Guid patientId) => $"chat:{patientId}";
}

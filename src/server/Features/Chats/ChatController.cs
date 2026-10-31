using BookMyDoctor.Features.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BookMyDoctor.Features.Chats;

[ApiController]
[Authorize(Roles = Roles.Patient)]
[Route("api/chats")]
public sealed class ChatController : ControllerBase
{
    private readonly IChatService _chatService;

    public ChatController(IChatService chatService)
    {
        _chatService = chatService;
    }

    [HttpGet]
    public ActionResult<IReadOnlyList<ChatMessage>> GetMessages() =>
        Ok(_chatService.GetMessages(User.GetUserId()));

    [HttpPost]
    public ActionResult<ChatMessage> Send(NewMessageRequest request)
    {
        try
        {
            return Ok(_chatService.Send(new SendMessageRequest(User.GetUserId(), "patient", request.Text)));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}

using Microsoft.AspNetCore.Mvc;

namespace BookMyDoctor.Features.Chats;

[ApiController]
[Route("api/chats")]
public sealed class ChatController : ControllerBase
{
    private readonly IChatService _chatService;

    public ChatController(IChatService chatService)
    {
        _chatService = chatService;
    }

    [HttpGet]
    public ActionResult<IReadOnlyList<ChatMessage>> GetMessages([FromQuery] Guid doctorId, [FromQuery] Guid patientId) =>
        Ok(_chatService.GetMessages(doctorId, patientId));

    [HttpPost]
    public ActionResult<ChatMessage> Send(SendMessageRequest request)
    {
        try
        {
            return Ok(_chatService.Send(request));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}

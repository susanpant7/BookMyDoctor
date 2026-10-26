using BookMyDoctor.Features.Appointments;
using BookMyDoctor.Features.Auth;
using BookMyDoctor.Features.Chats.Tools;
using BookMyDoctor.Features.Doctors;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace BookMyDoctor.Features.Chats;

[Authorize]
public sealed class ChatHub : Hub
{
    private readonly IChatService _chatService;
    private readonly IChatAssistant _assistant;
    private readonly BookingDrafts _drafts;
    private readonly IAppointmentService _appointmentService;
    private readonly IDoctorService _doctorService;

    public ChatHub(
        IChatService chatService,
        IChatAssistant assistant,
        BookingDrafts drafts,
        IAppointmentService appointmentService,
        IDoctorService doctorService)
    {
        _chatService = chatService;
        _assistant = assistant;
        _drafts = drafts;
        _appointmentService = appointmentService;
        _doctorService = doctorService;
    }

    private Guid PatientId => Context.User!.GetUserId();

    // Client calls this after connecting, to start receiving messages for the signed-in patient's chat.
    public Task JoinChat() =>
        Groups.AddToGroupAsync(Context.ConnectionId, GroupName(PatientId));

    // Client calls this to send a message; every tab in the patient's chat receives it via "ReceiveMessage",
    // followed by the assistant's reply, and time buttons via "BookingOptions" if the assistant looked up free times.
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
        await group.SendAsync("BookingOptions", null);   // the conversation moved on, so hide old time buttons

        string replyText;
        await group.SendAsync("AssistantTyping", true);
        try
        {
            replyText = await _assistant.ReplyAsync(message);
        }
        catch (HttpRequestException)
        {
            replyText = "Sorry, I'm having trouble right now. Please try again in a moment.";
        }
        catch (TaskCanceledException)   // the 2-minute timeout was hit
        {
            replyText = "Sorry, that took too long. Please try again.";
        }
        finally
        {
            // Always clear the indicator, even if the assistant throws something unexpected.
            await group.SendAsync("AssistantTyping", false);
        }

        var reply = _chatService.Send(new SendMessageRequest(patientId, "assistant", replyText));
        await group.SendAsync("ReceiveMessage", reply);

        // If the assistant looked up free times while answering this message, show them as buttons.
        if (_drafts.GetOffer(patientId) is { } offer && offer.OfferedInMessageId == message.Id)
        {
            await group.SendAsync("BookingOptions", new BookingOptions(
                offer.DoctorId,
                offer.DoctorName,
                $"{offer.Date:dddd d MMMM}",
                offer.Slots.Select(slot => new SlotOption(slot.StartsAt, ToolFormat.Time(slot.StartsAt))).ToList()));
        }
    }

    // Client calls this when the patient confirms a time from the buttons. Books directly (no AI involved)
    // and posts the result into the chat so the assistant and every tab see it.
    public async Task BookSlot(BookSlotRequest request)
    {
        var patientId = PatientId;
        Appointment appointment;
        try
        {
            appointment = _appointmentService.Book(patientId, new BookAppointmentRequest(request.DoctorId, request.StartsAt, request.Reason));
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or KeyNotFoundException)
        {
            throw new HubException(ex.Message);
        }

        _drafts.Clear(patientId);
        var doctorName = _doctorService.GetById(appointment.DoctorId)?.Name ?? "the doctor";
        var confirmation = _chatService.Send(new SendMessageRequest(patientId, "assistant",
            $"Booked: {doctorName}, {ToolFormat.Date(HospitalTime.DateOf(appointment.StartsAt))} at {ToolFormat.Time(appointment.StartsAt)}, " +
            $"for {appointment.Reason}. Your reference is {BookingReference.For(appointment.Id)}."));

        var group = Clients.Group(GroupName(patientId));
        await group.SendAsync("BookingOptions", null);
        await group.SendAsync("ReceiveMessage", confirmation);
    }

    private static string GroupName(Guid patientId) => $"chat:{patientId}";
}

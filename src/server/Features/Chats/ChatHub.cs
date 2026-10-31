using System.Text.RegularExpressions;
using BookMyDoctor.Features.Appointments;
using BookMyDoctor.Features.Auth;
using BookMyDoctor.Features.Chats.Tools;
using BookMyDoctor.Features.Doctors;
using BookMyDoctor.Features.Settings;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace BookMyDoctor.Features.Chats;

[Authorize(Roles = Roles.Patient)]
public sealed partial class ChatHub : Hub
{
    private readonly IChatService _chatService;
    private readonly IChatAssistant _assistant;
    private readonly BookingDrafts _drafts;
    private readonly IAppointmentService _appointmentService;
    private readonly IDoctorService _doctorService;
    private readonly IAiSettingsProvider _settings;

    public ChatHub(
        IChatService chatService,
        IChatAssistant assistant,
        BookingDrafts drafts,
        IAppointmentService appointmentService,
        IDoctorService doctorService,
        IAiSettingsProvider settings)
    {
        _chatService = chatService;
        _assistant = assistant;
        _drafts = drafts;
        _appointmentService = appointmentService;
        _doctorService = doctorService;
        _settings = settings;
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
            message = _chatService.Save(new SaveMessageRequest(patientId, "patient", request.Text));
        }
        catch (ArgumentException ex)
        {
            throw new HubException(ex.Message);
        }

        var group = Clients.Group(GroupName(patientId));
        await group.SendAsync("ReceiveMessage", message);
        await group.SendAsync("BookingOptions", null);   // the conversation moved on, so hide old time buttons

        if (await TryConfirmProposedBookingAsync(patientId, message, group))
        {
            return;
        }

        AssistantReply assistantReply;
        await group.SendAsync("AssistantTyping", true);
        try
        {
            assistantReply = await _assistant.ReplyAsync(message, new GroupReplyDraft(group));
        }
        // Ollama can't be reached, or the 2-minute timeout was hit.
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            assistantReply = new AssistantReply(_settings.Get().UnavailableReply);
        }
        finally
        {
            // Always clear the indicator, even if the assistant throws something unexpected.
            await group.SendAsync("AssistantTyping", false);
        }

        // The final, checked reply replaces whatever draft was shown while it was being written.
        var reply = _chatService.Save(new SaveMessageRequest(patientId, "assistant", assistantReply.Text));
        await group.SendAsync("ReceiveMessage", reply);
        await group.SendAsync("AssistantDraft", null);

        // If free times were looked up for this reply, show them as buttons right under it.
        if (assistantReply.SlotsShown is { } slots)
        {
            await group.SendAsync("BookingOptions", new BookingOptions(
                slots.DoctorId,
                slots.DoctorName,
                $"{slots.Date:dddd d MMMM}",
                slots.Slots.Select(slot => new SlotOption(slot.StartsAt, ToolFormat.Time(slot.StartsAt))).ToList()));
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

        await PostBookingConfirmationAsync(patientId, appointment, Clients.Group(GroupName(patientId)));
    }

    // A clear "yes" straight after the assistant proposed a booking books it here, like the Confirm button, instead of
    // trusting the model to make the booking call (small models sometimes just claim it is booked).
    private async Task<bool> TryConfirmProposedBookingAsync(Guid patientId, ChatMessage message, IClientProxy group)
    {
        if (_drafts.GetPending(patientId) is not { } pending || !IsClearYes(message.Text))
        {
            return false;
        }

        // Only a proposal made in reply to the patient's previous message counts, so an old proposal can't be
        // confirmed by a "yes" meant for a different question.
        var previousPatientMessage = _chatService.GetMessages(patientId)
            .LastOrDefault(earlier => earlier.Sender == "patient" && earlier.Id != message.Id);
        if (previousPatientMessage?.Id != pending.ProposedInMessageId)
        {
            return false;
        }

        try
        {
            var appointment = _appointmentService.Book(patientId, new BookAppointmentRequest(pending.DoctorId, pending.StartsAt, pending.Reason));
            await PostBookingConfirmationAsync(patientId, appointment, group);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or KeyNotFoundException)
        {
            // e.g. someone else just took the time; the patient can pick another one.
            _drafts.Clear(patientId);
            var sorry = _chatService.Save(new SaveMessageRequest(patientId, "assistant", $"Sorry, I couldn't book that: {ex.Message}"));
            await group.SendAsync("ReceiveMessage", sorry);
        }
        return true;
    }

    // "yes", "yes please", "ok", "sure", "confirm", "book it", "go ahead", "that's right"…, with nothing else in the message.
    internal static bool IsClearYes(string text) => ClearYes().IsMatch(text.Trim().ToLowerInvariant());

    [GeneratedRegex(@"^(?:yes|yeah|yep|yup|ok|okay|sure|correct|confirm|confirmed|go ahead|please do|do it|book it|perfect|sounds good|that'?s (?:right|correct|fine|good))(?:[\s,]+(?:please|yes|book it|go ahead|confirm|thanks|thank you|that'?s (?:right|correct)|correct))*[\s.!]*$")]
    private static partial Regex ClearYes();

    private async Task PostBookingConfirmationAsync(Guid patientId, Appointment appointment, IClientProxy group)
    {
        _drafts.Clear(patientId);
        var doctorName = _doctorService.GetById(appointment.DoctorId)?.Name ?? "the doctor";
        var confirmation = _chatService.Save(new SaveMessageRequest(patientId, "assistant",
            $"Booked: {doctorName}, {ToolFormat.Date(HospitalTime.DateOf(appointment.StartsAt))} at {ToolFormat.Time(appointment.StartsAt)}, " +
            $"for {appointment.Reason}. Your reference is {BookingReference.For(appointment.Id)}."));

        await group.SendAsync("BookingOptions", null);
        await group.SendAsync("ReceiveMessage", confirmation);
    }

    private static string GroupName(Guid patientId) => $"chat:{patientId}";

    // Shows the reply in every tab of the patient's chat while it's being written ("AssistantDraft"; null = remove it).
    private sealed class GroupReplyDraft(IClientProxy group) : IReplyDraft
    {
        public Task ShowAsync(string textSoFar) => group.SendAsync("AssistantDraft", textSoFar);

        public Task ClearAsync() => group.SendAsync("AssistantDraft", null);
    }
}

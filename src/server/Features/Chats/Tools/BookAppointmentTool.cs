using System.Text.Json;
using BookMyDoctor.Features.Appointments;
using BookMyDoctor.Features.Doctors;

namespace BookMyDoctor.Features.Chats.Tools;

// Books in two steps so the patient always confirms first, whatever the model does:
//   1. A call with a time only checks it is free, remembers it as pending, and asks the model to confirm.
//   2. A call while answering a LATER patient message, with no details or the same ones, books the pending one.
// A real patient reply therefore always sits between proposing and booking.
// Any details the model leaves out are filled in from the server's booking state (BookingDrafts): the doctor and
// day from the last times offered, and the reason from the pending proposal.
public sealed class BookAppointmentTool : IAssistantTool
{
    private readonly IDoctorService _doctorService;
    private readonly IAvailabilityService _availabilityService;
    private readonly IAppointmentService _appointmentService;
    private readonly BookingDrafts _drafts;
    private readonly TimeProvider _time;

    public BookAppointmentTool(
        IDoctorService doctorService,
        IAvailabilityService availabilityService,
        IAppointmentService appointmentService,
        BookingDrafts drafts,
        TimeProvider time)
    {
        _doctorService = doctorService;
        _availabilityService = availabilityService;
        _appointmentService = appointmentService;
        _drafts = drafts;
        _time = time;
    }

    public string Name => "book_appointment";

    public string Description =>
        "Book an appointment for the patient, in two steps. " +
        "Step 1, when the patient picks a time: call with the time and reason (doctor and date can be left out if you just showed " +
        "times for them). This does NOT book; it returns a summary to confirm with the patient. " +
        "Step 2, after the patient says yes: call with no arguments to book it.";

    public JsonElement Parameters { get; } = ToolArguments.Schema("""
        {
          "type": "object",
          "properties": {
            "time": { "type": "string", "description": "Start time in 24-hour HH:mm, e.g. 14:20. Leave out when confirming." },
            "reason": { "type": "string", "description": "The patient's health problem in a few words, e.g. itchy rash on arm" },
            "doctor": { "type": "string", "description": "Optional: the doctor's name, if different from the times just shown" },
            "date": { "type": "string", "description": "Optional: the day (e.g. tuesday or YYYY-MM-DD), if different from the times just shown" }
          },
          "required": []
        }
        """);

    public Task<string> RunAsync(JsonElement arguments, ToolContext context, CancellationToken cancellationToken)
    {
        var patientId = context.PatientId;
        var offer = _drafts.GetOffer(patientId);
        var pending = _drafts.GetPending(patientId);
        var reason = ToolArguments.OptionalString(arguments, "reason");

        var doctor = ToolArguments.OptionalString(arguments, "doctor") is { } doctorText
            ? DoctorLookup.Find(_doctorService, doctorText)
            : null;
        var date = ToolArguments.OptionalDate(arguments, "date", HospitalTime.Today(_time));
        var time = ToolArguments.OptionalTime(arguments, "time");

        // Step 2: confirm the pending booking (no details given, or the same ones), in a later turn than it was proposed.
        if (pending is not null && pending.ProposedInMessageId != context.MessageId && MatchesPending(pending, doctor, date, time))
        {
            _drafts.Clear(patientId);
            var appointment = _appointmentService.Book(
                patientId, new BookAppointmentRequest(pending.DoctorId, pending.StartsAt, reason ?? pending.Reason));
            var bookedDoctor = _doctorService.GetById(appointment.DoctorId)!;
            return Task.FromResult(
                $"Booked. Reference {BookingReference.For(appointment.Id)}: {bookedDoctor.Name}, " +
                $"{ToolFormat.Date(HospitalTime.DateOf(appointment.StartsAt))}, " +
                $"{ToolFormat.Time(appointment.StartsAt)}–{ToolFormat.Time(appointment.EndsAt)}, reason: {appointment.Reason}. " +
                "Tell the patient it is booked and give them the reference.");
        }

        // Step 1: propose. Fill in whatever the model left out from the times last shown to the patient.
        doctor ??= offer is null ? null : _doctorService.GetById(offer.DoctorId);
        if (doctor is null)
        {
            return Task.FromResult("Not booked: which doctor? Look up free times with get_available_slots first.");
        }

        date ??= offer?.DoctorId == doctor.Id ? offer.Date : null;
        if (date is null)
        {
            return Task.FromResult($"Not booked: which day for {doctor.Name}? Look up free times with get_available_slots first.");
        }

        if (time is null)
        {
            return Task.FromResult("Not booked: ask the patient which time they want.");
        }

        reason ??= pending?.Reason;
        if (reason is null || reason.Length < 3)
        {
            return Task.FromResult("Not booked: ask the patient what the visit is for (their health problem in a few words).");
        }

        var startsAt = HospitalTime.At(date.Value, time.Value);
        var slot = _availabilityService.GetAvailableSlots(doctor.Id, date.Value, 1).FirstOrDefault(slot => slot.StartsAt == startsAt);
        if (slot is null)
        {
            return Task.FromResult(
                $"Not booked: {doctor.Name} is not free at {ToolFormat.Time(startsAt)} on {ToolFormat.Date(date.Value)}. " +
                "Use get_available_slots to find free times and offer those instead.");
        }

        _drafts.SetPending(patientId, new PendingBooking(doctor.Id, startsAt, reason, context.MessageId));
        return Task.FromResult(
            $"NOT BOOKED YET. Ask the patient to confirm: {doctor.Name}, {ToolFormat.Date(date.Value)}, " +
            $"{ToolFormat.Time(slot.StartsAt)}–{ToolFormat.Time(slot.EndsAt)}, reason: {reason}. " +
            "If they say yes, call book_appointment again with no arguments.");
    }

    private static bool MatchesPending(PendingBooking pending, Doctor? doctor, DateOnly? date, TimeOnly? time)
    {
        if (time is null && doctor is null && date is null)
        {
            return true;
        }

        var pendingLocal = TimeZoneInfo.ConvertTime(pending.StartsAt, HospitalTime.Zone);
        return (doctor is null || doctor.Id == pending.DoctorId) &&
               (date is null || date == DateOnly.FromDateTime(pendingLocal.DateTime)) &&
               (time is null || time == TimeOnly.FromDateTime(pendingLocal.DateTime));
    }
}

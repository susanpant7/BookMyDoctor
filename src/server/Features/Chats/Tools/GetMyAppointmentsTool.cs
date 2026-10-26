using System.Text.Json;
using BookMyDoctor.Features.Appointments;
using BookMyDoctor.Features.Doctors;

namespace BookMyDoctor.Features.Chats.Tools;

public sealed class GetMyAppointmentsTool : IAssistantTool
{
    private readonly IAppointmentService _appointmentService;
    private readonly IDoctorService _doctorService;
    private readonly TimeProvider _time;

    public GetMyAppointmentsTool(IAppointmentService appointmentService, IDoctorService doctorService, TimeProvider time)
    {
        _appointmentService = appointmentService;
        _doctorService = doctorService;
        _time = time;
    }

    public string Name => "get_my_appointments";

    public string Description =>
        "The signed-in patient's own appointments, upcoming and past. Use when the patient asks about their bookings or appointments.";

    public JsonElement Parameters { get; } = ToolArguments.Schema("""{ "type": "object", "properties": {}, "required": [] }""");

    // The patient comes from the signed-in user's token (context), never from the model's arguments.
    public Task<string> RunAsync(JsonElement arguments, ToolContext context, CancellationToken cancellationToken)
    {
        var appointments = _appointmentService.GetForPatient(context.PatientId);
        if (appointments.Count == 0)
        {
            return Task.FromResult("The patient has no appointments.");
        }

        var now = _time.GetUtcNow();
        var upcoming = appointments.Where(appointment => appointment.Status == AppointmentStatus.Scheduled && appointment.StartsAt > now).ToList();
        var other = appointments.Except(upcoming).ToList();

        return Task.FromResult(
            $"Upcoming:\n{Lines(upcoming)}\nPast or cancelled:\n{Lines(other)}");
    }

    private string Lines(IReadOnlyList<Appointment> appointments) =>
        appointments.Count == 0
            ? "- none"
            : string.Join("\n", appointments.Select(appointment =>
                $"- {ToolFormat.Date(HospitalTime.DateOf(appointment.StartsAt))} {ToolFormat.Time(appointment.StartsAt)}–{ToolFormat.Time(appointment.EndsAt)} | " +
                $"{_doctorService.GetById(appointment.DoctorId)?.Name ?? "Unknown doctor"} | {appointment.Reason} | {appointment.Status} | " +
                BookingReference.For(appointment.Id)));
}

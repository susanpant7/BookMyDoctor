using System.Text.Json.Serialization;

namespace BookMyDoctor.Features.Appointments;

[JsonConverter(typeof(JsonStringEnumConverter<AppointmentStatus>))]
public enum AppointmentStatus
{
    Scheduled,
    Completed,
    Cancelled
}

// A recurring weekly block when a doctor sees patients at the hospital, in hospital-local time.
public sealed record DoctorSchedule(
    Guid DoctorId,
    DayOfWeek Day,
    TimeOnly Start,
    TimeOnly End,
    int SlotMinutes);

// A whole day the doctor is unavailable (holiday, leave, conference).
public sealed record DoctorTimeOff(
    Guid DoctorId,
    DateOnly Date,
    string Reason);

public sealed record Appointment(
    Guid Id,
    Guid DoctorId,
    Guid PatientId,
    DateTimeOffset StartsAt,
    DateTimeOffset EndsAt,
    string Reason,
    AppointmentStatus Status,
    DateTimeOffset BookedAt);

// A free, bookable slot calculated from schedules, time off and existing appointments (not stored).
public sealed record TimeSlot(
    Guid DoctorId,
    DateTimeOffset StartsAt,
    DateTimeOffset EndsAt);

// What the client sends to book; the patient comes from the signed-in user's token.
public sealed record BookAppointmentRequest(
    Guid DoctorId,
    DateTimeOffset StartsAt,
    string Reason);

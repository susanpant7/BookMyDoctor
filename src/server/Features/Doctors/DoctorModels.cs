namespace BookMyDoctor.Features.Doctors;

public sealed record Doctor(
    Guid Id,
    string Name,
    Guid SpecialtyId,
    string Specialty,
    int ExperienceYears,
    decimal ConsultationFee,
    decimal FollowUpFee,
    IReadOnlyList<string> Languages,
    string Bio);

// A weekly block when the doctor sees patients, in hospital-local time. Day: 0 = Sunday … 6 = Saturday.
public sealed record ScheduleBlock(DayOfWeek Day, TimeOnly Start, TimeOnly End, int SlotMinutes);

public sealed record DoctorRequest(
    string Name,
    Guid SpecialtyId,
    int ExperienceYears,
    decimal ConsultationFee,
    decimal FollowUpFee,
    IReadOnlyList<string>? Languages,
    string? Bio,
    IReadOnlyList<ScheduleBlock>? Schedule);

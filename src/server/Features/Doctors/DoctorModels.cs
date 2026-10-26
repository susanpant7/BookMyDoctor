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

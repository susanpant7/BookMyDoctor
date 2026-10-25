namespace BookMyDoctor.Features.Doctors;

public sealed record Doctor(
    Guid Id,
    string Name,
    Guid SpecialtyId,
    string Specialty,
    string Location,
    int ExperienceYears,
    decimal ConsultationFee);

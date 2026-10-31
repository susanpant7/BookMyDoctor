namespace BookMyDoctor.Features.Specialties;

public sealed record Specialty(Guid Id, string Name, string Description, int DoctorCount = 0);

public sealed record SpecialtyRequest(string Name, string? Description);

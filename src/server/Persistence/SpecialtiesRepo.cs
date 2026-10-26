using BookMyDoctor.Features.Specialties;
using static BookMyDoctor.Persistence.SeedIds;

namespace BookMyDoctor.Persistence;

public sealed class SpecialtiesRepo : ISpecialtiesRepo
{
    private static readonly IReadOnlyList<Specialty> Seed =
    [
        new(CardiologyId, "Cardiology", "Heart and blood vessel care"),
        new(DermatologyId, "Dermatology", "Skin, hair, and nail care"),
        new(PediatricsId, "Pediatrics", "Care for infants, children, and teens"),
        new(OrthopedicsId, "Orthopedics", "Bones, joints, and muscles"),
        new(GeneralPracticeId, "General Practice", "Primary care and routine checkups")
    ];

    private readonly IReadOnlyList<Specialty> _specialties = Seed;

    public IReadOnlyList<Specialty> GetAll() => _specialties;

    public Specialty? GetById(Guid id) =>
        _specialties.FirstOrDefault(specialty => specialty.Id == id);
}

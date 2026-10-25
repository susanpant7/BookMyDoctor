using BookMyDoctor.Features.Specialties;

namespace BookMyDoctor.Persistence;

public sealed class SpecialtiesRepo : ISpecialtiesRepo
{
    private readonly IReadOnlyList<Specialty> _specialties = StaticData.Specialties;

    public IReadOnlyList<Specialty> GetAll() => _specialties;

    public Specialty? GetById(Guid id) =>
        _specialties.FirstOrDefault(specialty => specialty.Id == id);
}

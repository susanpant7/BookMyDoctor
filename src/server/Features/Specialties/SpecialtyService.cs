using BookMyDoctor.Persistence;

namespace BookMyDoctor.Features.Specialties;

public sealed class SpecialtyService : ISpecialtyService
{
    private readonly ISpecialtiesRepo _specialtiesRepo;

    public SpecialtyService(ISpecialtiesRepo specialtiesRepo)
    {
        _specialtiesRepo = specialtiesRepo;
    }

    public IReadOnlyList<Specialty> GetAll() => _specialtiesRepo.GetAll();

    public Specialty? GetById(Guid id) => _specialtiesRepo.GetById(id);
}

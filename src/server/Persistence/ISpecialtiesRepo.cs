using BookMyDoctor.Features.Specialties;

namespace BookMyDoctor.Persistence;

public interface ISpecialtiesRepo
{
    IReadOnlyList<Specialty> GetAll();
    Specialty? GetById(Guid id);
}

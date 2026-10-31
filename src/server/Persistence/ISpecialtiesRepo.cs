using BookMyDoctor.Features.Specialties;

namespace BookMyDoctor.Persistence;

public interface ISpecialtiesRepo
{
    IReadOnlyList<Specialty> GetAll();
    Specialty? GetById(Guid id);
    Specialty? GetByName(string name);
    void Add(Specialty specialty);

    // False when no specialty has the id.
    bool Update(Specialty specialty);

    // False when no specialty has the id. Doctors of the specialty must be removed or moved first.
    bool Delete(Guid id);
}

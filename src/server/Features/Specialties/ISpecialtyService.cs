namespace BookMyDoctor.Features.Specialties;

public interface ISpecialtyService
{
    IReadOnlyList<Specialty> GetAll();
    Specialty? GetById(Guid id);
}

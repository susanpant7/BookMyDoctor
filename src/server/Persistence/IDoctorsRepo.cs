using BookMyDoctor.Features.Doctors;

namespace BookMyDoctor.Persistence;

public interface IDoctorsRepo
{
    IReadOnlyList<Doctor> GetAll();
    IReadOnlyList<Doctor> GetBySpecialty(Guid specialtyId);
    Doctor? GetById(Guid id);
}

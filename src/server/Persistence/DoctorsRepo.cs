using BookMyDoctor.Features.Doctors;

namespace BookMyDoctor.Persistence;

public sealed class DoctorsRepo : IDoctorsRepo
{
    private readonly IReadOnlyList<Doctor> _doctors = StaticData.Doctors;

    public IReadOnlyList<Doctor> GetAll() => _doctors;

    public IReadOnlyList<Doctor> GetBySpecialty(Guid specialtyId) =>
        _doctors.Where(doctor => doctor.SpecialtyId == specialtyId).ToList();

    public Doctor? GetById(Guid id) => _doctors.FirstOrDefault(doctor => doctor.Id == id);
}

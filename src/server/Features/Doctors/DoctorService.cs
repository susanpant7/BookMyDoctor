using BookMyDoctor.Persistence;

namespace BookMyDoctor.Features.Doctors;

public sealed class DoctorService : IDoctorService
{
    private readonly IDoctorsRepo _doctorsRepo;

    public DoctorService(IDoctorsRepo doctorsRepo)
    {
        _doctorsRepo = doctorsRepo;
    }

    public IReadOnlyList<Doctor> GetAll(Guid? specialtyId = null) =>
        specialtyId is null ? _doctorsRepo.GetAll() : _doctorsRepo.GetBySpecialty(specialtyId.Value);

    public Doctor? GetById(Guid id) => _doctorsRepo.GetById(id);
}

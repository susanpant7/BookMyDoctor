namespace BookMyDoctor.Features.Doctors;

public interface IDoctorService
{
    IReadOnlyList<Doctor> GetAll(Guid? specialtyId = null);
    Doctor? GetById(Guid id);
}

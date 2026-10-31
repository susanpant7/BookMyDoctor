using BookMyDoctor.Features.Doctors;

namespace BookMyDoctor.Persistence;

public interface IDoctorsRepo
{
    IReadOnlyList<Doctor> GetAll();
    IReadOnlyList<Doctor> GetBySpecialty(Guid specialtyId);
    Doctor? GetById(Guid id);
    Doctor? GetByName(string name);
    void Add(Doctor doctor);

    // False when no doctor has the id. The specialty name of the record is ignored; the specialty id decides.
    bool Update(Doctor doctor);

    // False when no doctor has the id. The weekly schedule goes with the doctor.
    bool Delete(Guid id);

    bool HasAppointments(Guid id);
}

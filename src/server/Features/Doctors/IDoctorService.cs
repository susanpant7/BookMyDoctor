using MyHospitalAI.Features.Common;

namespace MyHospitalAI.Features.Doctors;

public interface IDoctorService
{
    IReadOnlyList<Doctor> GetAll(Guid? specialtyId = null);
    Doctor? GetById(Guid id);
    IReadOnlyList<ScheduleBlock> GetSchedule(Guid id);

    // These throw ArgumentException for invalid input, KeyNotFoundException for an unknown id and
    // ConflictException when the name is taken or (for Delete) the doctor has appointments.
    Doctor Create(DoctorRequest request);
    Doctor Update(Guid id, DoctorRequest request);
    void Delete(Guid id);

    // Adds the default doctors (with their weekly schedules and logins, all with this password) that do not exist yet.
    // Their specialties must exist; throws ArgumentException when one is missing.
    LoadDefaultsResult LoadDefaults(string password);
}

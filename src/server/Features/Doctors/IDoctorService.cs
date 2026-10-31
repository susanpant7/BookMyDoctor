using BookMyDoctor.Features.Common;

namespace BookMyDoctor.Features.Doctors;

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

    // Adds the default doctors (with their weekly schedules) that do not exist yet (by name).
    // Their specialties must exist; throws ArgumentException when one is missing.
    LoadDefaultsResult LoadDefaults();
}

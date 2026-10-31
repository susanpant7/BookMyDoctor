using BookMyDoctor.Features.Appointments;

namespace BookMyDoctor.Persistence;

public interface IDoctorSchedulesRepo
{
    IReadOnlyList<DoctorSchedule> GetWeekly(Guid doctorId);

    // Replaces the doctor's whole weekly schedule.
    void ReplaceWeekly(Guid doctorId, IReadOnlyList<DoctorSchedule> blocks);

    // Days the doctor is off in [from, to], including days the whole hospital is closed.
    IReadOnlyList<DoctorTimeOff> GetTimeOff(Guid doctorId, DateOnly from, DateOnly to);

    void AddTimeOff(DoctorTimeOff timeOff);
}

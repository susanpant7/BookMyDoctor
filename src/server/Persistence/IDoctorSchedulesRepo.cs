using BookMyDoctor.Features.Appointments;

namespace BookMyDoctor.Persistence;

public interface IDoctorSchedulesRepo
{
    IReadOnlyList<DoctorSchedule> GetWeekly(Guid doctorId);
    IReadOnlyList<DoctorTimeOff> GetTimeOff(Guid doctorId, DateOnly from, DateOnly to);
}

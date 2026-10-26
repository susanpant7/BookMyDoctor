using BookMyDoctor.Features.Appointments;
using static BookMyDoctor.Persistence.SeedDates;
using static BookMyDoctor.Persistence.SeedIds;

namespace BookMyDoctor.Persistence;

public sealed class DoctorSchedulesRepo : IDoctorSchedulesRepo
{
    // Weekly hours each doctor sees patients at the hospital.
    internal static readonly IReadOnlyList<DoctorSchedule> SeedSchedules =
    [
        .. Weekly(CardiologyDoctorId, [DayOfWeek.Monday, DayOfWeek.Wednesday, DayOfWeek.Friday], 9, 16, 30),
        .. Weekly(DermatologyDoctorId, [DayOfWeek.Tuesday, DayOfWeek.Thursday], 10, 18, 20),
        .. Weekly(DermatologyDoctorId, [DayOfWeek.Saturday], 9, 13, 20),
        .. Weekly(PediatricsDoctorId, Weekdays, 8, 15, 20),
        .. Weekly(OrthopedicsDoctorId, [DayOfWeek.Tuesday, DayOfWeek.Thursday], 8, 17, 30),
        .. Weekly(OrthopedicsDoctorId, [DayOfWeek.Saturday], 9, 12, 30),
        .. Weekly(GeneralPracticeDoctorId, [.. Weekdays, DayOfWeek.Saturday], 9, 19, 15)
    ];

    private static readonly IReadOnlyList<DoctorTimeOff> SeedTimeOff =
    [
        .. from doctor in DoctorsRepo.Seed
           from holiday in HospitalHolidays(Today.Year).Concat(HospitalHolidays(Today.Year + 1))
           select new DoctorTimeOff(doctor.Id, holiday.Date, holiday.Name),
        new(DermatologyDoctorId, InWeeks(1, DayOfWeek.Thursday), "Dermatology conference"),
        new(OrthopedicsDoctorId, InWeeks(2, DayOfWeek.Tuesday), "Annual leave")
    ];

    private readonly IReadOnlyList<DoctorSchedule> _schedules;
    private readonly IReadOnlyList<DoctorTimeOff> _timeOff;

    public DoctorSchedulesRepo() : this(SeedSchedules, SeedTimeOff)
    {
    }

    internal DoctorSchedulesRepo(IReadOnlyList<DoctorSchedule> schedules, IReadOnlyList<DoctorTimeOff> timeOff)
    {
        _schedules = schedules;
        _timeOff = timeOff;
    }

    public IReadOnlyList<DoctorSchedule> GetWeekly(Guid doctorId) =>
        _schedules.Where(schedule => schedule.DoctorId == doctorId).ToList();

    public IReadOnlyList<DoctorTimeOff> GetTimeOff(Guid doctorId, DateOnly from, DateOnly to) =>
        _timeOff
            .Where(timeOff => timeOff.DoctorId == doctorId && timeOff.Date >= from && timeOff.Date <= to)
            .ToList();

    private static IEnumerable<DoctorSchedule> Weekly(Guid doctorId, DayOfWeek[] days, int startHour, int endHour, int slotMinutes) =>
        days.Select(day => new DoctorSchedule(doctorId, day, new TimeOnly(startHour, 0), new TimeOnly(endHour, 0), slotMinutes));

    private static IEnumerable<(DateOnly Date, string Name)> HospitalHolidays(int year) =>
    [
        (new DateOnly(year, 1, 1), "New Year's Day"),
        (LastWeekdayOfMonth(year, 5, DayOfWeek.Monday), "Memorial Day"),
        (new DateOnly(year, 7, 4), "Independence Day"),
        (NthWeekdayOfMonth(year, 9, DayOfWeek.Monday, 1), "Labor Day"),
        (NthWeekdayOfMonth(year, 11, DayOfWeek.Thursday, 4), "Thanksgiving Day"),
        (new DateOnly(year, 12, 25), "Christmas Day")
    ];

    private static DateOnly NthWeekdayOfMonth(int year, int month, DayOfWeek day, int n)
    {
        var first = new DateOnly(year, month, 1);
        return first.AddDays(((int)day - (int)first.DayOfWeek + 7) % 7 + 7 * (n - 1));
    }

    private static DateOnly LastWeekdayOfMonth(int year, int month, DayOfWeek day)
    {
        var last = new DateOnly(year, month, DateTime.DaysInMonth(year, month));
        return last.AddDays(-(((int)last.DayOfWeek - (int)day + 7) % 7));
    }
}

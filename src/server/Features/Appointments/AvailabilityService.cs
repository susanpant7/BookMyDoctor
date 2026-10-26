using BookMyDoctor.Persistence;

namespace BookMyDoctor.Features.Appointments;

// Free slots = weekly schedule, minus days off, minus already-booked appointments, minus times too soon to book.
public sealed class AvailabilityService : IAvailabilityService
{
    private readonly IDoctorsRepo _doctorsRepo;
    private readonly IDoctorSchedulesRepo _schedulesRepo;
    private readonly IAppointmentsRepo _appointmentsRepo;
    private readonly TimeProvider _time;

    public AvailabilityService(
        IDoctorsRepo doctorsRepo,
        IDoctorSchedulesRepo schedulesRepo,
        IAppointmentsRepo appointmentsRepo,
        TimeProvider time)
    {
        _doctorsRepo = doctorsRepo;
        _schedulesRepo = schedulesRepo;
        _appointmentsRepo = appointmentsRepo;
        _time = time;
    }

    public IReadOnlyList<TimeSlot> GetAvailableSlots(Guid doctorId, DateOnly? from = null, int days = 7)
    {
        if (days < 1 || days > BookingRules.MaxDaysAhead + 1)
        {
            throw new ArgumentException($"Days must be between 1 and {BookingRules.MaxDaysAhead + 1}.");
        }

        if (_doctorsRepo.GetById(doctorId) is null)
        {
            throw new KeyNotFoundException("Doctor not found.");
        }

        var today = HospitalTime.Today(_time);
        var requestedFirst = from ?? today;
        var first = requestedFirst < today ? today : requestedFirst;
        var last = Min(requestedFirst.AddDays(days - 1), today.AddDays(BookingRules.MaxDaysAhead));
        if (last < first)
        {
            return [];
        }

        var earliestStart = _time.GetUtcNow() + BookingRules.MinimumNotice;
        var schedule = _schedulesRepo.GetWeekly(doctorId);
        var daysOff = _schedulesRepo.GetTimeOff(doctorId, first, last).Select(timeOff => timeOff.Date).ToHashSet();
        var booked = _appointmentsRepo
            .GetForDoctor(doctorId, HospitalTime.At(first, TimeOnly.MinValue), HospitalTime.At(last.AddDays(1), TimeOnly.MinValue))
            .Where(appointment => appointment.Status == AppointmentStatus.Scheduled)
            .ToList();

        var slots = new List<TimeSlot>();
        for (var date = first; date <= last; date = date.AddDays(1))
        {
            if (daysOff.Contains(date))
            {
                continue;
            }

            foreach (var block in schedule.Where(block => block.Day == date.DayOfWeek))
            {
                var length = TimeSpan.FromMinutes(block.SlotMinutes);
                for (var start = block.Start.ToTimeSpan(); start + length <= block.End.ToTimeSpan(); start += length)
                {
                    var startsAt = HospitalTime.At(date, TimeOnly.FromTimeSpan(start));
                    var endsAt = startsAt + length;
                    var isTaken = booked.Any(appointment => appointment.StartsAt < endsAt && startsAt < appointment.EndsAt);
                    if (startsAt >= earliestStart && !isTaken)
                    {
                        slots.Add(new TimeSlot(doctorId, startsAt, endsAt));
                    }
                }
            }
        }

        return slots.OrderBy(slot => slot.StartsAt).ToList();
    }

    private static DateOnly Min(DateOnly left, DateOnly right) => left < right ? left : right;
}

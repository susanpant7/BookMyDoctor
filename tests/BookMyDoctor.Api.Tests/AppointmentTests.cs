using BookMyDoctor.Features.Appointments;
using BookMyDoctor.Persistence;
using Xunit;

namespace BookMyDoctor.Tests;

public sealed class AppointmentTests
{
    // Monday 19 Oct 2026, 06:00 in Texas. Doctor General Practice works 9:00–19:00 in 15-minute slots.
    private static readonly DateOnly Monday = new(2026, 10, 19);
    private static readonly DateTimeOffset EarlyMonday = HospitalTime.At(Monday, new TimeOnly(6, 0));
    private static readonly Guid Doctor = SeedIds.GeneralPracticeDoctorId;
    private static readonly Guid Patient = Guid.NewGuid();

    [Fact]
    public void Slots_FollowWeeklySchedule()
    {
        var slots = Availability(EarlyMonday).GetAvailableSlots(Doctor, Monday, 1);

        Assert.Equal(40, slots.Count);
        Assert.Equal(At(9, 0), slots[0].StartsAt);
        Assert.Equal(At(18, 45), slots[^1].StartsAt);
        Assert.Equal(At(19, 0), slots[^1].EndsAt);
    }

    [Fact]
    public void Slots_SkipBookedButNotCancelledAppointments()
    {
        var appointments = new[]
        {
            Existing(At(9, 0), AppointmentStatus.Scheduled),
            Existing(At(9, 15), AppointmentStatus.Cancelled)
        };

        var slots = Availability(EarlyMonday, appointments).GetAvailableSlots(Doctor, Monday, 1);

        Assert.Equal(39, slots.Count);
        Assert.DoesNotContain(slots, slot => slot.StartsAt == At(9, 0));
        Assert.Contains(slots, slot => slot.StartsAt == At(9, 15));
    }

    [Fact]
    public void Slots_SkipDaysOff()
    {
        var timeOff = new[] { new DoctorTimeOff(Doctor, Monday, "Annual leave") };

        var slots = Availability(EarlyMonday, timeOff: timeOff).GetAvailableSlots(Doctor, Monday, 1);

        Assert.Empty(slots);
    }

    [Fact]
    public void Slots_StartAtLeastTwoHoursFromNow()
    {
        var slots = Availability(HospitalTime.At(Monday, new TimeOnly(10, 30))).GetAvailableSlots(Doctor, Monday, 1);

        Assert.Equal(At(12, 30), slots[0].StartsAt);
        Assert.Equal(26, slots.Count);
    }

    [Fact]
    public void Book_TakesTheSlotSoItIsNoLongerAvailable()
    {
        var (availability, booking) = Services(EarlyMonday);

        var appointment = booking.Book(Patient, new BookAppointmentRequest(Doctor, At(11, 0), "Headache"));

        Assert.Equal(At(11, 15), appointment.EndsAt);
        Assert.DoesNotContain(availability.GetAvailableSlots(Doctor, Monday, 1), slot => slot.StartsAt == At(11, 0));
        Assert.Single(booking.GetForPatient(Patient));
    }

    [Fact]
    public void Book_RejectsSlotAlreadyTakenByAnotherPatient()
    {
        var (_, booking) = Services(EarlyMonday);
        booking.Book(Guid.NewGuid(), new BookAppointmentRequest(Doctor, At(11, 0), "Fever"));

        Assert.Throws<InvalidOperationException>(() =>
            booking.Book(Patient, new BookAppointmentRequest(Doctor, At(11, 0), "Headache")));
    }

    [Fact]
    public void Book_RejectsTimeThatIsNotASlot()
    {
        var (_, booking) = Services(EarlyMonday);

        Assert.Throws<InvalidOperationException>(() =>
            booking.Book(Patient, new BookAppointmentRequest(Doctor, At(11, 7), "Headache")));
        Assert.Throws<InvalidOperationException>(() =>
            booking.Book(Patient, new BookAppointmentRequest(Doctor, At(20, 0), "Headache")));
    }

    [Fact]
    public void Book_RejectsSecondUpcomingAppointmentWithSameDoctor()
    {
        var (_, booking) = Services(EarlyMonday);
        booking.Book(Patient, new BookAppointmentRequest(Doctor, At(11, 0), "Headache"));

        Assert.Throws<InvalidOperationException>(() =>
            booking.Book(Patient, new BookAppointmentRequest(Doctor, At(15, 0), "Follow-up")));
    }

    private static DateTimeOffset At(int hour, int minute) => HospitalTime.At(Monday, new TimeOnly(hour, minute));

    private static Appointment Existing(DateTimeOffset startsAt, AppointmentStatus status) =>
        new(Guid.NewGuid(), Doctor, Guid.NewGuid(), startsAt, startsAt.AddMinutes(15), "Existing", status, startsAt.AddDays(-1));

    private static AvailabilityService Availability(
        DateTimeOffset now, IEnumerable<Appointment>? appointments = null, IReadOnlyList<DoctorTimeOff>? timeOff = null) =>
        new(new DoctorsRepo(),
            new DoctorSchedulesRepo(DoctorSchedulesRepo.SeedSchedules, timeOff ?? []),
            new AppointmentsRepo(appointments ?? []),
            new FixedTimeProvider(now));

    private static (AvailabilityService, AppointmentService) Services(DateTimeOffset now)
    {
        var appointmentsRepo = new AppointmentsRepo();
        var time = new FixedTimeProvider(now);
        var availability = new AvailabilityService(
            new DoctorsRepo(), new DoctorSchedulesRepo(DoctorSchedulesRepo.SeedSchedules, []), appointmentsRepo, time);
        return (availability, new AppointmentService(appointmentsRepo, availability, time));
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now.ToUniversalTime();
    }
}

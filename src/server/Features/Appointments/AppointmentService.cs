using BookMyDoctor.Persistence;

namespace BookMyDoctor.Features.Appointments;

public sealed class AppointmentService : IAppointmentService
{
    private readonly IAppointmentsRepo _appointmentsRepo;
    private readonly IAvailabilityService _availabilityService;
    private readonly TimeProvider _time;

    public AppointmentService(IAppointmentsRepo appointmentsRepo, IAvailabilityService availabilityService, TimeProvider time)
    {
        _appointmentsRepo = appointmentsRepo;
        _availabilityService = availabilityService;
        _time = time;
    }

    public IReadOnlyList<Appointment> GetForPatient(Guid patientId) => _appointmentsRepo.GetForPatient(patientId);

    public Appointment Book(Guid patientId, BookAppointmentRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Reason))
        {
            throw new ArgumentException("A reason for the visit is required.");
        }

        // The requested time must be one of the doctor's currently free slots.
        var slot = _availabilityService
            .GetAvailableSlots(request.DoctorId, HospitalTime.DateOf(request.StartsAt), 1)
            .FirstOrDefault(slot => slot.StartsAt == request.StartsAt)
            ?? throw new InvalidOperationException("That time is not available. Please choose another slot.");

        var now = _time.GetUtcNow();
        var upcoming = _appointmentsRepo
            .GetForPatient(patientId)
            .Where(appointment => appointment.Status == AppointmentStatus.Scheduled && appointment.StartsAt > now)
            .ToList();
        if (upcoming.Count >= BookingRules.MaxUpcomingPerPatient)
        {
            throw new InvalidOperationException(
                $"You can have at most {BookingRules.MaxUpcomingPerPatient} upcoming appointments.");
        }

        if (upcoming.Count(appointment => appointment.DoctorId == request.DoctorId) >= BookingRules.MaxUpcomingPerDoctor)
        {
            throw new InvalidOperationException("You already have an upcoming appointment with this doctor.");
        }

        var appointment = new Appointment(
            Guid.NewGuid(),
            request.DoctorId,
            patientId,
            slot.StartsAt,
            slot.EndsAt,
            request.Reason.Trim(),
            AppointmentStatus.Scheduled,
            now);

        if (!_appointmentsRepo.TryAdd(appointment))
        {
            throw new InvalidOperationException("That time was just booked by someone else. Please choose another slot.");
        }

        return appointment;
    }
}

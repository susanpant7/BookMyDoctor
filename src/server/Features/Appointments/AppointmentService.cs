using MyHospitalAI.Persistence;

namespace MyHospitalAI.Features.Appointments;

public sealed class AppointmentService : IAppointmentService
{
    private readonly IAppointmentsRepo _appointmentsRepo;
    private readonly IAvailabilityService _availabilityService;
    private readonly TimeProvider _time;
    private readonly IUsersRepo _usersRepo;

    public AppointmentService(IAppointmentsRepo appointmentsRepo, IAvailabilityService availabilityService, TimeProvider time, IUsersRepo usersRepo)
    {
        _appointmentsRepo = appointmentsRepo;
        _availabilityService = availabilityService;
        _time = time;
        _usersRepo = usersRepo;
    }

    public IReadOnlyList<Appointment> GetForPatient(Guid patientId) => _appointmentsRepo.GetForPatient(patientId);

    public IReadOnlyList<DoctorAppointment> GetForDoctor(Guid doctorId)
    {
        var appointments = _appointmentsRepo.GetForDoctor(doctorId, DateTimeOffset.MinValue, DateTimeOffset.MaxValue);
        var patients = appointments.Select(appointment => appointment.PatientId).Distinct()
            .ToDictionary(id => id, id => _usersRepo.GetById(id));
        return appointments
            .Select(appointment => new DoctorAppointment(
                appointment.Id, BookingReference.For(appointment.Id), appointment.StartsAt, appointment.EndsAt, appointment.Reason,
                appointment.Status, patients[appointment.PatientId]?.Name ?? "Unknown patient", patients[appointment.PatientId]?.Email ?? ""))
            .ToList();
    }

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

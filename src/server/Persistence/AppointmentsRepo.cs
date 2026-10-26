using BookMyDoctor.Features.Appointments;

namespace BookMyDoctor.Persistence;

// Starts empty. Bookings are kept in memory for as long as the server runs (the repo is a singleton);
// a real database will keep them across restarts.
public sealed class AppointmentsRepo : IAppointmentsRepo
{
    private readonly List<Appointment> _appointments;
    private readonly Lock _lock = new();

    public AppointmentsRepo() : this([])
    {
    }

    internal AppointmentsRepo(IEnumerable<Appointment> seed)
    {
        _appointments = [.. seed];
    }

    public IReadOnlyList<Appointment> GetForDoctor(Guid doctorId, DateTimeOffset from, DateTimeOffset to)
    {
        lock (_lock)
        {
            return _appointments
                .Where(appointment => appointment.DoctorId == doctorId && appointment.StartsAt < to && appointment.EndsAt > from)
                .OrderBy(appointment => appointment.StartsAt)
                .ToList();
        }
    }

    public IReadOnlyList<Appointment> GetForPatient(Guid patientId)
    {
        lock (_lock)
        {
            return _appointments
                .Where(appointment => appointment.PatientId == patientId)
                .OrderBy(appointment => appointment.StartsAt)
                .ToList();
        }
    }

    public bool TryAdd(Appointment appointment)
    {
        lock (_lock)
        {
            var taken = _appointments.Any(existing =>
                existing.DoctorId == appointment.DoctorId &&
                existing.Status == AppointmentStatus.Scheduled &&
                existing.StartsAt < appointment.EndsAt &&
                appointment.StartsAt < existing.EndsAt);
            if (taken)
            {
                return false;
            }

            _appointments.Add(appointment);
            return true;
        }
    }
}

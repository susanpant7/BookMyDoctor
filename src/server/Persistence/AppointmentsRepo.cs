using BookMyDoctor.Features.Appointments;
using BookMyDoctor.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace BookMyDoctor.Persistence;

public sealed class AppointmentsRepo : IAppointmentsRepo
{
    private readonly IDbContextFactory<AppDbContext> _factory;

    // The API runs as one process, so this lock is what keeps two patients from taking the same slot.
    private readonly Lock _bookingLock = new();

    public AppointmentsRepo(IDbContextFactory<AppDbContext> factory)
    {
        _factory = factory;
    }

    public IReadOnlyList<Appointment> GetForDoctor(Guid doctorId, DateTimeOffset from, DateTimeOffset to)
    {
        using var db = _factory.CreateDbContext();
        return db.Appointments
            .AsNoTracking()
            .Where(appointment => appointment.DoctorId == doctorId && appointment.StartsAt < to && appointment.EndsAt > from)
            .OrderBy(appointment => appointment.StartsAt)
            .ToList()
            .Select(ToAppointment)
            .ToList();
    }

    public IReadOnlyList<Appointment> GetForPatient(Guid patientId)
    {
        using var db = _factory.CreateDbContext();
        return db.Appointments
            .AsNoTracking()
            .Where(appointment => appointment.PatientId == patientId)
            .OrderBy(appointment => appointment.StartsAt)
            .ToList()
            .Select(ToAppointment)
            .ToList();
    }

    public bool TryAdd(Appointment appointment)
    {
        lock (_bookingLock)
        {
            using var db = _factory.CreateDbContext();
            var taken = db.Appointments.Any(existing =>
                existing.DoctorId == appointment.DoctorId &&
                existing.Status == AppointmentStatus.Scheduled &&
                existing.StartsAt < appointment.EndsAt &&
                appointment.StartsAt < existing.EndsAt);
            if (taken)
            {
                return false;
            }

            db.Appointments.Add(new AppointmentEntity
            {
                Id = appointment.Id,
                DoctorId = appointment.DoctorId,
                PatientId = appointment.PatientId,
                StartsAt = appointment.StartsAt,
                EndsAt = appointment.EndsAt,
                Reason = appointment.Reason,
                Status = appointment.Status,
                BookedAt = appointment.BookedAt
            });
            db.SaveChanges();
            return true;
        }
    }

    private static Appointment ToAppointment(AppointmentEntity entity) =>
        new(entity.Id, entity.DoctorId, entity.PatientId, entity.StartsAt, entity.EndsAt, entity.Reason, entity.Status, entity.BookedAt);
}

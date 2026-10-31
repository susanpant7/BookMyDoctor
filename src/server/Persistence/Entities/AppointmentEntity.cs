using BookMyDoctor.Features.Appointments;

namespace BookMyDoctor.Persistence.Entities;

public class AppointmentEntity
{
    public Guid Id { get; set; }
    public Guid DoctorId { get; set; }
    public Guid PatientId { get; set; }
    public DateTimeOffset StartsAt { get; set; }
    public DateTimeOffset EndsAt { get; set; }
    public required string Reason { get; set; }
    public AppointmentStatus Status { get; set; }
    public DateTimeOffset BookedAt { get; set; }
}

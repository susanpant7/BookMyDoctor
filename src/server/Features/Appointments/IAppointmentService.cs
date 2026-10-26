namespace BookMyDoctor.Features.Appointments;

public interface IAppointmentService
{
    IReadOnlyList<Appointment> GetForPatient(Guid patientId);
    Appointment Book(Guid patientId, BookAppointmentRequest request);
}

namespace MyHospitalAI.Features.Appointments;

public interface IAppointmentService
{
    IReadOnlyList<Appointment> GetForPatient(Guid patientId);

    // All of the doctor's appointments, earliest first.
    IReadOnlyList<DoctorAppointment> GetForDoctor(Guid doctorId);
    Appointment Book(Guid patientId, BookAppointmentRequest request);
}

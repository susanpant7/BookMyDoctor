using BookMyDoctor.Features.Appointments;

namespace BookMyDoctor.Persistence;

public interface IAppointmentsRepo
{
    // All appointments (any status) for the doctor that overlap [from, to).
    IReadOnlyList<Appointment> GetForDoctor(Guid doctorId, DateTimeOffset from, DateTimeOffset to);
    IReadOnlyList<Appointment> GetForPatient(Guid patientId);

    // Adds the appointment unless it overlaps another scheduled appointment for the same doctor.
    // Checking and inserting happen together so two patients can't book the same slot at once.
    bool TryAdd(Appointment appointment);
}

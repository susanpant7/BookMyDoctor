namespace BookMyDoctor.Features.Appointments;

public interface IAvailabilityService
{
    // Free slots for `days` days starting at `from` (hospital-local date; defaults to today).
    IReadOnlyList<TimeSlot> GetAvailableSlots(Guid doctorId, DateOnly? from = null, int days = 7);
}

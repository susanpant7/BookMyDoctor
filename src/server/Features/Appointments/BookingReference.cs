namespace BookMyDoctor.Features.Appointments;

public static class BookingReference
{
    // Short, readable reference shown to patients, e.g. "BMD-61D3D31B" (Knowledge/visit-preparation.md mentions it).
    public static string For(Guid appointmentId) => $"BMD-{appointmentId.ToString("N")[..8].ToUpperInvariant()}";
}

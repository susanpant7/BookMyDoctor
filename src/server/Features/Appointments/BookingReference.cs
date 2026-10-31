namespace MyHospitalAI.Features.Appointments;

public static class BookingReference
{
    // Short, readable reference shown to patients, e.g. "MHA-61D3D31B" (Knowledge/visit-preparation.md mentions it).
    public static string For(Guid appointmentId) => $"MHA-{appointmentId.ToString("N")[..8].ToUpperInvariant()}";
}

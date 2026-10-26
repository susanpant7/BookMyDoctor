namespace BookMyDoctor.Features.Appointments;

// Mirrors the booking policy in Knowledge/booking.md.
public static class BookingRules
{
    public static readonly TimeSpan MinimumNotice = TimeSpan.FromHours(2);
    public const int MaxDaysAhead = 30;
    public const int MaxUpcomingPerPatient = 3;
    public const int MaxUpcomingPerDoctor = 1;
}

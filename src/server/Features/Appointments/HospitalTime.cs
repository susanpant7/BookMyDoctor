namespace BookMyDoctor.Features.Appointments;

public static class HospitalTime
{
    // The hospital is in Texas, so schedules are stored in US Central Time.
    public static readonly TimeZoneInfo Zone = TimeZoneInfo.FindSystemTimeZoneById("America/Chicago");

    public static DateTimeOffset At(DateOnly date, TimeOnly time)
    {
        var local = date.ToDateTime(time);
        return new DateTimeOffset(local, Zone.GetUtcOffset(local));
    }

    public static DateOnly DateOf(DateTimeOffset instant) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(instant, Zone).DateTime);

    public static DateOnly Today(TimeProvider time) => DateOf(time.GetUtcNow());
}

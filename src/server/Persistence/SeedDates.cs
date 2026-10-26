using BookMyDoctor.Features.Appointments;

namespace BookMyDoctor.Persistence;

// Seed rows like doctor leave are placed relative to today so the mock data stays current.
public static class SeedDates
{
    public static readonly DateOnly Today = HospitalTime.Today(TimeProvider.System);

    public static DayOfWeek[] Weekdays =>
        [DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday];

    // The given weekday in the week `weeks` away from this one (0 = this week, 1 = next week, -1 = last week).
    public static DateOnly InWeeks(int weeks, DayOfWeek day)
    {
        var monday = Today.AddDays(-(((int)Today.DayOfWeek + 6) % 7));
        return monday.AddDays(7 * weeks + ((int)day + 6) % 7);
    }
}

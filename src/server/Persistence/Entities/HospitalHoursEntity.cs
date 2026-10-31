namespace BookMyDoctor.Persistence.Entities;

/// <summary>Outpatient opening hours for one weekday; days without a row are closed.</summary>
public class HospitalHoursEntity
{
    public DayOfWeek Day { get; set; }
    public TimeOnly Opens { get; set; }
    public TimeOnly Closes { get; set; }
}

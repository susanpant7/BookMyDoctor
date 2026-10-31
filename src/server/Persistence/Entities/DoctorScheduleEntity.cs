namespace BookMyDoctor.Persistence.Entities;

/// <summary>A recurring weekly block when a doctor sees patients, in hospital-local time.</summary>
public class DoctorScheduleEntity
{
    public int Id { get; set; }
    public Guid DoctorId { get; set; }
    public DayOfWeek Day { get; set; }
    public TimeOnly Start { get; set; }
    public TimeOnly End { get; set; }
    public int SlotMinutes { get; set; }
}

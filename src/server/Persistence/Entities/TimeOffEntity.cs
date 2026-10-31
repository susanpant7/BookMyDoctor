namespace BookMyDoctor.Persistence.Entities;

/// <summary>A whole day nobody can be booked. With no doctor, it applies to the whole hospital (a public holiday).</summary>
public class TimeOffEntity
{
    public int Id { get; set; }
    public Guid? DoctorId { get; set; }
    public DateOnly Date { get; set; }
    public required string Reason { get; set; }
}

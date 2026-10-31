namespace BookMyDoctor.Persistence.Entities;

/// <summary>The single row (Id 1) with the hospital's contact details.</summary>
public class HospitalInfoEntity
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public string Address { get; set; } = "";
    public string Phone { get; set; } = "";

    /// <summary>One facility per line.</summary>
    public string Facilities { get; set; } = "";

    public string Parking { get; set; } = "";
    public string PublicTransport { get; set; } = "";
}

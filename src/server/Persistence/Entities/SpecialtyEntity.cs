namespace BookMyDoctor.Persistence.Entities;

public class SpecialtyEntity
{
    public Guid Id { get; set; }
    public required string Name { get; set; }
    public string Description { get; set; } = "";

    public ICollection<DoctorEntity> Doctors { get; set; } = [];
}

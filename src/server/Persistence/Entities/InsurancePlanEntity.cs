namespace BookMyDoctor.Persistence.Entities;

public class InsurancePlanEntity
{
    public Guid Id { get; set; }
    public required string Name { get; set; }
    public string? Restrictions { get; set; }
    public int SortOrder { get; set; }
}

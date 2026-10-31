namespace BookMyDoctor.Persistence.Entities;

public class ServicePriceEntity
{
    public Guid Id { get; set; }
    public required string Name { get; set; }
    public decimal Price { get; set; }
    public int SortOrder { get; set; }
}

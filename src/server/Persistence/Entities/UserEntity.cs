namespace BookMyDoctor.Persistence.Entities;

/// <summary>A patient. The admin is not a row here; it comes from configuration.</summary>
public class UserEntity
{
    public Guid Id { get; set; }
    public required string Name { get; set; }
    public required string Email { get; set; }
    public required string PasswordHash { get; set; }
}

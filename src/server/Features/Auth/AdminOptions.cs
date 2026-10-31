using System.ComponentModel.DataAnnotations;

namespace BookMyDoctor.Features.Auth;

// The admin is not a row in the database: the login comes from configuration.
public sealed class AdminOptions
{
    public const string SectionName = "Admin";

    [Required]
    public string Username { get; init; } = "";

    [Required]
    public string Password { get; init; } = "";
}

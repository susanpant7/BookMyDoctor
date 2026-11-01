using System.ComponentModel.DataAnnotations;

namespace MyHospitalAI.Features.Auth;

// The admin is not a row in the database: the login comes from configuration.
public sealed class AdminOptions
{
    public const string SectionName = "Admin";

    [Required(ErrorMessage = "Admin__Username must be set, in the .env file or as an environment variable. See .env.example.")]
    public string Username { get; init; } = "";

    [Required(ErrorMessage = "Admin__Password must be set, in the .env file or as an environment variable. See .env.example.")]
    public string Password { get; init; } = "";
}

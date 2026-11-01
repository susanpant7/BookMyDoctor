namespace MyHospitalAI.Persistence.Entities;

/// <summary>A chat model the admin saved on the Settings page to switch to later: where it runs and its name there.</summary>
public class ChatModelEntity
{
    public Guid Id { get; set; }

    /// <summary>What the admin calls it, for example "Gemini Flash (free)". Unique.</summary>
    public required string Name { get; set; }

    /// <summary>The value for the "Chat API address" setting.</summary>
    public required string BaseUrl { get; set; }

    /// <summary>The value for the "Chat model" setting.</summary>
    public required string Model { get; set; }

    public string Notes { get; set; } = "";
}

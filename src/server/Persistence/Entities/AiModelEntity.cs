namespace MyHospitalAI.Persistence.Entities;

/// <summary>
/// A chat or embedding model on the Settings page, to switch to with "Use": where it runs and its name there.
/// The services are listed from AiProviders; these are the models the admin saved.
/// </summary>
public class AiModelEntity
{
    public Guid Id { get; set; }

    /// <summary>"chat" or "embedding".</summary>
    public required string Kind { get; set; }

    /// <summary>What the admin calls it, for example "Google Gemini Flash". Unique for its kind.</summary>
    public required string Name { get; set; }

    /// <summary>The value for the "Chat API address" (or "Embedding API address") setting.</summary>
    public required string BaseUrl { get; set; }

    /// <summary>The value for the "Chat model" (or "Embedding model") setting.</summary>
    public required string Model { get; set; }

    /// <summary>The name of the .env variable with the service's key, e.g. GEMINI_API_KEY. Never the key itself.</summary>
    public string ApiKeyName { get; set; } = "";

    /// <summary>Embedding models only: the text put before questions and before knowledge sections.</summary>
    public string QueryPrefix { get; set; } = "";

    public string DocumentPrefix { get; set; } = "";

    public string Notes { get; set; } = "";

    /// <summary>Keeps the order the models were added in.</summary>
    public int SortOrder { get; set; }
}

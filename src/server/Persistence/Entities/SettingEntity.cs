namespace BookMyDoctor.Persistence.Entities;

/// <summary>One application setting. The value is always text.</summary>
public class SettingEntity
{
    /// <summary>Unique name, grouped by prefix, for example "retrieval.top_k".</summary>
    public required string Key { get; set; }

    public required string Label { get; set; }

    /// <summary>Plain-language help: what the setting does and what to watch for when changing it.</summary>
    public required string Description { get; set; }

    public required string Value { get; set; }
}

namespace MyHospitalAI.Features.Settings;

// Options lists the allowed values of a "choice" setting.
public sealed record Setting(
    string Key, string Label, string Description, string Value, string DefaultValue, string Kind, IReadOnlyList<string>? Options = null);

public sealed record UpdateSettingRequest(string Value);

// An AI provider as the Settings page lists it. HasApiKey says whether its key is in the .env file; the key itself
// is never sent.
public sealed record AiProviderInfo(
    string Id, string Name, string? ApiKeyName, bool HasApiKey, string DefaultModel, string FreeTier, string SignUpUrl);

namespace BookMyDoctor.Features.Settings;

public sealed record Setting(string Key, string Label, string Description, string Value, string DefaultValue, string Kind);

public sealed record UpdateSettingRequest(string Value);

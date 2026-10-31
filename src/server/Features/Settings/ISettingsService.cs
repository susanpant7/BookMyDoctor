namespace BookMyDoctor.Features.Settings;

public interface ISettingsService : IAiSettingsProvider
{
    IReadOnlyList<Setting> GetAll();

    // Throws KeyNotFoundException for an unknown key and ArgumentException for a value that is not allowed.
    Setting Update(string key, string value);

    // Sets every setting back to its default value and returns them all.
    IReadOnlyList<Setting> RestoreDefaults();
}

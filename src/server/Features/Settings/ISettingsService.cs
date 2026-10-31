namespace BookMyDoctor.Features.Settings;

public interface ISettingsService : IAiSettingsProvider
{
    IReadOnlyList<Setting> GetAll();

    // Throws KeyNotFoundException for an unknown key and ArgumentException for a value that is not allowed.
    Setting Update(string key, string value);

    // Saves every setting with its default value (adding any that are missing) and returns them all.
    IReadOnlyList<Setting> RestoreDefaults();
}

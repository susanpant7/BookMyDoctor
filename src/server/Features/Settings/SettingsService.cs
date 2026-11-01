using System.Globalization;
using MyHospitalAI.Persistence;
using MyHospitalAI.Persistence.Entities;

namespace MyHospitalAI.Features.Settings;

public sealed class SettingsService : ISettingsService
{
    private readonly ISettingsRepo _settingsRepo;

    public SettingsService(ISettingsRepo settingsRepo)
    {
        _settingsRepo = settingsRepo;
    }

    public AiSettings Get() => AiSettings.From(_settingsRepo.GetAll().ToDictionary(setting => setting.Key, setting => setting.Value));

    // Empty until the defaults are loaded. A setting added in a newer version of the app shows its default value
    // until it is saved.
    public IReadOnlyList<Setting> GetAll()
    {
        var rows = _settingsRepo.GetAll().ToDictionary(row => row.Key, row => row.Value);
        return rows.Count == 0
            ? []
            : SettingDefaults.All.Select(definition => ToSetting(definition, rows.GetValueOrDefault(definition.Key, definition.Value))).ToList();
    }

    public Setting Update(string key, string value)
    {
        var definition = SettingDefaults.Find(key) ?? throw new KeyNotFoundException("Setting not found.");
        value = value.Trim();
        Validate(definition, value);

        if (!_settingsRepo.SetValue(key, value))
        {
            _settingsRepo.Add(ToEntity(definition, value));
        }
        return GetAll().First(setting => setting.Key == key);
    }

    public IReadOnlyList<Setting> RestoreDefaults()
    {
        _settingsRepo.ReplaceAll(SettingDefaults.All.Select(definition => ToEntity(definition, definition.Value)).ToList());
        return GetAll();
    }

    private static void Validate(SettingDefinition definition, string value)
    {
        if (definition.Key.EndsWith("url") && !(Uri.TryCreate(value, UriKind.Absolute, out var url) && url.Scheme is "http" or "https"))
        {
            throw new ArgumentException($"{definition.Label} must be a web address that starts with http:// or https://.");
        }


        switch (definition.Kind)
        {
            case SettingKind.Text when definition.Key.EndsWith("_prefix"):
                break;
            case SettingKind.Text:
                if (value.Length == 0 || value.Length > 200)
                {
                    throw new ArgumentException($"{definition.Label} is required and can be up to 200 characters.");
                }
                break;
            case SettingKind.LongText:
                if (value.Length == 0 || value.Length > 8000)
                {
                    throw new ArgumentException($"{definition.Label} is required and can be up to 8000 characters.");
                }
                break;
            case SettingKind.Boolean:
                if (value is not ("true" or "false"))
                {
                    throw new ArgumentException($"{definition.Label} must be true or false.");
                }
                break;
            case SettingKind.Integer:
                if (!int.TryParse(value, CultureInfo.InvariantCulture, out var whole) || whole < definition.Min || whole > definition.Max)
                {
                    throw new ArgumentException($"{definition.Label} must be a whole number from {definition.Min} to {definition.Max}.");
                }
                break;
            default:
                if (!double.TryParse(value, CultureInfo.InvariantCulture, out var number) || number < definition.Min || number > definition.Max)
                {
                    throw new ArgumentException($"{definition.Label} must be a number from {definition.Min} to {definition.Max}.");
                }
                break;
        }
    }

    private static Setting ToSetting(SettingDefinition definition, string value) =>
        new(definition.Key, definition.Label, definition.Description, value, definition.Value, definition.Kind.ToString().ToLowerInvariant());

    private static SettingEntity ToEntity(SettingDefinition definition, string value) =>
        new() { Key = definition.Key, Label = definition.Label, Description = definition.Description, Value = value };
}

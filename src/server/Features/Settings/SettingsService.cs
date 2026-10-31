using System.Globalization;
using BookMyDoctor.Persistence;
using BookMyDoctor.Persistence.Entities;

namespace BookMyDoctor.Features.Settings;

public sealed class SettingsService : ISettingsService
{
    private readonly ISettingsRepo _settingsRepo;

    public SettingsService(ISettingsRepo settingsRepo)
    {
        _settingsRepo = settingsRepo;
    }

    public AiSettings Get() => AiSettings.From(_settingsRepo.GetAll().ToDictionary(setting => setting.Key, setting => setting.Value));

    public IReadOnlyList<Setting> GetAll() =>
        SettingDefaults.All
            .Join(_settingsRepo.GetAll(), definition => definition.Key, row => row.Key, ToSetting)
            .ToList();

    public Setting Update(string key, string value)
    {
        var definition = SettingDefaults.Find(key) ?? throw new KeyNotFoundException("Setting not found.");
        value = value.Trim();
        Validate(definition, value);

        if (!_settingsRepo.SetValue(key, value))
        {
            throw new KeyNotFoundException("Setting not found.");
        }
        return GetAll().First(setting => setting.Key == key);
    }

    public IReadOnlyList<Setting> RestoreDefaults()
    {
        _settingsRepo.ReplaceAll(SettingDefaults.All
            .Select(definition => new SettingEntity
            {
                Key = definition.Key, Label = definition.Label, Description = definition.Description, Value = definition.Value
            })
            .ToList());
        return GetAll();
    }

    private static void Validate(SettingDefinition definition, string value)
    {
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

    private static Setting ToSetting(SettingDefinition definition, SettingEntity row) =>
        new(definition.Key, definition.Label, definition.Description, row.Value, definition.Value, definition.Kind.ToString().ToLowerInvariant());
}

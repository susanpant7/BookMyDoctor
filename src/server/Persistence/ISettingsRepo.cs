using BookMyDoctor.Persistence.Entities;

namespace BookMyDoctor.Persistence;

public interface ISettingsRepo
{
    IReadOnlyList<SettingEntity> GetAll();

    // False when no setting has the key.
    bool SetValue(string key, string value);

    // Deletes every setting and saves these instead.
    void ReplaceAll(IReadOnlyList<SettingEntity> settings);
}

using BookMyDoctor.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace BookMyDoctor.Persistence;

public sealed class SettingsRepo : ISettingsRepo
{
    private readonly IDbContextFactory<AppDbContext> _factory;

    public SettingsRepo(IDbContextFactory<AppDbContext> factory)
    {
        _factory = factory;
    }

    public IReadOnlyList<SettingEntity> GetAll()
    {
        using var db = _factory.CreateDbContext();
        return db.Settings.AsNoTracking().OrderBy(setting => setting.Key).ToList();
    }

    public bool SetValue(string key, string value)
    {
        using var db = _factory.CreateDbContext();
        return db.Settings.Where(setting => setting.Key == key).ExecuteUpdate(setters => setters.SetProperty(setting => setting.Value, value)) > 0;
    }

    public void ReplaceAll(IReadOnlyList<SettingEntity> settings)
    {
        using var db = _factory.CreateDbContext();
        using var transaction = db.Database.BeginTransaction();
        db.Settings.ExecuteDelete();
        db.Settings.AddRange(settings);
        db.SaveChanges();
        transaction.Commit();
    }
}

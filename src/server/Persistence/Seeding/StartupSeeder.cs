using BookMyDoctor.Features.Settings;
using BookMyDoctor.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace BookMyDoctor.Persistence.Seeding;

// Fills in what the app needs to run, on every start. Rows that exist are never changed, so edits are kept.
public static class StartupSeeder
{
    public static async Task SeedAsync(IDbContextFactory<AppDbContext> factory, IHospitalRepo hospitalRepo, TimeProvider time)
    {
        await using var db = await factory.CreateDbContextAsync();
        await SeedSettingsAsync(db);
        await SeedHolidaysAsync(db, time);

        // The hospital details start as the defaults and are edited on the Settings page afterwards.
        if (!await db.HospitalInfo.AnyAsync())
        {
            hospitalRepo.Save(DefaultData.Hospital);
        }
    }

    private static async Task SeedSettingsAsync(AppDbContext db)
    {
        var existing = (await db.Settings.Select(setting => setting.Key).ToListAsync()).ToHashSet();
        db.Settings.AddRange(SettingDefaults.All
            .Where(definition => !existing.Contains(definition.Key))
            .Select(definition => new SettingEntity
            {
                Key = definition.Key, Label = definition.Label, Description = definition.Description, Value = definition.Value
            }));
        await db.SaveChangesAsync();
    }

    // Public holidays for this year and next, so a doctor created later is also off on them.
    private static async Task SeedHolidaysAsync(AppDbContext db, TimeProvider time)
    {
        var year = time.GetUtcNow().Year;
        foreach (var (date, name) in DefaultData.Holidays(year).Concat(DefaultData.Holidays(year + 1)))
        {
            if (!await db.TimeOff.AnyAsync(timeOff => timeOff.DoctorId == null && timeOff.Date == date))
            {
                db.TimeOff.Add(new TimeOffEntity { Date = date, Reason = name });
            }
        }
        await db.SaveChangesAsync();
    }
}

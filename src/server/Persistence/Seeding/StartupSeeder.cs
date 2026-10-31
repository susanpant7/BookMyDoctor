using System.Security.Cryptography;
using System.Text;
using BookMyDoctor.Features.Settings;
using BookMyDoctor.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace BookMyDoctor.Persistence.Seeding;

// Fills in what the app needs to run, on every start. Edited rows are never changed; the only exception is a setting
// still holding a default that a later version replaced (see SettingDefaults.ReplacedDefaultHashes).
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

        // A value still equal to an old default was never edited, so it moves to the current default.
        var replaceable = SettingDefaults.ReplacedDefaultHashes.Keys.ToList();
        foreach (var row in await db.Settings.Where(setting => replaceable.Contains(setting.Key)).ToListAsync())
        {
            var hash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(row.Value)));
            if (SettingDefaults.ReplacedDefaultHashes[row.Key].Contains(hash))
            {
                row.Value = SettingDefaults.Find(row.Key)!.Value;
            }
        }
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

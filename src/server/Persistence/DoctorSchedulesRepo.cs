using BookMyDoctor.Features.Appointments;
using BookMyDoctor.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace BookMyDoctor.Persistence;

public sealed class DoctorSchedulesRepo : IDoctorSchedulesRepo
{
    private readonly IDbContextFactory<AppDbContext> _factory;

    public DoctorSchedulesRepo(IDbContextFactory<AppDbContext> factory)
    {
        _factory = factory;
    }

    public IReadOnlyList<DoctorSchedule> GetWeekly(Guid doctorId)
    {
        using var db = _factory.CreateDbContext();
        return db.DoctorSchedules
            .AsNoTracking()
            .Where(block => block.DoctorId == doctorId)
            .ToList()
            .OrderBy(block => ((int)block.Day + 6) % 7)   // Monday first
            .ThenBy(block => block.Start)
            .Select(block => new DoctorSchedule(block.DoctorId, block.Day, block.Start, block.End, block.SlotMinutes))
            .ToList();
    }

    public void ReplaceWeekly(Guid doctorId, IReadOnlyList<DoctorSchedule> blocks)
    {
        using var db = _factory.CreateDbContext();
        using var transaction = db.Database.BeginTransaction();
        db.DoctorSchedules.Where(block => block.DoctorId == doctorId).ExecuteDelete();
        db.DoctorSchedules.AddRange(blocks.Select(block => new DoctorScheduleEntity
        {
            DoctorId = doctorId,
            Day = block.Day,
            Start = block.Start,
            End = block.End,
            SlotMinutes = block.SlotMinutes
        }));
        db.SaveChanges();
        transaction.Commit();
    }

    public IReadOnlyList<DoctorTimeOff> GetTimeOff(Guid doctorId, DateOnly from, DateOnly to)
    {
        using var db = _factory.CreateDbContext();
        return db.TimeOff
            .AsNoTracking()
            .Where(timeOff => (timeOff.DoctorId == doctorId || timeOff.DoctorId == null) && timeOff.Date >= from && timeOff.Date <= to)
            .ToList()
            .Select(timeOff => new DoctorTimeOff(timeOff.DoctorId, timeOff.Date, timeOff.Reason))
            .ToList();
    }

    public void AddTimeOff(DoctorTimeOff timeOff)
    {
        using var db = _factory.CreateDbContext();
        db.TimeOff.Add(new TimeOffEntity { DoctorId = timeOff.DoctorId, Date = timeOff.Date, Reason = timeOff.Reason });
        db.SaveChanges();
    }
}

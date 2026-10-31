using BookMyDoctor.Features.Hospital;
using BookMyDoctor.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace BookMyDoctor.Persistence;

public sealed class HospitalRepo : IHospitalRepo
{
    private const int InfoRowId = 1;

    private readonly IDbContextFactory<AppDbContext> _factory;

    public HospitalRepo(IDbContextFactory<AppDbContext> factory)
    {
        _factory = factory;
    }

    public HospitalInfo GetInfo()
    {
        using var db = _factory.CreateDbContext();
        var entity = db.HospitalInfo.AsNoTracking().FirstOrDefault(info => info.Id == InfoRowId);
        return entity is null
            ? new HospitalInfo("BookMyDoctor Hospital", "", "", [], "", "")
            : new HospitalInfo(entity.Name, entity.Address, entity.Phone,
                entity.Facilities.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
                entity.Parking, entity.PublicTransport);
    }

    public IReadOnlyList<HospitalHours> GetHours()
    {
        using var db = _factory.CreateDbContext();
        return db.HospitalHours
            .AsNoTracking()
            .ToList()
            .OrderBy(hours => ((int)hours.Day + 6) % 7)   // Monday first
            .Select(hours => new HospitalHours(hours.Day, hours.Opens, hours.Closes))
            .ToList();
    }

    public IReadOnlyList<ServicePrice> GetServicePrices()
    {
        using var db = _factory.CreateDbContext();
        return db.ServicePrices
            .AsNoTracking()
            .OrderBy(price => price.SortOrder)
            .ToList()
            .Select(price => new ServicePrice(price.Id, price.Name, price.Price))
            .ToList();
    }

    public void Save(HospitalDetails details)
    {
        using var db = _factory.CreateDbContext();
        using var transaction = db.Database.BeginTransaction();

        var info = db.HospitalInfo.Find(InfoRowId);
        if (info is null)
        {
            info = new HospitalInfoEntity { Id = InfoRowId, Name = details.Info.Name };
            db.HospitalInfo.Add(info);
        }
        info.Name = details.Info.Name;
        info.Address = details.Info.Address;
        info.Phone = details.Info.Phone;
        info.Facilities = string.Join('\n', details.Info.Facilities);
        info.Parking = details.Info.Parking;
        info.PublicTransport = details.Info.PublicTransport;

        db.HospitalHours.ExecuteDelete();
        db.HospitalHours.AddRange(details.Hours.Select(hours =>
            new HospitalHoursEntity { Day = hours.Day, Opens = hours.Opens, Closes = hours.Closes }));

        db.ServicePrices.ExecuteDelete();
        db.ServicePrices.AddRange(details.ServicePrices.Select((price, i) => new ServicePriceEntity
        {
            Id = price.Id == Guid.Empty ? Guid.NewGuid() : price.Id,
            Name = price.Name,
            Price = price.Price,
            SortOrder = i
        }));

        db.InsurancePlans.ExecuteDelete();
        db.InsurancePlans.AddRange(details.InsurancePlans.Select((plan, i) => new InsurancePlanEntity
        {
            Id = plan.Id == Guid.Empty ? Guid.NewGuid() : plan.Id,
            Name = plan.Name,
            Restrictions = plan.Restrictions,
            SortOrder = i
        }));

        db.SaveChanges();
        transaction.Commit();
    }
}

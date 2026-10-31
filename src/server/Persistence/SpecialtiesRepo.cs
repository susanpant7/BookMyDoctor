using BookMyDoctor.Features.Specialties;
using BookMyDoctor.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace BookMyDoctor.Persistence;

public sealed class SpecialtiesRepo : ISpecialtiesRepo
{
    private readonly IDbContextFactory<AppDbContext> _factory;

    public SpecialtiesRepo(IDbContextFactory<AppDbContext> factory)
    {
        _factory = factory;
    }

    public IReadOnlyList<Specialty> GetAll()
    {
        using var db = _factory.CreateDbContext();
        return db.Specialties
            .AsNoTracking()
            .OrderBy(specialty => specialty.Name)
            .Select(specialty => new Specialty(specialty.Id, specialty.Name, specialty.Description, specialty.Doctors.Count))
            .ToList();
    }

    public Specialty? GetById(Guid id)
    {
        using var db = _factory.CreateDbContext();
        return db.Specialties
            .AsNoTracking()
            .Where(specialty => specialty.Id == id)
            .Select(specialty => new Specialty(specialty.Id, specialty.Name, specialty.Description, specialty.Doctors.Count))
            .FirstOrDefault();
    }

    public Specialty? GetByName(string name)
    {
        using var db = _factory.CreateDbContext();
        return db.Specialties
            .AsNoTracking()
            .Where(specialty => specialty.Name == name)
            .Select(specialty => new Specialty(specialty.Id, specialty.Name, specialty.Description, specialty.Doctors.Count))
            .FirstOrDefault();
    }

    public void Add(Specialty specialty)
    {
        using var db = _factory.CreateDbContext();
        db.Specialties.Add(new SpecialtyEntity { Id = specialty.Id, Name = specialty.Name, Description = specialty.Description });
        db.SaveChanges();
    }

    public bool Update(Specialty specialty)
    {
        using var db = _factory.CreateDbContext();
        var entity = db.Specialties.Find(specialty.Id);
        if (entity is null)
        {
            return false;
        }

        entity.Name = specialty.Name;
        entity.Description = specialty.Description;
        db.SaveChanges();
        return true;
    }

    public bool Delete(Guid id)
    {
        using var db = _factory.CreateDbContext();
        return db.Specialties.Where(specialty => specialty.Id == id).ExecuteDelete() > 0;
    }
}

using BookMyDoctor.Features.Doctors;
using BookMyDoctor.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace BookMyDoctor.Persistence;

public sealed class DoctorsRepo : IDoctorsRepo
{
    private readonly IDbContextFactory<AppDbContext> _factory;

    public DoctorsRepo(IDbContextFactory<AppDbContext> factory)
    {
        _factory = factory;
    }

    public IReadOnlyList<Doctor> GetAll()
    {
        using var db = _factory.CreateDbContext();
        return Query(db).OrderBy(doctor => doctor.Name).ToList().Select(ToDoctor).ToList();
    }

    public IReadOnlyList<Doctor> GetBySpecialty(Guid specialtyId)
    {
        using var db = _factory.CreateDbContext();
        return Query(db).Where(doctor => doctor.SpecialtyId == specialtyId).OrderBy(doctor => doctor.Name).ToList().Select(ToDoctor).ToList();
    }

    public Doctor? GetById(Guid id)
    {
        using var db = _factory.CreateDbContext();
        return Query(db).Where(doctor => doctor.Id == id).ToList().Select(ToDoctor).FirstOrDefault();
    }

    public Doctor? GetByName(string name)
    {
        using var db = _factory.CreateDbContext();
        return Query(db).Where(doctor => doctor.Name.ToLower() == name.ToLower()).ToList().Select(ToDoctor).FirstOrDefault();
    }

    public void Add(Doctor doctor)
    {
        using var db = _factory.CreateDbContext();
        var entity = new DoctorEntity { Id = doctor.Id, Name = doctor.Name };
        Apply(entity, doctor);
        db.Doctors.Add(entity);
        db.SaveChanges();
    }

    public bool Update(Doctor doctor)
    {
        using var db = _factory.CreateDbContext();
        var entity = db.Doctors.Find(doctor.Id);
        if (entity is null)
        {
            return false;
        }

        Apply(entity, doctor);
        db.SaveChanges();
        return true;
    }

    public bool Delete(Guid id)
    {
        using var db = _factory.CreateDbContext();
        return db.Doctors.Where(doctor => doctor.Id == id).ExecuteDelete() > 0;
    }

    public bool HasAppointments(Guid id)
    {
        using var db = _factory.CreateDbContext();
        return db.Appointments.Any(appointment => appointment.DoctorId == id);
    }

    private static IQueryable<DoctorEntity> Query(AppDbContext db) =>
        db.Doctors.AsNoTracking().Include(doctor => doctor.Specialty);

    private static void Apply(DoctorEntity entity, Doctor doctor)
    {
        entity.Name = doctor.Name;
        entity.SpecialtyId = doctor.SpecialtyId;
        entity.ExperienceYears = doctor.ExperienceYears;
        entity.ConsultationFee = doctor.ConsultationFee;
        entity.FollowUpFee = doctor.FollowUpFee;
        entity.Languages = string.Join(',', doctor.Languages);
        entity.Bio = doctor.Bio;
    }

    private static Doctor ToDoctor(DoctorEntity entity) =>
        new(entity.Id, entity.Name, entity.SpecialtyId, entity.Specialty.Name, entity.ExperienceYears,
            entity.ConsultationFee, entity.FollowUpFee,
            entity.Languages.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
            entity.Bio);
}

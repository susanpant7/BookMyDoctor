using BookMyDoctor.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace BookMyDoctor.Persistence;

public sealed class UsersRepo : IUsersRepo
{
    private readonly IDbContextFactory<AppDbContext> _factory;

    public UsersRepo(IDbContextFactory<AppDbContext> factory)
    {
        _factory = factory;
    }

    public IReadOnlyList<UserAccount> GetAll()
    {
        using var db = _factory.CreateDbContext();
        return db.Users.AsNoTracking().OrderBy(user => user.Name).ToList().Select(ToAccount).ToList();
    }

    public UserAccount? GetById(Guid id)
    {
        using var db = _factory.CreateDbContext();
        var entity = db.Users.AsNoTracking().FirstOrDefault(user => user.Id == id);
        return entity is null ? null : ToAccount(entity);
    }

    public UserAccount? GetByEmail(string email)
    {
        using var db = _factory.CreateDbContext();
        var entity = db.Users.AsNoTracking().FirstOrDefault(user => user.Email == email);   // NOCASE column
        return entity is null ? null : ToAccount(entity);
    }

    public bool EmailExists(string email, Guid? excludeUserId = null)
    {
        using var db = _factory.CreateDbContext();
        return db.Users.Any(user => user.Email == email && user.Id != excludeUserId);
    }

    public void Add(UserAccount user)
    {
        using var db = _factory.CreateDbContext();
        db.Users.Add(new UserEntity { Id = user.Id, Name = user.Name, Email = user.Email, PasswordHash = user.PasswordHash });
        db.SaveChanges();
    }

    public bool Update(UserAccount user)
    {
        using var db = _factory.CreateDbContext();
        var entity = db.Users.Find(user.Id);
        if (entity is null)
        {
            return false;
        }

        entity.Name = user.Name;
        entity.Email = user.Email;
        if (user.PasswordHash.Length > 0)
        {
            entity.PasswordHash = user.PasswordHash;
        }
        db.SaveChanges();
        return true;
    }

    public bool Delete(Guid id)
    {
        using var db = _factory.CreateDbContext();
        return db.Users.Where(user => user.Id == id).ExecuteDelete() > 0;
    }

    public bool HasAppointments(Guid id)
    {
        using var db = _factory.CreateDbContext();
        return db.Appointments.Any(appointment => appointment.PatientId == id);
    }

    private static UserAccount ToAccount(UserEntity entity) => new(entity.Id, entity.Name, entity.Email, entity.PasswordHash);
}

public sealed record UserAccount(Guid Id, string Name, string Email, string PasswordHash);

using static BookMyDoctor.Persistence.SeedIds;

namespace BookMyDoctor.Persistence;

public sealed class UsersRepo : IUsersRepo
{
    private static readonly IReadOnlyList<UserAccount> Seed =
    [
        new(HariPatientId, "Hari Bahadur", "hb@gmail.com", "susan123"),
        new(MadanPatientId, "Madan Bahadur", "mb@gmail.com", "susan123")
    ];

    private readonly List<UserAccount> _users = [.. Seed];

    public UserAccount? GetById(Guid id) => _users.FirstOrDefault(user => user.Id == id);

    public UserAccount? GetByEmail(string email) =>
        _users.FirstOrDefault(user => user.Email.Equals(email, StringComparison.OrdinalIgnoreCase));

    public bool EmailExists(string email) => GetByEmail(email) is not null;

    public void Add(UserAccount user) => _users.Add(user);
}

public sealed record UserAccount(Guid Id, string Name, string Email, string Password);

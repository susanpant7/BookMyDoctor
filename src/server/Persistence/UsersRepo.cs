namespace BookMyDoctor.Persistence;

public sealed class UsersRepo : IUsersRepo
{
    private readonly List<UserAccount> _users = [.. StaticData.Users];

    public UserAccount? GetById(Guid id) => _users.FirstOrDefault(user => user.Id == id);

    public UserAccount? GetByEmail(string email) =>
        _users.FirstOrDefault(user => user.Email.Equals(email, StringComparison.OrdinalIgnoreCase));

    public bool EmailExists(string email) => GetByEmail(email) is not null;

    public void Add(UserAccount user) => _users.Add(user);
}

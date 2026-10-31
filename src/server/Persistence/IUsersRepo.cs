namespace BookMyDoctor.Persistence;

public interface IUsersRepo
{
    IReadOnlyList<UserAccount> GetAll();
    UserAccount? GetById(Guid id);
    UserAccount? GetByEmail(string email);
    bool EmailExists(string email, Guid? excludeUserId = null);
    void Add(UserAccount user);

    // False when no user has the id. An empty password hash keeps the existing one.
    bool Update(UserAccount user);

    // False when no user has the id. The user's chat messages go with them.
    bool Delete(Guid id);

    bool HasAppointments(Guid id);
}

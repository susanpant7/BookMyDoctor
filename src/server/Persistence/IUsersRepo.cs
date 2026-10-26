namespace BookMyDoctor.Persistence;

public interface IUsersRepo
{
    UserAccount? GetById(Guid id);
    UserAccount? GetByEmail(string email);
    bool EmailExists(string email);
    void Add(UserAccount user);
}

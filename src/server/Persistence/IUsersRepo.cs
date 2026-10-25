namespace BookMyDoctor.Persistence;

public interface IUsersRepo
{
    UserAccount? GetByEmail(string email);
    bool EmailExists(string email);
    void Add(UserAccount user);
}

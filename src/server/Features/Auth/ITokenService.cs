using BookMyDoctor.Persistence;

namespace BookMyDoctor.Features.Auth;

public interface ITokenService
{
    AccessToken Create(UserAccount user);
}

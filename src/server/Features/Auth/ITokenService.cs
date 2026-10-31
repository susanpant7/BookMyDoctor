using MyHospitalAI.Persistence;

namespace MyHospitalAI.Features.Auth;

public interface ITokenService
{
    AccessToken Create(UserAccount user);
    AccessToken CreateAdmin(string username);
}

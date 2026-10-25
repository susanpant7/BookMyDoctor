using BookMyDoctor.Persistence;

namespace BookMyDoctor.Features.Auth;

public sealed class AuthService : IAuthService
{
    private readonly IUsersRepo _usersRepo;

    public AuthService(IUsersRepo usersRepo)
    {
        _usersRepo = usersRepo;
    }

    public AuthResponse Register(RegisterRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name) ||
            string.IsNullOrWhiteSpace(request.Email) ||
            string.IsNullOrWhiteSpace(request.Password))
        {
            throw new ArgumentException("Name, email, and password are required.");
        }

        if (_usersRepo.EmailExists(request.Email))
        {
            throw new InvalidOperationException("An account with that email already exists.");
        }

        var user = new UserAccount(Guid.NewGuid(), request.Name.Trim(), request.Email.Trim(), request.Password);
        _usersRepo.Add(user);
        return new AuthResponse(user.Id, user.Name, user.Email);
    }

    public AuthResponse Login(LoginRequest request)
    {
        var user = _usersRepo.GetByEmail(request.Email);
        if (user is null || user.Password != request.Password)
        {
            throw new UnauthorizedAccessException("Invalid email or password.");
        }

        return new AuthResponse(user.Id, user.Name, user.Email);
    }
}

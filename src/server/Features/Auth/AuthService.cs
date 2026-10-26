using BookMyDoctor.Persistence;

namespace BookMyDoctor.Features.Auth;

public sealed class AuthService : IAuthService
{
    private readonly IUsersRepo _usersRepo;
    private readonly ITokenService _tokenService;

    public AuthService(IUsersRepo usersRepo, ITokenService tokenService)
    {
        _usersRepo = usersRepo;
        _tokenService = tokenService;
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
        return CreateResponse(user);
    }

    public AuthResponse Login(LoginRequest request)
    {
        var user = _usersRepo.GetByEmail(request.Email);
        if (user is null || user.Password != request.Password)
        {
            throw new UnauthorizedAccessException("Invalid email or password.");
        }

        return CreateResponse(user);
    }

    private AuthResponse CreateResponse(UserAccount user)
    {
        var token = _tokenService.Create(user);
        return new AuthResponse(user.Id, user.Name, user.Email, token.Token, token.ExpiresAt);
    }
}

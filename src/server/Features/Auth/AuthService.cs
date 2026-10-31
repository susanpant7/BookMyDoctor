using System.Security.Cryptography;
using System.Text;
using BookMyDoctor.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace BookMyDoctor.Features.Auth;

public sealed class AuthService : IAuthService
{
    private readonly IUsersRepo _usersRepo;
    private readonly ITokenService _tokenService;
    private readonly IPasswordHasher<UserAccount> _passwordHasher;
    private readonly AdminOptions _admin;

    public AuthService(
        IUsersRepo usersRepo,
        ITokenService tokenService,
        IPasswordHasher<UserAccount> passwordHasher,
        IOptions<AdminOptions> admin)
    {
        _usersRepo = usersRepo;
        _tokenService = tokenService;
        _passwordHasher = passwordHasher;
        _admin = admin.Value;
    }

    public AuthResponse Register(RegisterRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name) ||
            string.IsNullOrWhiteSpace(request.Email) ||
            string.IsNullOrWhiteSpace(request.Password))
        {
            throw new ArgumentException("Name, email, and password are required.");
        }

        if (_usersRepo.EmailExists(request.Email.Trim()))
        {
            throw new InvalidOperationException("An account with that email already exists.");
        }

        var user = new UserAccount(Guid.NewGuid(), request.Name.Trim(), request.Email.Trim(), "");
        _usersRepo.Add(user with { PasswordHash = _passwordHasher.HashPassword(user, request.Password) });
        return CreateResponse(_usersRepo.GetById(user.Id)!);
    }

    public AuthResponse Login(LoginRequest request)
    {
        if (IsAdmin(request))
        {
            var token = _tokenService.CreateAdmin(_admin.Username);
            return new AuthResponse(Guid.Empty, _admin.Username, "", Roles.Admin, token.Token, token.ExpiresAt);
        }

        var user = _usersRepo.GetByEmail(request.Username.Trim());
        if (user is null || _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, request.Password) == PasswordVerificationResult.Failed)
        {
            throw new UnauthorizedAccessException("Invalid email or password.");
        }

        return CreateResponse(user);
    }

    private bool IsAdmin(LoginRequest request) =>
        request.Username == _admin.Username &&
        CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(request.Password), Encoding.UTF8.GetBytes(_admin.Password));

    private AuthResponse CreateResponse(UserAccount user)
    {
        var token = _tokenService.Create(user);
        return new AuthResponse(user.Id, user.Name, user.Email, Roles.Patient, token.Token, token.ExpiresAt);
    }
}

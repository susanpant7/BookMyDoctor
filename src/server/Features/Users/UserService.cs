using System.Net.Mail;
using BookMyDoctor.Features.Auth;
using BookMyDoctor.Features.Common;
using BookMyDoctor.Persistence;
using Microsoft.AspNetCore.Identity;

namespace BookMyDoctor.Features.Users;

public sealed class UserService : IUserService
{
    private const int MinPasswordLength = 6;

    private readonly IUsersRepo _usersRepo;
    private readonly IPasswordHasher<UserAccount> _passwordHasher;

    public UserService(IUsersRepo usersRepo, IPasswordHasher<UserAccount> passwordHasher)
    {
        _usersRepo = usersRepo;
        _passwordHasher = passwordHasher;
    }

    public IReadOnlyList<User> GetAll() => _usersRepo.GetAll().Select(ToUser).ToList();

    public User Create(CreateUserRequest request)
    {
        var (name, email) = Check(request.Name, request.Email, excludeId: null);
        CheckPassword(request.Password);
        return Add(Guid.NewGuid(), name, email, request.Password);
    }

    public User Update(Guid id, UpdateUserRequest request)
    {
        var (name, email) = Check(request.Name, request.Email, excludeId: id);
        var user = _usersRepo.GetById(id) ?? throw new KeyNotFoundException("Patient not found.");

        var hash = "";
        if (!string.IsNullOrEmpty(request.Password))
        {
            CheckPassword(request.Password);
            hash = _passwordHasher.HashPassword(user, request.Password);
        }

        _usersRepo.Update(new UserAccount(id, name, email, hash));
        return ToUser(_usersRepo.GetById(id)!);
    }

    public void Delete(Guid id)
    {
        if (_usersRepo.GetById(id) is null)
        {
            throw new KeyNotFoundException("Patient not found.");
        }
        if (_usersRepo.HasAppointments(id))
        {
            throw new ConflictException("This patient has appointments, so they cannot be deleted.");
        }
        _usersRepo.Delete(id);
    }

    public LoadDefaultsResult LoadDefaults(string password)
    {
        CheckPassword(password);
        var added = 0;
        foreach (var (id, name, email) in DefaultData.Patients)
        {
            if (_usersRepo.EmailExists(email))
            {
                continue;
            }
            Add(id, name, email, password);
            added++;
        }
        return new LoadDefaultsResult(added, DefaultData.Patients.Count - added);
    }

    private User Add(Guid id, string name, string email, string password)
    {
        var account = new UserAccount(id, name, email, "");
        _usersRepo.Add(account with { PasswordHash = _passwordHasher.HashPassword(account, password) });
        return ToUser(_usersRepo.GetById(id)!);
    }

    private (string Name, string Email) Check(string? name, string? email, Guid? excludeId)
    {
        name = name?.Trim() ?? "";
        email = email?.Trim() ?? "";
        if (name.Length == 0 || name.Length > 100)
        {
            throw new ArgumentException("The name is required and can be up to 100 characters.");
        }
        if (email.Length > 256 || !MailAddress.TryCreate(email, out var address) || address.Address != email)
        {
            throw new ArgumentException("Enter a valid email address.");
        }
        if (_usersRepo.EmailExists(email, excludeId))
        {
            throw new ConflictException("An account with that email already exists.");
        }
        return (name, email);
    }

    private static void CheckPassword(string? password)
    {
        if (password is null || password.Length < MinPasswordLength)
        {
            throw new ArgumentException($"The password must be at least {MinPasswordLength} characters.");
        }
    }

    private static User ToUser(UserAccount account) => new(account.Id, account.Name, account.Email);
}

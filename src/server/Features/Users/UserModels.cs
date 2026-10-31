namespace BookMyDoctor.Features.Users;

public sealed record CreateUserRequest(string Name, string Email, string Password);

// An empty password keeps the existing one.
public sealed record UpdateUserRequest(string Name, string Email, string? Password);

public sealed record LoadDefaultUsersRequest(string Password);

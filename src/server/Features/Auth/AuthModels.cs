namespace BookMyDoctor.Features.Auth;

public sealed record User(Guid Id, string Name, string Email);

public sealed record RegisterRequest(string Name, string Email, string Password);

// Patients sign in with their email; the admin with the configured username.
public sealed record LoginRequest(string Username, string Password);

public sealed record AccessToken(string Token, DateTimeOffset ExpiresAt);

// For the admin, UserId is empty and Email is blank.
public sealed record AuthResponse(Guid UserId, string Name, string Email, string Role, string Token, DateTimeOffset ExpiresAt);

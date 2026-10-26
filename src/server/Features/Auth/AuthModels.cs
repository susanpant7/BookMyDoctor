namespace BookMyDoctor.Features.Auth;

public sealed record User(Guid Id, string Name, string Email);

public sealed record RegisterRequest(string Name, string Email, string Password);

public sealed record LoginRequest(string Email, string Password);

public sealed record AccessToken(string Token, DateTimeOffset ExpiresAt);

public sealed record AuthResponse(Guid UserId, string Name, string Email, string Token, DateTimeOffset ExpiresAt);

namespace MyHospitalAI.Features.Auth;

public sealed record User(Guid Id, string Name, string Email);

public sealed record RegisterRequest(string Name, string Email, string Password);

// Patients sign in with their email; doctors and the admin with a username.
public sealed record LoginRequest(string Username, string Password);

public sealed record AccessToken(string Token, DateTimeOffset ExpiresAt);

// For the admin, UserId is empty and Email is blank. For a doctor, UserId is the doctor's id and Email is their username.
public sealed record AuthResponse(Guid UserId, string Name, string Email, string Role, string Token, DateTimeOffset ExpiresAt);

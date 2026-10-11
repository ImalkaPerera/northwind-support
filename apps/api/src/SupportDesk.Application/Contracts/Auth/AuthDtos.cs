namespace SupportDesk.Application.Contracts.Auth;

/// <summary>Body of POST /api/auth/login.</summary>
public sealed record LoginRequest(string Email, string Password);

/// <summary>The agent as the web app greets them. Never includes the password hash.</summary>
public sealed record CurrentUserDto(int Id, string FullName, string Email);

/// <summary>The token to send with every request, when it expires, and who it is for.</summary>
public sealed record LoginResponse(string Token, DateTime ExpiresAtUtc, CurrentUserDto User);

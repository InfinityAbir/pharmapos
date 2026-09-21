namespace PharmaPOS.Application.Features.Auth;

/// <summary>Tokens handed to the SPA after login, refresh or registration.</summary>
public sealed record LoginResponse(
    string AccessToken,
    string? RefreshToken,
    int ExpiresInSeconds,
    string TokenType = "Bearer");

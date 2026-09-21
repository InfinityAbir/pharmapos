namespace PharmaPOS.Application.Common.Identity;

/// <summary>Tokens returned by Keycloak's token endpoint (login and refresh grants).</summary>
public sealed record KeycloakTokenResult(
    string AccessToken,
    int ExpiresInSeconds,
    string? RefreshToken);

/// <summary>
/// Thin client over Keycloak's token endpoint and Admin REST API. All traffic stays server-side
/// so the confidential client secret never reaches the browser — the same shape as the
/// recruitment system's client, trimmed to what the POS needs (login, refresh, registration).
/// </summary>
public interface IKeycloakClient
{
    /// <summary>
    /// Resource Owner Password Credentials grant. Throws <c>UnauthorizedException</c> on
    /// invalid credentials and <c>BusinessRuleException</c> when Keycloak is unreachable.
    /// </summary>
    Task<KeycloakTokenResult> TokenAsync(
        string username,
        string password,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Refresh grant. Throws <c>UnauthorizedException</c> when the refresh token expired or was
    /// revoked, <c>BusinessRuleException</c> when Keycloak is unreachable.
    /// </summary>
    Task<KeycloakTokenResult> RefreshTokenAsync(
        string refreshToken,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a realm user via the Admin REST API (service-account client-credentials grant),
    /// sets the <c>tenant_id</c> attribute the tenancy strategy reads, and assigns the realm
    /// role. Throws <c>ConflictException</c> when the username/email already exists.
    /// </summary>
    Task<string> CreateUserAsync(
        string username,
        string email,
        string firstName,
        string lastName,
        string password,
        string tenantId,
        string realmRole,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a realm user via the Admin REST API. Best-effort/non-throwing: used to roll back
    /// an orphaned Keycloak account when registration fails after the user was created.
    /// </summary>
    Task DeleteUserAsync(string keycloakUserId, string email);
}

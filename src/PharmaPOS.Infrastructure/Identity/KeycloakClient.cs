using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PharmaPOS.Application.Common.Identity;
using PharmaPOS.Domain.Exceptions;

namespace PharmaPOS.Infrastructure.Identity;

/// <summary>
/// Server-side Keycloak client: ROPC login/refresh proxy plus Admin REST user provisioning.
/// Usernames and passwords pass through but are never logged; only outcomes are.
/// </summary>
public sealed class KeycloakClient : IKeycloakClient
{
    private readonly HttpClient _httpClient;
    private readonly KeycloakOptions _options;
    private readonly ILogger<KeycloakClient> _logger;

    public KeycloakClient(
        HttpClient httpClient,
        IOptions<KeycloakOptions> options,
        ILogger<KeycloakClient> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    private string TokenEndpoint => $"{AuthorityRoot()}/protocol/openid-connect/token";
    private string AdminUsersEndpoint => $"{BaseUrl()}/admin/realms/{Realm()}/users";
    private string AdminRolesEndpoint => $"{BaseUrl()}/admin/realms/{Realm()}/roles";

    public async Task<KeycloakTokenResult> TokenAsync(
        string username,
        string password,
        CancellationToken cancellationToken = default)
    {
        RequireClientSecret();

        var response = await PostFormAsync(TokenEndpoint, new Dictionary<string, string>
        {
            ["grant_type"] = "password",
            ["client_id"] = _options.ClientId,
            ["client_secret"] = _options.ClientSecret,
            ["username"] = username,
            ["password"] = password,
        }, cancellationToken);

        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.BadRequest)
            {
                _logger.LogWarning("Keycloak rejected a login attempt for {Username}.", username);
                throw new UnauthorizedException("auth.invalid_credentials", "Invalid email or password.");
            }

            throw KeycloakDown(response.StatusCode);
        }

        return ReadTokens(body);
    }

    public async Task<KeycloakTokenResult> RefreshTokenAsync(
        string refreshToken,
        CancellationToken cancellationToken = default)
    {
        RequireClientSecret();

        var response = await PostFormAsync(TokenEndpoint, new Dictionary<string, string>
        {
            ["grant_type"] = "refresh_token",
            ["client_id"] = _options.ClientId,
            ["client_secret"] = _options.ClientSecret,
            ["refresh_token"] = refreshToken,
        }, cancellationToken);

        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.BadRequest)
            {
                _logger.LogWarning("Keycloak rejected a refresh-token exchange.");
                throw new UnauthorizedException("auth.session_expired", "The session has expired. Sign in again.");
            }

            throw KeycloakDown(response.StatusCode);
        }

        return ReadTokens(body);
    }

    public async Task<string> CreateUserAsync(
        string username,
        string email,
        string firstName,
        string lastName,
        string password,
        string tenantId,
        string realmRole,
        CancellationToken cancellationToken = default)
    {
        RequireClientSecret();

        var adminToken = await AdminTokenAsync(cancellationToken);

        var payload = new
        {
            username,
            email,
            firstName,
            lastName,
            enabled = true,
            emailVerified = false,
            attributes = new Dictionary<string, string[]> { ["tenant_id"] = [tenantId] },
            credentials = new[] { new { type = "password", value = password, temporary = false } },
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, AdminUsersEndpoint)
        {
            Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json"),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

        var response = await SendAsync(request, cancellationToken);

        if (response.StatusCode == HttpStatusCode.Conflict)
        {
            throw new ConflictException("auth.email_taken", "An account with this email already exists.");
        }

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogError("Keycloak user creation failed ({Status}): {Body}", (int)response.StatusCode, body);
            throw KeycloakDown(response.StatusCode);
        }

        var userId = response.Headers.Location?.Segments.Last().TrimEnd('/')
            ?? throw new BusinessRuleException("auth.keycloak_error", "Keycloak did not return the created user's id.");

        try
        {
            await AssignRealmRoleAsync(adminToken, userId, realmRole, cancellationToken);
        }
        catch
        {
            await DeleteUserSafelyAsync(adminToken, userId, email);
            throw;
        }

        return userId;
    }

    public async Task DeleteUserAsync(string keycloakUserId, string email)
    {
        try
        {
            RequireClientSecret();
            var adminToken = await AdminTokenAsync();
            await DeleteUserSafelyAsync(adminToken, keycloakUserId, email);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Could not roll back the Keycloak account for {Email}.", email);
        }
    }

    private async Task<string> AdminTokenAsync(CancellationToken cancellationToken = default)
    {
        var response = await PostFormAsync(TokenEndpoint, new Dictionary<string, string>
        {
            ["grant_type"] = "client_credentials",
            ["client_id"] = _options.ClientId,
            ["client_secret"] = _options.ClientSecret,
        }, cancellationToken);

        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("Keycloak service-account token request failed ({Status}).", (int)response.StatusCode);
            throw KeycloakDown(response.StatusCode);
        }

        using var json = JsonDocument.Parse(body);
        return json.RootElement.GetProperty("access_token").GetString()!;
    }

    private async Task AssignRealmRoleAsync(
        string adminToken,
        string userId,
        string realmRole,
        CancellationToken cancellationToken)
    {
        using var roleRequest = new HttpRequestMessage(HttpMethod.Get, $"{AdminRolesEndpoint}/{realmRole}");
        roleRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
        var roleResponse = await SendAsync(roleRequest, cancellationToken);

        if (!roleResponse.IsSuccessStatusCode)
        {
            throw new BusinessRuleException("auth.keycloak_error", $"Keycloak realm role '{realmRole}' was not found.");
        }

        var roleJson = await roleResponse.Content.ReadAsStringAsync(cancellationToken);

        using var mappingRequest = new HttpRequestMessage(
            HttpMethod.Post, $"{AdminUsersEndpoint}/{userId}/role-mappings/realm")
        {
            Content = new StringContent($"[{roleJson}]", Encoding.UTF8, "application/json"),
        };
        mappingRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
        var mappingResponse = await SendAsync(mappingRequest, cancellationToken);

        if (!mappingResponse.IsSuccessStatusCode)
        {
            throw new BusinessRuleException("auth.keycloak_error", "Keycloak role assignment failed.");
        }
    }

    private async Task DeleteUserSafelyAsync(string adminToken, string userId, string email)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Delete, $"{AdminUsersEndpoint}/{userId}");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
            var response = await SendAsync(request);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Could not roll back the Keycloak account for {Email} ({Status}).", email, (int)response.StatusCode);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Could not roll back the Keycloak account for {Email}.", email);
        }
    }

    private void RequireClientSecret()
    {
        if (string.IsNullOrWhiteSpace(_options.ClientSecret))
        {
            throw new BusinessRuleException(
                "auth.keycloak_not_configured",
                "Keycloak confidential client is not configured. Set Keycloak__ClientSecret.");
        }
    }

    private string AuthorityRoot() => _options.Authority.TrimEnd('/');

    private string Realm() => AuthorityRoot().Split('/').Last();

    private string BaseUrl()
    {
        var trimmed = AuthorityRoot();
        var index = trimmed.LastIndexOf("/realms/", StringComparison.OrdinalIgnoreCase);
        return index > 0 ? trimmed[..index] : trimmed;
    }

    private static KeycloakTokenResult ReadTokens(string body)
    {
        using var json = JsonDocument.Parse(body);
        return new KeycloakTokenResult(
            AccessToken: json.RootElement.GetProperty("access_token").GetString()!,
            ExpiresInSeconds: json.RootElement.GetProperty("expires_in").GetInt32(),
            RefreshToken: json.RootElement.TryGetProperty("refresh_token", out var refresh)
                ? refresh.GetString()
                : null);
    }

    private BusinessRuleException KeycloakDown(HttpStatusCode status) =>
        new("auth.keycloak_unavailable", $"The login service returned {(int)status}. Try again shortly.");

    private async Task<HttpResponseMessage> PostFormAsync(
        string url,
        Dictionary<string, string> form,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await _httpClient.PostAsync(url, new FormUrlEncodedContent(form), cancellationToken);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            _logger.LogWarning(ex, "Keycloak server is unreachable.");
            throw new BusinessRuleException("auth.keycloak_unavailable", "The login service is unreachable. Try again shortly.");
        }
    }

    private async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await _httpClient.SendAsync(request, cancellationToken);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            _logger.LogWarning(ex, "Keycloak server is unreachable.");
            throw new BusinessRuleException("auth.keycloak_unavailable", "The login service is unreachable. Try again shortly.");
        }
    }
}

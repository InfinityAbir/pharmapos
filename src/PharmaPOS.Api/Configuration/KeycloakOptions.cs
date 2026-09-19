using System.ComponentModel.DataAnnotations;

namespace PharmaPOS.Api.Configuration;

/// <summary>
/// Keycloak (OIDC) settings. Validated at startup so misconfiguration fails fast rather than
/// silently accepting unauthenticated traffic.
/// </summary>
public sealed class KeycloakOptions
{
    public const string SectionName = "Keycloak";

    /// <summary>Realm authority URL, e.g. <c>https://keycloak.example.com/realms/pharmapos</c>.</summary>
    [Required]
    public string Authority { get; set; } = string.Empty;

    /// <summary>Expected token audience for the API resource server.</summary>
    [Required]
    public string Audience { get; set; } = string.Empty;

    /// <summary>
    /// Accepted token issuers. Required in split-horizon deployments where the API contacts
    /// Keycloak over an internal address (e.g. <c>http://keycloak:8080/realms/pharmapos</c>) while
    /// browsers obtain tokens whose issuer is the public URL
    /// (e.g. <c>http://localhost:8090/realms/pharmapos</c>). When set, these replace the issuer
    /// advertised by the discovery document during token validation.
    /// </summary>
    public string[] ValidIssuers { get; set; } = [];

    /// <summary>
    /// Must be false only for local HTTP development. Production must use HTTPS so token
    /// metadata retrieval cannot be tampered with.
    /// </summary>
    public bool RequireHttpsMetadata { get; set; } = true;
}

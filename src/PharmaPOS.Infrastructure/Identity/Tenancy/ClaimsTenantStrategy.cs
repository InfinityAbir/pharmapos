using Microsoft.AspNetCore.Http;
using Finbuckle.MultiTenant.Abstractions;

namespace PharmaPOS.Infrastructure.Identity.Tenancy;

/// <summary>
/// Resolves the tenant identifier from the authenticated principal only.
///
/// The identifier comes from the <c>tenant_id</c> claim inside the Keycloak-issued access token,
/// which is signature-validated before this strategy runs (Finbuckle's middleware is placed after
/// authentication). It is therefore server-derived and cannot be spoofed by the Angular client
/// through a URL, query string, body, header or local storage.
///
/// Phase 1 adds membership validation: the claim will be checked against
/// <c>TenantMembership</c>, and users with multiple memberships will select an active tenant
/// explicitly (validated server-side). Until then the claim alone selects the store record.
/// </summary>
public sealed class ClaimsTenantStrategy : IMultiTenantStrategy
{
    public const string TenantClaimType = "tenant_id";

    private readonly IHttpContextAccessor _httpContextAccessor;

    public ClaimsTenantStrategy(IHttpContextAccessor httpContextAccessor)
        => _httpContextAccessor = httpContextAccessor;

    public int Priority => 0;

    public Task<string?> GetIdentifierAsync(object context)
    {
        if (context is not HttpContext httpContext)
        {
            return Task.FromResult<string?>(null);
        }

        var identifier = httpContext.User.FindFirst(TenantClaimType)?.Value;

        return Task.FromResult(string.IsNullOrWhiteSpace(identifier) ? null : identifier);
    }
}

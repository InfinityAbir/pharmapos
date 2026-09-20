using Microsoft.EntityFrameworkCore;
using PharmaPOS.Application.Common.Authorization;
using PharmaPOS.Domain.Identity;
using PharmaPOS.Infrastructure.Persistence;

namespace PharmaPOS.Infrastructure.Identity;

/// <summary>
/// Resolves a member's roles and permissions from the database, scoped to the current tenant by
/// Finbuckle. The identity is matched on Keycloak subject first (freshly provisioned users), then on
/// case-insensitive email == Keycloak username (locally seeded demo members). Returns <c>null</c> when
/// the identity has no active membership in the current tenant.
/// </summary>
public sealed class MembershipResolver : IMembershipResolver
{
    private readonly AppDbContext _dbContext;

    public MembershipResolver(AppDbContext dbContext)
        => _dbContext = dbContext;

    public async Task<MemberAccess?> ResolveAccessAsync(
        string subjectOrUsername,
        CancellationToken cancellationToken)
    {
        var key = subjectOrUsername?.Trim();
        if (string.IsNullOrEmpty(key))
        {
            return null;
        }

        var email = key.ToLowerInvariant();

        var user = await _dbContext.ApplicationUsers
            .AsNoTracking()
            .FirstOrDefaultAsync(
                user => (user.KeycloakSubject != null && user.KeycloakSubject == key)
                        || user.Email == email,
                cancellationToken);

        if (user is null || !user.IsActive)
        {
            return null;
        }

        var membership = await _dbContext.TenantMemberships
            .AsNoTracking()
            .Include(membership => membership.MembershipRoles)
                .ThenInclude(membershipRole => membershipRole.Role)
                    .ThenInclude(role => role.RolePermissions)
                        .ThenInclude(rolePermission => rolePermission.Permission)
            .FirstOrDefaultAsync(
                membership => membership.ApplicationUserId == user.Id
                              && membership.Status == MembershipStatus.Active,
                cancellationToken);

        if (membership is null)
        {
            return null;
        }

        return new MemberAccess(
            UserId: user.Id,
            MembershipId: membership.Id,
            Email: user.Email,
            IsDefault: membership.IsDefault,
            Roles: membership.MembershipRoles
                .Select(membershipRole => membershipRole.Role.Name)
                .Distinct()
                .OrderBy(name => name)
                .ToArray(),
            Permissions: membership.MembershipRoles
                .SelectMany(membershipRole => membershipRole.Role.RolePermissions)
                .Select(rolePermission => rolePermission.Permission.Code)
                .Distinct()
                .OrderBy(code => code)
                .ToArray());
    }
}
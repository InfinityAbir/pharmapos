using Microsoft.EntityFrameworkCore;
using PharmaPOS.Application.Common.Tenancy;
using PharmaPOS.Application.Features.Tenancy;
using PharmaPOS.Domain.Exceptions;
using PharmaPOS.Domain.Identity;
using PharmaPOS.Infrastructure.Persistence;

namespace PharmaPOS.Infrastructure.Identity.Tenancy;

/// <summary>
/// Manages the current tenant's memberships. Scoped to the current tenant by the filtered
/// <see cref="AppDbContext"/>; role assignments are validated against the same tenant's roles.
/// </summary>
public sealed class TenantMembershipRepository : ITenantMembershipRepository
{
    private readonly AppDbContext _dbContext;

    public TenantMembershipRepository(AppDbContext dbContext)
        => _dbContext = dbContext;

    public async Task<IReadOnlyList<MemberDto>> ListAsync(CancellationToken cancellationToken)
    {
        var memberships = await _dbContext.TenantMemberships
            .AsNoTracking()
            .Include(membership => membership.ApplicationUser)
            .Include(membership => membership.MembershipRoles)
                .ThenInclude(membershipRole => membershipRole.Role)
            .OrderBy(membership => membership.ApplicationUser.Email)
            .ToListAsync(cancellationToken);

        return memberships.Select(ToDto).ToArray();
    }

    public async Task<MemberDto> SetStatusAsync(
        Guid membershipId,
        string status,
        CancellationToken cancellationToken)
    {
        var membership = await LoadAsync(membershipId, cancellationToken);

        membership.Status = status;
        await _dbContext.SaveChangesAsync(cancellationToken);

        return ToDto(membership);
    }

    public async Task<MemberDto> ReplaceRolesAsync(
        Guid membershipId,
        IReadOnlyList<Guid> roleIds,
        CancellationToken cancellationToken)
    {
        var membership = await LoadAsync(membershipId, cancellationToken);

        var distinctRoleIds = roleIds.Distinct().ToArray();

        if (distinctRoleIds.Length > 0)
        {
            var found = await _dbContext.Roles
                .Where(role => distinctRoleIds.Contains(role.Id))
                .Select(role => role.Id)
                .ToListAsync(cancellationToken);

            if (found.Count != distinctRoleIds.Length)
            {
                var missing = distinctRoleIds.Except(found).ToArray();
                throw new NotFoundException(
                    "role.not_found",
                    $"Role(s) not found in this tenant: {string.Join(", ", missing)}.");
            }
        }

        // Delete-then-insert in separate units of work: Clear()+Add on a single tracked
        // collection makes EF emit UPDATE and DELETE for the same rows under composite FKs.
        _dbContext.MembershipRoles.RemoveRange(membership.MembershipRoles);
        await _dbContext.SaveChangesAsync(cancellationToken);

        foreach (var roleId in distinctRoleIds)
        {
            _dbContext.MembershipRoles.Add(new MembershipRole
            {
                TenantId = membership.TenantId,
                MembershipId = membership.Id,
                RoleId = roleId,
            });
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        var refreshed = await _dbContext.TenantMemberships
            .AsNoTracking()
            .Include(candidate => candidate.ApplicationUser)
            .Include(candidate => candidate.MembershipRoles)
                .ThenInclude(membershipRole => membershipRole.Role)
            .FirstAsync(candidate => candidate.Id == membershipId, cancellationToken);

        return ToDto(refreshed);
    }

    private async Task<TenantMembership> LoadAsync(
        Guid membershipId,
        CancellationToken cancellationToken)
    {
        var membership = await _dbContext.TenantMemberships
            .Include(candidate => candidate.ApplicationUser)
            .Include(candidate => candidate.MembershipRoles)
                .ThenInclude(membershipRole => membershipRole.Role)
            .FirstOrDefaultAsync(candidate => candidate.Id == membershipId, cancellationToken);

        if (membership is null)
        {
            throw new NotFoundException(
                "membership.not_found",
                $"Membership '{membershipId}' was not found in this tenant.");
        }

        return membership;
    }

    private static MemberDto ToDto(TenantMembership membership)
        => new(
            MembershipId: membership.Id,
            Status: membership.Status,
            IsDefault: membership.IsDefault,
            UserId: membership.ApplicationUserId,
            Email: membership.ApplicationUser.Email,
            DisplayName: membership.ApplicationUser.DisplayName,
            UserActive: membership.ApplicationUser.IsActive,
            Roles: membership.MembershipRoles
                .Select(membershipRole => membershipRole.Role.Name)
                .Distinct()
                .OrderBy(name => name)
                .ToArray());
}
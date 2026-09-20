using Microsoft.EntityFrameworkCore;
using PharmaPOS.Application.Common.Authorization;
using PharmaPOS.Application.Common.Tenancy;
using PharmaPOS.Application.Features.Tenancy;
using PharmaPOS.Domain.Exceptions;
using PharmaPOS.Domain.Identity;
using PharmaPOS.Infrastructure.Persistence;

namespace PharmaPOS.Infrastructure.Identity.Tenancy;

/// <summary>
/// Manages the current tenant's roles. Scoped to the current tenant by the filtered
/// <see cref="AppDbContext"/>; permission codes are validated against the global catalog.
/// </summary>
public sealed class TenantRoleRepository : ITenantRoleRepository
{
    private readonly AppDbContext _dbContext;

    public TenantRoleRepository(AppDbContext dbContext)
        => _dbContext = dbContext;

    public async Task<IReadOnlyList<RoleDto>> ListAsync(CancellationToken cancellationToken)
    {
        var roles = await _dbContext.Roles
            .AsNoTracking()
            .Include(role => role.RolePermissions)
                .ThenInclude(rolePermission => rolePermission.Permission)
            .OrderBy(role => role.IsSystem)
            .ThenBy(role => role.Name)
            .ToListAsync(cancellationToken);

        return roles.Select(role => ToDto(role)).ToArray();
    }

    public async Task<RoleDto> CreateAsync(
        string name,
        string? description,
        IReadOnlyList<string> permissions,
        CancellationToken cancellationToken)
    {
        name = name.Trim();

        if (await _dbContext.Roles.AnyAsync(role => role.Name == name, cancellationToken))
        {
            throw new ConflictException(
                "role.duplicate",
                $"A role named '{name}' already exists.");
        }

        var permissionsByCode = await ResolvePermissionsAsync(permissions, cancellationToken);

        // TenantId is assigned explicitly (not left for Finbuckle to stamp) because the
        // tenant-composite alternate key must be non-null when tracking starts.
        var tenantId = TenantScope.CurrentTenantId(_dbContext);

        var role = new Role
        {
            TenantId = tenantId,
            Name = name,
            Description = Normalize(description),
            IsSystem = false,
            RolePermissions = permissionsByCode
                .Select(code => new RolePermission { TenantId = tenantId, PermissionId = code.Value })
                .ToList(),
        };

        _dbContext.Roles.Add(role);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return ToDto(role, permissionsByCode.Keys.ToArray());
    }

    public async Task<RoleDto> UpdateAsync(
        Guid id,
        string name,
        string? description,
        IReadOnlyList<string> permissions,
        CancellationToken cancellationToken)
    {
        name = name.Trim();

        var role = await _dbContext.Roles
            .Include(existing => existing.RolePermissions)
            .FirstOrDefaultAsync(existing => existing.Id == id, cancellationToken);

        if (role is null)
        {
            throw new NotFoundException("role.not_found", $"Role '{id}' was not found.");
        }

        if (role.IsSystem && role.Name != name)
        {
            throw new ForbiddenException(
                "role.system_immutable",
                $"System role '{role.Name}' cannot be renamed.");
        }

        if (role.Name != name
            && await _dbContext.Roles.AnyAsync(
                candidate => candidate.Name == name && candidate.Id != id,
                cancellationToken))
        {
            throw new ConflictException(
                "role.duplicate",
                $"A role named '{name}' already exists.");
        }

        var permissionsByCode = await ResolvePermissionsAsync(permissions, cancellationToken);

        // Delete-then-insert in separate units of work: Clear()+Add on a single tracked
        // collection makes EF emit UPDATE and DELETE for the same rows under composite FKs.
        _dbContext.RolePermissions.RemoveRange(role.RolePermissions);
        await _dbContext.SaveChangesAsync(cancellationToken);

        foreach (var entry in permissionsByCode)
        {
            _dbContext.RolePermissions.Add(new RolePermission
            {
                TenantId = role.TenantId,
                RoleId = role.Id,
                PermissionId = entry.Value,
            });
        }

        role.Name = name;
        role.Description = Normalize(description);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return ToDto(role, permissionsByCode.Keys.ToArray());
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var role = await _dbContext.Roles
            .AsNoTracking()
            .FirstOrDefaultAsync(candidate => candidate.Id == id, cancellationToken);

        if (role is null)
        {
            throw new NotFoundException("role.not_found", $"Role '{id}' was not found.");
        }

        if (role.IsSystem)
        {
            throw new ForbiddenException(
                "role.system_immutable",
                $"System role '{role.Name}' cannot be deleted.");
        }

        var toRemove = await _dbContext.Roles
            .Include(candidate => candidate.RolePermissions)
            .Include(candidate => candidate.MembershipRoles)
            .FirstAsync(candidate => candidate.Id == id, cancellationToken);

        _dbContext.Roles.Remove(toRemove);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task<IReadOnlyDictionary<string, Guid>> ResolvePermissionsAsync(
        IReadOnlyList<string> codes,
        CancellationToken cancellationToken)
    {
        var distinct = codes.Distinct().ToArray();

        var unknown = distinct.Where(code => !Permissions.Contains(code)).ToArray();
        if (unknown.Length > 0)
        {
            throw new BusinessRuleException(
                "permission.unknown",
                $"Unknown permission code(s): {string.Join(", ", unknown)}.");
        }

        var permissionsByCode = await _dbContext.Permissions
            .AsNoTracking()
            .Where(permission => distinct.Contains(permission.Code))
            .ToDictionaryAsync(permission => permission.Code, cancellationToken);

        if (permissionsByCode.Count != distinct.Length)
        {
            var missing = distinct.Except(permissionsByCode.Keys).ToArray();
            throw new BusinessRuleException(
                "permission.not_seeded",
                $"Permission code(s) not registered in the database: {string.Join(", ", missing)}.");
        }

        return permissionsByCode.ToDictionary(
            pair => pair.Key,
            pair => pair.Value.Id);
    }

    private static RoleDto ToDto(Role role, IReadOnlyList<string>? permissions = null)
        => new(
            Id: role.Id,
            Name: role.Name,
            Description: role.Description,
            IsSystem: role.IsSystem,
            Permissions: (permissions
                    ?? role.RolePermissions.Select(rolePermission => rolePermission.Permission.Code))
                .Distinct()
                .OrderBy(code => code)
                .ToArray());

    private static string? Normalize(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
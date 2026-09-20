using Microsoft.EntityFrameworkCore;
using PharmaPOS.Application.Common.Tenancy;
using PharmaPOS.Application.Features.Tenancy;
using PharmaPOS.Domain.Exceptions;
using PharmaPOS.Infrastructure.Identity.Tenancy;

namespace PharmaPOS.Infrastructure.Identity.Tenancy;

/// <summary>
/// Platform-level tenant directory backed by the Finbuckle store context. Deliberately uses the
/// non-filtered <see cref="TenantStoreDbContext"/> so tenant management is never scoped to the
/// caller's own tenant.
/// </summary>
public sealed class PlatformTenantStore : IPlatformTenantStore
{
    private readonly TenantStoreDbContext _dbContext;

    public PlatformTenantStore(TenantStoreDbContext dbContext)
        => _dbContext = dbContext;

    public async Task<IReadOnlyList<TenantDto>> ListAsync(CancellationToken cancellationToken)
    {
        var tenants = await _dbContext.TenantInfo
            .AsNoTracking()
            .OrderBy(tenant => tenant.Identifier)
            .ToListAsync(cancellationToken);

        return tenants.Select(ToDto).ToArray();
    }

    public async Task<TenantDto?> FindByIdentifierAsync(
        string identifier,
        CancellationToken cancellationToken)
    {
        var tenant = await _dbContext.TenantInfo
            .AsNoTracking()
            .FirstOrDefaultAsync(
                candidate => candidate.Identifier == identifier,
                cancellationToken);

        return tenant is null ? null : ToDto(tenant);
    }

    public async Task<TenantDto> CreateAsync(
        TenantDto tenant,
        CancellationToken cancellationToken)
    {
        if (await _dbContext.TenantInfo
            .AnyAsync(candidate => candidate.Identifier == tenant.Identifier, cancellationToken))
        {
            throw new ConflictException(
                "tenant.exists",
                $"A tenant with identifier '{tenant.Identifier}' already exists.");
        }

        var entity = new AppTenantInfo
        {
            Id = tenant.Identifier,
            Identifier = tenant.Identifier,
            Name = tenant.Name,
            Status = tenant.Status,
            PlanCode = tenant.PlanCode,
        };

        _dbContext.TenantInfo.Add(entity);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return ToDto(entity);
    }

    public async Task<TenantDto> UpdateAsync(
        TenantDto tenant,
        CancellationToken cancellationToken)
    {
        // Finbuckle's TenantInfo.Name is init-only, so updates go through ExecuteUpdateAsync
        // (translated to SQL; no CLR setter involved) instead of tracked-entity mutation.
        var affected = await _dbContext.TenantInfo
            .Where(candidate => candidate.Id == tenant.Id)
            .ExecuteUpdateAsync(
                setter => setter
                    .SetProperty(candidate => candidate.Name, tenant.Name)
                    .SetProperty(candidate => candidate.Status, tenant.Status)
                    .SetProperty(candidate => candidate.PlanCode, tenant.PlanCode),
                cancellationToken);

        if (affected == 0)
        {
            throw new NotFoundException(
                "tenant.not_found",
                $"Tenant '{tenant.Identifier}' was not found.");
        }

        var updated = await _dbContext.TenantInfo
            .AsNoTracking()
            .FirstAsync(candidate => candidate.Id == tenant.Id, cancellationToken);

        return ToDto(updated);
    }

    private static TenantDto ToDto(AppTenantInfo tenant)
        => new(
            Id: tenant.Id ?? tenant.Identifier,
            Identifier: tenant.Identifier,
            Name: tenant.Name ?? tenant.Identifier,
            Status: tenant.Status,
            PlanCode: tenant.PlanCode);
}
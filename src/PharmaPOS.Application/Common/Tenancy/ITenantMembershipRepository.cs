using PharmaPOS.Application.Features.Tenancy;

namespace PharmaPOS.Application.Common.Tenancy;

/// <summary>
/// Manages the current tenant's memberships (who belongs to this tenant and which roles they hold).
/// Operations are scoped to the current tenant by the tenant-filtered context.
/// </summary>
public interface ITenantMembershipRepository
{
    Task<IReadOnlyList<MemberDto>> ListAsync(CancellationToken cancellationToken = default);

    Task<MemberDto> SetStatusAsync(
        Guid membershipId,
        string status,
        CancellationToken cancellationToken = default);

    Task<MemberDto> ReplaceRolesAsync(
        Guid membershipId,
        IReadOnlyList<Guid> roleIds,
        CancellationToken cancellationToken = default);
}
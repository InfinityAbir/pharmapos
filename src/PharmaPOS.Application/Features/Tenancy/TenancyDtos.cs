namespace PharmaPOS.Application.Features.Tenancy;

/// <summary>Lifecycle status of a tenant, kept as stable strings.</summary>
public static class TenantStatus
{
    public const string Active = "Active";
    public const string Suspended = "Suspended";
}

public sealed record TenantDto(
    string Id,
    string Identifier,
    string Name,
    string Status,
    string? PlanCode);

public sealed record RoleDto(
    Guid Id,
    string Name,
    string? Description,
    bool IsSystem,
    IReadOnlyList<string> Permissions);

public sealed record MemberDto(
    Guid MembershipId,
    string Status,
    bool IsDefault,
    Guid UserId,
    string Email,
    string DisplayName,
    bool UserActive,
    IReadOnlyList<string> Roles);

public sealed record CurrentMembershipDto(
    string? Subject,
    string Username,
    string Email,
    string TenantIdentifier,
    string TenantName,
    string UserStatus,
    string MembershipStatus,
    bool IsDefaultMembership,
    IReadOnlyList<string> Roles,
    IReadOnlyList<string> Permissions);
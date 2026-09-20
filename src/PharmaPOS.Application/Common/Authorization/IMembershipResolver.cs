namespace PharmaPOS.Application.Common.Authorization;

/// <summary>
/// The access a member of the current tenant is granted by their roles and permissions. Returned by
/// <see cref="IMembershipResolver"/>; <c>null</c> means the identity has no active membership in the
/// current tenant (and therefore no access).
/// </summary>
public sealed record MemberAccess(
    Guid UserId,
    Guid MembershipId,
    string Email,
    bool IsDefault,
    IReadOnlyList<string> Roles,
    IReadOnlyList<string> Permissions);

/// <summary>
/// Resolves a user's tenant membership and granted permissions entirely from the database, scoped
/// by Finbuckle to the current tenant. The identity is matched on Keycloak subject first, then on
/// email/username, so locally seeded demo members and freshly provisioned users behave identically.
/// </summary>
public interface IMembershipResolver
{
    Task<MemberAccess?> ResolveAccessAsync(
        string subjectOrUsername,
        CancellationToken cancellationToken = default);
}
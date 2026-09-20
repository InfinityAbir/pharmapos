using MediatR;
using PharmaPOS.Application.Features.Tenancy;

namespace PharmaPOS.Application.Features.Identity.GetCurrentMembership;

/// <summary>
/// Returns the caller's server-resolved session context: who they are, the active tenant, and the
/// roles and permissions the database grants them for that tenant. Failure to resolve an active
/// membership surfaces as a 403 — there is no fallback tenant.
/// </summary>
/// <param name="Subject">Token <c>sub</c> claim (server-derived).</param>
/// <param name="Username"><c>preferred_username</c> claim, used to match the application user.</param>
/// <param name="TenantIdentifier">Active tenant identifier from the validated token.</param>
public sealed record GetCurrentMembershipQuery(
    string Subject,
    string Username,
    string TenantIdentifier) : IRequest<CurrentMembershipDto>;
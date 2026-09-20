using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using PharmaPOS.Api.Authentication;
using PharmaPOS.Application.Common.Authorization;
using Xunit;

namespace PharmaPOS.UnitTests;

public sealed class PermissionAuthorizationTests
{
    [Fact]
    public async Task Policy_provider_builds_a_permission_policy_on_demand()
    {
        var provider = new PermissionPolicyProvider(Options.Create(new AuthorizationOptions()));

        var policy = await provider.GetPolicyAsync(
            PermissionPolicyProvider.Prefix + Permissions.Platform.AuditRead);

        policy.Should().NotBeNull();
        policy!.Requirements.OfType<PermissionRequirement>().Should().ContainSingle()
            .Which.Permission.Should().Be(Permissions.Platform.AuditRead);
    }

    [Fact]
    public async Task Policy_provider_defers_non_permission_policy_names()
    {
        var provider = new PermissionPolicyProvider(Options.Create(new AuthorizationOptions()));

        (await provider.GetPolicyAsync("some-unregistered-policy")).Should().BeNull();
    }

    [Fact]
    public async Task Handler_grants_when_the_membership_graph_grants_the_permission()
    {
        var requirement = new PermissionRequirement(Permissions.Platform.AuditRead);
        var context = BuildContext(requirement, sub: "subject-1");
        var resolver = new StubMembershipResolver(
            new MemberAccess(
                UserId: Guid.NewGuid(),
                MembershipId: Guid.NewGuid(),
                Email: "admin@pharmapos.local",
                IsDefault: true,
                Roles: ["Platform Admin"],
                Permissions: [Permissions.Platform.AuditRead]));

        await new PermissionAuthorizationHandler(resolver).HandleAsync(context);

        context.HasSucceeded.Should().BeTrue();
    }

    [Fact]
    public async Task Handler_denies_when_the_membership_lacks_the_permission()
    {
        var requirement = new PermissionRequirement(Permissions.Platform.TenantsManage);
        var context = BuildContext(requirement, sub: "subject-1");
        var resolver = new StubMembershipResolver(
            new MemberAccess(
                UserId: Guid.NewGuid(),
                MembershipId: Guid.NewGuid(),
                Email: "owner@demo-pharmacy.local",
                IsDefault: true,
                Roles: ["Owner"],
                Permissions: [Permissions.Platform.AuditRead]));

        await new PermissionAuthorizationHandler(resolver).HandleAsync(context);

        context.HasSucceeded.Should().BeFalse();
    }

    [Fact]
    public async Task Handler_denies_when_there_is_no_active_membership()
    {
        var requirement = new PermissionRequirement(Permissions.Platform.AuditRead);
        var context = BuildContext(requirement, sub: "stranger");

        await new PermissionAuthorizationHandler(new StubMembershipResolver(null)).HandleAsync(context);

        context.HasSucceeded.Should().BeFalse();
    }

    [Fact]
    public async Task Handler_denies_when_the_principal_carries_no_identity()
    {
        var requirement = new PermissionRequirement(Permissions.Platform.AuditRead);
        var identity = new ClaimsIdentity(authenticationType: "Test");
        var context = new AuthorizationHandlerContext(
            [requirement],
            new ClaimsPrincipal(identity),
            resource: null);

        await new PermissionAuthorizationHandler(new StubMembershipResolver(null)).HandleAsync(context);

        context.HasSucceeded.Should().BeFalse();
    }

    private static AuthorizationHandlerContext BuildContext(
        PermissionRequirement requirement,
        string sub)
    {
        var identity = new ClaimsIdentity(
            [new Claim("sub", sub)],
            authenticationType: "Test");

        return new AuthorizationHandlerContext(
            [requirement],
            new ClaimsPrincipal(identity),
            resource: null);
    }

    private sealed class StubMembershipResolver(MemberAccess? access) : IMembershipResolver
    {
        public Task<MemberAccess?> ResolveAccessAsync(
            string subjectOrUsername,
            CancellationToken cancellationToken = default)
            => Task.FromResult(access);
    }
}

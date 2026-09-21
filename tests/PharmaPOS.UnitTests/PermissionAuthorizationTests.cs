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
    public async Task Handler_grants_when_a_realm_role_grants_the_permission()
    {
        var requirement = new PermissionRequirement(Permissions.Platform.AuditRead);
        var context = BuildContext(requirement, RealmRoles.PharmacyOwner);

        await new PermissionAuthorizationHandler().HandleAsync(context);

        context.HasSucceeded.Should().BeTrue();
    }

    [Fact]
    public async Task Handler_denies_when_no_realm_role_grants_the_permission()
    {
        var requirement = new PermissionRequirement(Permissions.Platform.TenantsManage);
        var context = BuildContext(requirement, RealmRoles.Cashier);

        await new PermissionAuthorizationHandler().HandleAsync(context);

        context.HasSucceeded.Should().BeFalse();
    }

    [Fact]
    public async Task Handler_denies_when_the_principal_carries_no_roles()
    {
        var requirement = new PermissionRequirement(Permissions.Platform.AuditRead);
        var identity = new ClaimsIdentity(
            [new Claim("sub", "subject-1")],
            authenticationType: "Test");
        var context = new AuthorizationHandlerContext(
            [requirement],
            new ClaimsPrincipal(identity),
            resource: null);

        await new PermissionAuthorizationHandler().HandleAsync(context);

        context.HasSucceeded.Should().BeFalse();
    }

    [Fact]
    public async Task Handler_denies_unknown_permissions_even_for_admins()
    {
        var requirement = new PermissionRequirement("tenant.nope.not_a_permission");
        var context = BuildContext(requirement, RealmRoles.PlatformAdmin);

        await new PermissionAuthorizationHandler().HandleAsync(context);

        context.HasSucceeded.Should().BeFalse();
    }

    [Fact]
    public void Role_map_grants_every_catalog_permission_to_at_least_one_role()
    {
        foreach (var descriptor in Permissions.All)
        {
            RealmRoles.RolePermissions.Should().ContainKey(descriptor.Code);
        }
    }

    [Fact]
    public void Role_map_only_names_known_realm_roles()
    {
        foreach (var roles in RealmRoles.RolePermissions.Values)
        {
            roles.Should().OnlyContain(role => RealmRoles.All.Contains(role));
        }
    }

    private static AuthorizationHandlerContext BuildContext(
        PermissionRequirement requirement,
        params string[] roles)
    {
        var claims = new List<Claim> { new("sub", "subject-1") };
        claims.AddRange(roles.Select(role => new Claim(ClaimTypes.Role, role)));

        var identity = new ClaimsIdentity(claims, authenticationType: "Test");

        return new AuthorizationHandlerContext(
            [requirement],
            new ClaimsPrincipal(identity),
            resource: null);
    }
}

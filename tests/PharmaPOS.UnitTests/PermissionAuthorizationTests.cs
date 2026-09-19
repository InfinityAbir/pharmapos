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
    public async Task Handler_grants_when_the_validated_token_carries_the_permission()
    {
        var requirement = new PermissionRequirement(Permissions.Platform.AuditRead);
        var context = BuildContext(requirement, Permissions.Platform.AuditRead);

        await new PermissionAuthorizationHandler().HandleAsync(context);

        context.HasSucceeded.Should().BeTrue();
    }

    [Fact]
    public async Task Handler_denies_when_the_permission_is_absent()
    {
        var requirement = new PermissionRequirement(Permissions.Platform.AuditRead);
        var context = BuildContext(requirement, Permissions.Platform.TenantsManage);

        await new PermissionAuthorizationHandler().HandleAsync(context);

        context.HasSucceeded.Should().BeFalse();
    }

    private static AuthorizationHandlerContext BuildContext(
        PermissionRequirement requirement,
        string permission)
    {
        var identity = new ClaimsIdentity(
            [new Claim(PermissionAuthorizationHandler.PermissionClaimType, permission)],
            authenticationType: "Test");

        return new AuthorizationHandlerContext(
            [requirement],
            new ClaimsPrincipal(identity),
            resource: null);
    }
}

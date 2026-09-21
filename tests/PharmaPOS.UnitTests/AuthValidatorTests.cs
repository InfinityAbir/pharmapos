using FluentAssertions;
using PharmaPOS.Application.Common.Authorization;
using PharmaPOS.Application.Features.Auth.Login;
using PharmaPOS.Application.Features.Auth.Refresh;
using PharmaPOS.Application.Features.Auth.Register;
using Xunit;

namespace PharmaPOS.UnitTests;

public sealed class AuthValidatorTests
{
    [Fact]
    public async Task Login_accepts_email_and_password()
    {
        var result = await new LoginCommandValidator()
            .ValidateAsync(new LoginCommand("owner@demo-pharmacy.local", "Pharmacy#2026!"));

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task Login_rejects_blank_credentials()
    {
        var result = await new LoginCommandValidator().ValidateAsync(new LoginCommand("", ""));

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task Refresh_accepts_a_token()
    {
        var result = await new RefreshCommandValidator().ValidateAsync(new RefreshCommand("refresh-token"));

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task Register_accepts_a_well_formed_signup()
    {
        var result = await new RegisterCommandValidator().ValidateAsync(new RegisterCommand(
            "owner@new-pharmacy.local",
            "Pharmacy#2026!",
            "Karim",
            "Uddin",
            "New Pharmacy"));

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task Register_rejects_a_weak_password()
    {
        var result = await new RegisterCommandValidator().ValidateAsync(new RegisterCommand(
            "owner@new-pharmacy.local",
            "short",
            "Karim",
            "Uddin",
            "New Pharmacy"));

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task Register_rejects_a_bad_email()
    {
        var result = await new RegisterCommandValidator().ValidateAsync(new RegisterCommand(
            "not-an-email",
            "Pharmacy#2026!",
            "Karim",
            "Uddin",
            "New Pharmacy"));

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Cashier_role_grants_checkout_but_not_tenant_admin()
    {
        var granted = RealmRoles.PermissionsFor([RealmRoles.Cashier]);

        granted.Should().Contain(Permissions.Tenant.SalesManage);
        granted.Should().NotContain(Permissions.Tenant.MembersManage);
        granted.Should().NotContain(Permissions.Platform.TenantsManage);
    }

    [Fact]
    public void Platform_admin_holds_every_catalog_permission()
    {
        var granted = RealmRoles.PermissionsFor([RealmRoles.PlatformAdmin]);

        granted.Should().BeEquivalentTo(Permissions.All.Select(descriptor => descriptor.Code));
    }
}

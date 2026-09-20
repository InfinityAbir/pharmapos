using FluentAssertions;
using PharmaPOS.Application.Features.Tenancy.CreateRole;
using PharmaPOS.Application.Features.Tenancy.CreateTenant;
using PharmaPOS.Application.Features.Tenancy.DeleteRole;
using PharmaPOS.Application.Features.Tenancy.SetMembershipStatus;
using PharmaPOS.Application.Features.Tenancy.SetTenantStatus;
using Xunit;

namespace PharmaPOS.UnitTests;

public sealed class TenancyValidatorTests
{
    [Fact]
    public async Task CreateTenant_accepts_a_well_formed_identifier()
    {
        var result = await new CreateTenantCommandValidator()
            .ValidateAsync(new CreateTenantCommand("green-life-pharmacy", "Green Life"));

        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("Green_Life")]
    [InlineData("Green Life")]
    [InlineData("-leading-dash")]
    [InlineData("trailing-dash-")]
    [InlineData("UPPERCASE")]
    public async Task CreateTenant_rejects_malformed_identifiers(string identifier)
    {
        var result = await new CreateTenantCommandValidator()
            .ValidateAsync(new CreateTenantCommand(identifier, "Green Life"));

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task CreateTenant_rejects_an_empty_name()
    {
        var result = await new CreateTenantCommandValidator()
            .ValidateAsync(new CreateTenantCommand("green-life", string.Empty));

        result.IsValid.Should().BeFalse();
    }

    [Theory]
    [InlineData("Active")]
    [InlineData("Suspended")]
    public async Task SetTenantStatus_accepts_known_statuses(string status)
    {
        var result = await new SetTenantStatusCommandValidator()
            .ValidateAsync(new SetTenantStatusCommand("green-life", status));

        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("Deleted")]
    [InlineData("active")]
    public async Task SetTenantStatus_rejects_unknown_statuses(string status)
    {
        var result = await new SetTenantStatusCommandValidator()
            .ValidateAsync(new SetTenantStatusCommand("green-life", status));

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task CreateRole_rejects_an_empty_name()
    {
        var result = await new CreateRoleCommandValidator()
            .ValidateAsync(new CreateRoleCommand(string.Empty, null, []));

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task DeleteRole_rejects_an_empty_id()
    {
        var result = await new DeleteRoleCommandValidator()
            .ValidateAsync(new DeleteRoleCommand(Guid.Empty));

        result.IsValid.Should().BeFalse();
    }

    [Theory]
    [InlineData("Active")]
    [InlineData("Inactive")]
    public async Task SetMembershipStatus_accepts_known_statuses(string status)
    {
        var result = await new SetMembershipStatusCommandValidator()
            .ValidateAsync(new SetMembershipStatusCommand(Guid.NewGuid(), status));

        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("Suspended")]
    public async Task SetMembershipStatus_rejects_unknown_statuses(string status)
    {
        var result = await new SetMembershipStatusCommandValidator()
            .ValidateAsync(new SetMembershipStatusCommand(Guid.NewGuid(), status));

        result.IsValid.Should().BeFalse();
    }
}

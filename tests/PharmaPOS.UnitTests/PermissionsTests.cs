using FluentAssertions;
using PharmaPOS.Application.Common.Authorization;
using Xunit;

namespace PharmaPOS.UnitTests;

public sealed class PermissionsTests
{
    [Fact]
    public void Platform_permission_codes_are_stable()
    {
        Permissions.Platform.TenantsManage.Should().Be("platform.tenants.manage");
        Permissions.Platform.UsersManage.Should().Be("platform.users.manage");
        Permissions.Platform.AuditRead.Should().Be("platform.audit.read");
    }

    [Fact]
    public void Permission_codes_follow_the_namespace_convention()
    {
        var codes = new[]
        {
            Permissions.Platform.TenantsManage,
            Permissions.Platform.UsersManage,
            Permissions.Platform.AuditRead,
        };

        codes.Should().OnlyHaveUniqueItems();
        codes.Should().OnlyContain(code =>
            System.Text.RegularExpressions.Regex.IsMatch(code, "^[a-z0-9]+(\\.[a-z0-9]+)+$"));
    }
}

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
    public void Tenant_permission_codes_are_stable()
    {
        Permissions.Tenant.MembersManage.Should().Be("tenant.members.manage");
        Permissions.Tenant.RolesManage.Should().Be("tenant.roles.manage");
        Permissions.Tenant.MedicinesRead.Should().Be("tenant.medicines.read");
        Permissions.Tenant.MedicinesManage.Should().Be("tenant.medicines.manage");
        Permissions.Tenant.PharmacyManage.Should().Be("tenant.pharmacy.manage");
        Permissions.Tenant.InventoryRead.Should().Be("tenant.inventory.read");
        Permissions.Tenant.InventoryManage.Should().Be("tenant.inventory.manage");
        Permissions.Tenant.PurchasingRead.Should().Be("tenant.purchasing.read");
        Permissions.Tenant.PurchasingManage.Should().Be("tenant.purchasing.manage");
        Permissions.Tenant.SalesRead.Should().Be("tenant.sales.read");
        Permissions.Tenant.SalesManage.Should().Be("tenant.sales.manage");
    }

    [Fact]
    public void Catalog_covers_every_declared_code_exactly_once()
    {
        var declared = new[]
        {
            Permissions.Platform.TenantsManage,
            Permissions.Platform.UsersManage,
            Permissions.Platform.AuditRead,
            Permissions.Tenant.MembersManage,
            Permissions.Tenant.RolesManage,
            Permissions.Tenant.MedicinesRead,
            Permissions.Tenant.MedicinesManage,
            Permissions.Tenant.PharmacyManage,
            Permissions.Tenant.InventoryRead,
            Permissions.Tenant.InventoryManage,
            Permissions.Tenant.PurchasingRead,
            Permissions.Tenant.PurchasingManage,
            Permissions.Tenant.SalesRead,
            Permissions.Tenant.SalesManage,
        };

        Permissions.All.Select(descriptor => descriptor.Code)
            .Should().BeEquivalentTo(declared);
    }

    [Fact]
    public void Permission_codes_follow_the_namespace_convention()
    {
        var codes = new[]
        {
            Permissions.Platform.TenantsManage,
            Permissions.Platform.UsersManage,
            Permissions.Platform.AuditRead,
            Permissions.Tenant.MembersManage,
            Permissions.Tenant.RolesManage,
            Permissions.Tenant.MedicinesRead,
            Permissions.Tenant.MedicinesManage,
            Permissions.Tenant.PharmacyManage,
            Permissions.Tenant.InventoryRead,
            Permissions.Tenant.InventoryManage,
            Permissions.Tenant.PurchasingRead,
            Permissions.Tenant.PurchasingManage,
            Permissions.Tenant.SalesRead,
            Permissions.Tenant.SalesManage,
        };

        codes.Should().OnlyHaveUniqueItems();
        codes.Should().OnlyContain(code =>
            System.Text.RegularExpressions.Regex.IsMatch(code, "^[a-z0-9]+(\\.[a-z0-9]+)+$"));
    }
}

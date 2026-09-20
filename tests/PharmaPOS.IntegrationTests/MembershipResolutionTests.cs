using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using PharmaPOS.Application.Common.Authorization;
using PharmaPOS.Application.Features.Tenancy;
using PharmaPOS.Domain.Exceptions;
using PharmaPOS.Domain.Identity;
using PharmaPOS.Infrastructure.Identity;
using PharmaPOS.Infrastructure.Identity.Tenancy;
using PharmaPOS.Infrastructure.Persistence;
using Xunit;

namespace PharmaPOS.IntegrationTests;

/// <summary>
/// Proves the Phase 1 security model end to end against PostgreSQL: authorization is resolved from
/// the database membership graph, memberships do not leak across tenants, and tenant administration
/// obeys its invariants (unique names, system-role immutability, catalog-validated permissions).
/// </summary>
public sealed class MembershipResolutionTests : IClassFixture<PostgresFixture>, IAsyncLifetime
{
    private const string TenantA = "tenant-alpha";
    private const string TenantB = "tenant-beta";

    private readonly PostgresFixture _fixture;

    public MembershipResolutionTests(PostgresFixture fixture) => _fixture = fixture;

    public Task InitializeAsync() => _fixture.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task ResolveAccess_returns_roles_and_permissions_matched_by_email_case_insensitively()
    {
        await using (var db = _fixture.CreateAppContext(TenantA))
        {
            await SeedCatalogAsync(db, Permissions.Platform.AuditRead, Permissions.Tenant.MembersManage);
            var user = await AddUserAsync(db, "owner@demo-pharmacy.local", subject: null);
            var role = await AddRoleAsync(db, "Owner", isSystem: true,
                Permissions.Platform.AuditRead, Permissions.Tenant.MembersManage);
            await AddMembershipAsync(db, user, role);
            await db.SaveChangesAsync();
        }

        await using var reader = _fixture.CreateAppContext(TenantA);
        var access = await new MembershipResolver(reader)
            .ResolveAccessAsync("Owner@Demo-Pharmacy.Local", CancellationToken.None);

        access.Should().NotBeNull();
        access!.Email.Should().Be("owner@demo-pharmacy.local");
        access.Roles.Should().ContainSingle().Which.Should().Be("Owner");
        access.Permissions.Should().BeEquivalentTo(
            Permissions.Platform.AuditRead, Permissions.Tenant.MembersManage);
    }

    [Fact]
    public async Task ResolveAccess_matches_the_keycloak_subject_first()
    {
        await using (var db = _fixture.CreateAppContext(TenantA))
        {
            await SeedCatalogAsync(db, Permissions.Platform.AuditRead);
            var user = await AddUserAsync(db, "admin@pharmapos.local", subject: "keycloak-sub-1");
            var role = await AddRoleAsync(db, "Platform Admin", isSystem: true, Permissions.Platform.AuditRead);
            await AddMembershipAsync(db, user, role);
            await db.SaveChangesAsync();
        }

        await using var reader = _fixture.CreateAppContext(TenantA);
        var access = await new MembershipResolver(reader)
            .ResolveAccessAsync("keycloak-sub-1", CancellationToken.None);

        access.Should().NotBeNull();
        access!.Roles.Should().ContainSingle().Which.Should().Be("Platform Admin");
    }

    [Theory]
    [InlineData("ghost@pharmapos.local")]
    public async Task ResolveAccess_returns_null_for_unknown_identities(string identity)
    {
        await using var reader = _fixture.CreateAppContext(TenantA);

        var access = await new MembershipResolver(reader)
            .ResolveAccessAsync(identity, CancellationToken.None);

        access.Should().BeNull();
    }

    [Fact]
    public async Task ResolveAccess_returns_null_when_the_user_is_deactivated()
    {
        await using (var db = _fixture.CreateAppContext(TenantA))
        {
            await SeedCatalogAsync(db, Permissions.Platform.AuditRead);
            var user = await AddUserAsync(db, "owner@demo-pharmacy.local", subject: null);
            var role = await AddRoleAsync(db, "Owner", isSystem: true, Permissions.Platform.AuditRead);
            await AddMembershipAsync(db, user, role);
            await db.SaveChangesAsync();

            user.IsActive = false;
            await db.SaveChangesAsync();
        }

        await using var reader = _fixture.CreateAppContext(TenantA);
        var access = await new MembershipResolver(reader)
            .ResolveAccessAsync("owner@demo-pharmacy.local", CancellationToken.None);

        access.Should().BeNull();
    }

    [Fact]
    public async Task Deactivating_a_membership_revokes_access()
    {
        Guid membershipId;
        await using (var db = _fixture.CreateAppContext(TenantA))
        {
            await SeedCatalogAsync(db, Permissions.Platform.AuditRead);
            var user = await AddUserAsync(db, "owner@demo-pharmacy.local", subject: null);
            var role = await AddRoleAsync(db, "Owner", isSystem: true, Permissions.Platform.AuditRead);
            membershipId = (await AddMembershipAsync(db, user, role)).Id;
            await db.SaveChangesAsync();
        }

        await using (var db = _fixture.CreateAppContext(TenantA))
        {
            await new TenantMembershipRepository(db)
                .SetStatusAsync(membershipId, MembershipStatus.Inactive, CancellationToken.None);
        }

        await using var reader = _fixture.CreateAppContext(TenantA);
        var access = await new MembershipResolver(reader)
            .ResolveAccessAsync("owner@demo-pharmacy.local", CancellationToken.None);

        access.Should().BeNull();
    }

    [Fact]
    public async Task Memberships_do_not_leak_across_tenants()
    {
        await using (var alpha = _fixture.CreateAppContext(TenantA))
        {
            await SeedCatalogAsync(alpha, Permissions.Platform.AuditRead);
            var user = await AddUserAsync(alpha, "owner@demo-pharmacy.local", subject: null);
            var role = await AddRoleAsync(alpha, "Owner", isSystem: true, Permissions.Platform.AuditRead);
            await AddMembershipAsync(alpha, user, role);
            await alpha.SaveChangesAsync();
        }

        await using var beta = _fixture.CreateAppContext(TenantB);
        var access = await new MembershipResolver(beta)
            .ResolveAccessAsync("owner@demo-pharmacy.local", CancellationToken.None);

        access.Should().BeNull();
    }

    [Fact]
    public async Task Roles_from_another_tenant_cannot_be_assigned()
    {
        Guid membershipBId;
        Guid roleAId;

        await using (var alpha = _fixture.CreateAppContext(TenantA))
        {
            await SeedCatalogAsync(alpha, Permissions.Platform.AuditRead);
            var roleA = await AddRoleAsync(alpha, "Owner", isSystem: true, Permissions.Platform.AuditRead);
            roleAId = roleA.Id;
            await alpha.SaveChangesAsync();
        }

        await using (var beta = _fixture.CreateAppContext(TenantB))
        {
            await SeedCatalogAsync(beta, Permissions.Platform.AuditRead);
            var userB = await AddUserAsync(beta, "manager@beta.local", subject: null);
            var roleB = await AddRoleAsync(beta, "Manager", isSystem: false);
            membershipBId = (await AddMembershipAsync(beta, userB, roleB)).Id;
            await beta.SaveChangesAsync();
        }

        await using var betaReader = _fixture.CreateAppContext(TenantB);
        var act = async () => await new TenantMembershipRepository(betaReader)
            .ReplaceRolesAsync(membershipBId, [roleAId], CancellationToken.None);

        var exception = await act.Should().ThrowAsync<NotFoundException>();
        exception.Which.Code.Should().Be("role.not_found");
    }

    [Fact]
    public async Task Same_role_name_may_exist_in_two_tenants()
    {
        await using (var alpha = _fixture.CreateAppContext(TenantA))
        {
            await SeedCatalogAsync(alpha);
            await new TenantRoleRepository(alpha)
                .CreateAsync("Cashier", null, [], CancellationToken.None);
        }

        await using var beta = _fixture.CreateAppContext(TenantB);
        await SeedCatalogAsync(beta);

        var act = async () => await new TenantRoleRepository(beta)
            .CreateAsync("Cashier", null, [], CancellationToken.None);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task ReplaceRoles_happy_path_swaps_assignments()
    {
        Guid membershipId;
        Guid managerRoleId;

        await using (var db = _fixture.CreateAppContext(TenantA))
        {
            await SeedCatalogAsync(db, Permissions.Platform.AuditRead);
            var user = await AddUserAsync(db, "owner@demo-pharmacy.local", subject: null);
            var ownerRole = await AddRoleAsync(db, "Owner", isSystem: true, Permissions.Platform.AuditRead);
            var managerRole = await AddRoleAsync(db, "Manager", isSystem: false);
            managerRoleId = managerRole.Id;
            membershipId = (await AddMembershipAsync(db, user, ownerRole)).Id;
            await db.SaveChangesAsync();
        }

        await using var writer = _fixture.CreateAppContext(TenantA);
        var updated = await new TenantMembershipRepository(writer)
            .ReplaceRolesAsync(membershipId, [managerRoleId], CancellationToken.None);

        updated.Roles.Should().ContainSingle().Which.Should().Be("Manager");

        await using var reader = _fixture.CreateAppContext(TenantA);
        var access = await new MembershipResolver(reader)
            .ResolveAccessAsync("owner@demo-pharmacy.local", CancellationToken.None);

        access.Should().NotBeNull();
        access!.Roles.Should().ContainSingle().Which.Should().Be("Manager");
        access.Permissions.Should().BeEmpty();
    }

    [Fact]
    public async Task UpdateRole_happy_path_renames_and_swaps_permissions()
    {
        Guid roleId;

        await using (var db = _fixture.CreateAppContext(TenantA))
        {
            await SeedCatalogAsync(db, Permissions.Platform.AuditRead, Permissions.Tenant.MembersManage);
            roleId = (await new TenantRoleRepository(db)
                .CreateAsync("Cashier", null, [Permissions.Platform.AuditRead], CancellationToken.None)).Id;
        }

        await using var writer = _fixture.CreateAppContext(TenantA);
        var updated = await new TenantRoleRepository(writer)
            .UpdateAsync(
                roleId,
                "Senior Cashier",
                "Handles refunds.",
                [Permissions.Tenant.MembersManage],
                CancellationToken.None);

        updated.Name.Should().Be("Senior Cashier");
        updated.Permissions.Should().ContainSingle().Which.Should().Be(Permissions.Tenant.MembersManage);

        await using var reader = _fixture.CreateAppContext(TenantA);
        var listed = await new TenantRoleRepository(reader).ListAsync(CancellationToken.None);
        listed.Should().ContainSingle().Which.Should().BeEquivalentTo(updated);
    }

    [Fact]
    public async Task Duplicate_role_names_are_rejected_within_a_tenant()
    {
        await using var db = _fixture.CreateAppContext(TenantA);
        await SeedCatalogAsync(db);
        var repository = new TenantRoleRepository(db);
        await repository.CreateAsync("Cashier", null, [], CancellationToken.None);

        var act = async () => await repository
            .CreateAsync("Cashier", null, [], CancellationToken.None);

        var exception = await act.Should().ThrowAsync<ConflictException>();
        exception.Which.Code.Should().Be("role.duplicate");
    }

    [Fact]
    public async Task System_roles_cannot_be_deleted_or_renamed()
    {
        Guid systemId;
        await using (var db = _fixture.CreateAppContext(TenantA))
        {
            await SeedCatalogAsync(db);
            systemId = (await new TenantRoleRepository(db)
                .CreateAsync("Cashier", null, [], CancellationToken.None)).Id;

            // Promote to system to exercise the guard (seeded roles are created this way).
            await db.Roles
                .Where(role => role.Id == systemId)
                .ExecuteUpdateAsync(
                    setter => setter.SetProperty(role => role.IsSystem, true),
                    CancellationToken.None);
        }

        // Fresh contexts, as separate requests would use — no stale tracked state.
        await using var reader = _fixture.CreateAppContext(TenantA);
        var repository = new TenantRoleRepository(reader);

        var delete = async () => await repository.DeleteAsync(systemId, CancellationToken.None);
        (await delete.Should().ThrowAsync<ForbiddenException>()).Which.Code.Should().Be("role.system_immutable");

        var rename = async () => await repository.UpdateAsync(
            systemId, "Senior Cashier", null, [], CancellationToken.None);
        (await rename.Should().ThrowAsync<ForbiddenException>()).Which.Code.Should().Be("role.system_immutable");
    }

    [Fact]
    public async Task Unknown_permission_codes_are_rejected()
    {
        await using var db = _fixture.CreateAppContext(TenantA);
        await SeedCatalogAsync(db);

        var act = async () => await new TenantRoleRepository(db)
            .CreateAsync("Cashier", null, ["pos.refund"], CancellationToken.None);

        var exception = await act.Should().ThrowAsync<BusinessRuleException>();
        exception.Which.Code.Should().Be("permission.unknown");
    }

    [Fact]
    public async Task Platform_tenant_store_round_trip_with_conflict_and_not_found()
    {
        await using var store = _fixture.CreateStoreContext();
        var directory = new PlatformTenantStore(store);

        var created = await directory.CreateAsync(
            new TenantDto("green-life", "green-life", "Green Life", "Active", null),
            CancellationToken.None);
        created.Name.Should().Be("Green Life");

        (await directory.ListAsync(CancellationToken.None))
            .Should().ContainSingle().Which.Identifier.Should().Be("green-life");

        var duplicate = async () => await directory.CreateAsync(
            new TenantDto("green-life", "green-life", "Green Life", "Active", null),
            CancellationToken.None);
        (await duplicate.Should().ThrowAsync<ConflictException>()).Which.Code.Should().Be("tenant.exists");

        var suspended = await directory.UpdateAsync(
            created with { Status = "Suspended" },
            CancellationToken.None);
        suspended.Status.Should().Be("Suspended");

        var missing = async () => await directory.UpdateAsync(
            new TenantDto("nope", "nope", "Nope", "Active", null),
            CancellationToken.None);
        (await missing.Should().ThrowAsync<NotFoundException>()).Which.Code.Should().Be("tenant.not_found");
    }

    private static async Task SeedCatalogAsync(AppDbContext db, params string[] codes)
    {
        // The catalog is platform-wide (shared across tenants), so seeding must be idempotent:
        // several tenant contexts in one test share the same table.
        var wanted = codes.Length == 0
            ? Permissions.All.Select(descriptor => descriptor.Code).ToArray()
            : codes;

        var existing = await db.Permissions
            .Where(permission => wanted.Contains(permission.Code))
            .Select(permission => permission.Code)
            .ToListAsync();

        foreach (var code in wanted.Distinct().Except(existing))
        {
            var descriptor = Permissions.All.Single(candidate => candidate.Code == code);
            db.Permissions.Add(new Permission
            {
                Code = descriptor.Code,
                Module = descriptor.Module,
                Description = descriptor.Description,
            });
        }

        await db.SaveChangesAsync();
    }

    private static async Task<ApplicationUser> AddUserAsync(
        AppDbContext db,
        string email,
        string? subject)
    {
        var user = new ApplicationUser
        {
            KeycloakSubject = subject,
            Email = email.ToLowerInvariant(),
            DisplayName = email,
            IsActive = true,
        };

        db.ApplicationUsers.Add(user);
        await db.SaveChangesAsync();

        return user;
    }

    private static async Task<Role> AddRoleAsync(
        AppDbContext db,
        string name,
        bool isSystem,
        params string[] permissionCodes)
    {
        var tenantId = TenantOf(db);

        var role = new Role
        {
            TenantId = tenantId,
            Name = name,
            Description = null,
            IsSystem = isSystem,
        };

        foreach (var code in permissionCodes.Distinct())
        {
            var permission = await db.Permissions.SingleAsync(candidate => candidate.Code == code);
            role.RolePermissions.Add(new RolePermission
            {
                TenantId = tenantId,
                PermissionId = permission.Id,
            });
        }

        db.Roles.Add(role);
        await db.SaveChangesAsync();

        return role;
    }

    private static async Task<TenantMembership> AddMembershipAsync(
        AppDbContext db,
        ApplicationUser user,
        Role role)
    {
        var tenantId = TenantOf(db);

        var membership = new TenantMembership
        {
            TenantId = tenantId,
            ApplicationUserId = user.Id,
            Status = MembershipStatus.Active,
            IsDefault = true,
        };

        membership.MembershipRoles.Add(new MembershipRole
        {
            TenantId = tenantId,
            RoleId = role.Id,
        });
        db.TenantMemberships.Add(membership);

        return membership;
    }

    private static string TenantOf(AppDbContext db)
        => db.TenantInfo?.Id
        ?? throw new InvalidOperationException("Test context has no tenant bound.");
}

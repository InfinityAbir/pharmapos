using FluentAssertions;
using PharmaPOS.Application.Common.Authorization;
using PharmaPOS.Application.Common.Tenancy;
using PharmaPOS.Application.Features.Identity.GetCurrentMembership;
using PharmaPOS.Application.Features.Tenancy;
using PharmaPOS.Domain.Exceptions;
using Xunit;

namespace PharmaPOS.UnitTests;

public sealed class GetCurrentMembershipHandlerTests
{
    [Fact]
    public async Task Returns_server_resolved_context_with_tenant_name()
    {
        var access = new MemberAccess(
            UserId: Guid.NewGuid(),
            MembershipId: Guid.NewGuid(),
            Email: "owner@demo-pharmacy.local",
            IsDefault: true,
            Roles: ["Owner"],
            Permissions: ["platform.audit.read", "tenant.members.manage"]);

        var resolver = new CapturingResolver(access);
        var store = new StubTenantStore(
            new TenantDto("demo-pharmacy", "demo-pharmacy", "Demo Pharmacy", "Active", null));

        var result = await new GetCurrentMembershipQueryHandler(resolver, store).Handle(
            new GetCurrentMembershipQuery("subject-1", "owner@demo-pharmacy.local", "demo-pharmacy"),
            CancellationToken.None);

        result.Email.Should().Be("owner@demo-pharmacy.local");
        result.TenantName.Should().Be("Demo Pharmacy");
        result.Roles.Should().ContainSingle().Which.Should().Be("Owner");
        result.Permissions.Should().HaveCount(2);
        result.IsDefaultMembership.Should().BeTrue();

        // Username (preferred_username) is preferred over the opaque subject for matching.
        resolver.LastIdentity.Should().Be("owner@demo-pharmacy.local");
    }

    [Fact]
    public async Task Falls_back_to_subject_when_username_is_missing()
    {
        var access = new MemberAccess(
            Guid.NewGuid(), Guid.NewGuid(), "admin@pharmapos.local", true, ["Platform Admin"], []);

        var resolver = new CapturingResolver(access);
        var store = new StubTenantStore(null);

        await new GetCurrentMembershipQueryHandler(resolver, store).Handle(
            new GetCurrentMembershipQuery("subject-9", string.Empty, "demo-pharmacy"),
            CancellationToken.None);

        resolver.LastIdentity.Should().Be("subject-9");
    }

    [Fact]
    public async Task Throws_forbidden_when_no_active_membership_exists()
    {
        var handler = new GetCurrentMembershipQueryHandler(
            new CapturingResolver(null),
            new StubTenantStore(null));

        var act = async () => await handler.Handle(
            new GetCurrentMembershipQuery("stranger", "stranger", "demo-pharmacy"),
            CancellationToken.None);

        var exception = await act.Should().ThrowAsync<ForbiddenException>();
        exception.Which.Code.Should().Be("membership.not_found");
    }

    private sealed class CapturingResolver(MemberAccess? access) : IMembershipResolver
    {
        public string? LastIdentity { get; private set; }

        public Task<MemberAccess?> ResolveAccessAsync(
            string subjectOrUsername,
            CancellationToken cancellationToken = default)
        {
            LastIdentity = subjectOrUsername;
            return Task.FromResult(access);
        }
    }

    private sealed class StubTenantStore(TenantDto? tenant) : IPlatformTenantStore
    {
        public Task<IReadOnlyList<TenantDto>> ListAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<TenantDto>>(
                tenant is null ? [] : [tenant]);

        public Task<TenantDto?> FindByIdentifierAsync(
            string identifier,
            CancellationToken cancellationToken = default)
            => Task.FromResult(tenant);

        public Task<TenantDto> CreateAsync(TenantDto tenant, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<TenantDto> UpdateAsync(TenantDto tenant, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }
}

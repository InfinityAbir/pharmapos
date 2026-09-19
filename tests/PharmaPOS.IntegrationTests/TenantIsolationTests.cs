using Finbuckle.MultiTenant.Abstractions;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using PharmaPOS.Domain.Auditing;
using PharmaPOS.Infrastructure.Identity.Tenancy;
using Xunit;

namespace PharmaPOS.IntegrationTests;

/// <summary>
/// Proves the security-critical Phase 0 guarantee: shared-database multi-tenancy is enforced at the
/// persistence layer. One tenant can never read or write another tenant's rows, and a mismatched
/// write is rejected rather than silently stored.
/// </summary>
public sealed class TenantIsolationTests : IClassFixture<PostgresFixture>, IAsyncLifetime
{
    private const string TenantA = "tenant-alpha";
    private const string TenantB = "tenant-beta";

    private readonly PostgresFixture _fixture;

    public TenantIsolationTests(PostgresFixture fixture) => _fixture = fixture;

    public Task InitializeAsync() => _fixture.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Migrations_and_tenant_store_round_trip()
    {
        await using var store = _fixture.CreateStoreContext();

        store.TenantInfo.Add(new AppTenantInfo
        {
            Id = TenantA,
            Identifier = TenantA,
            Name = "Alpha Pharmacy",
        });
        await store.SaveChangesAsync();

        var loaded = await store.TenantInfo.SingleAsync(tenant => tenant.Identifier == TenantA);
        loaded.Name.Should().Be("Alpha Pharmacy");
    }

    [Fact]
    public async Task Reads_are_filtered_to_the_current_tenant()
    {
        await using (var alpha = _fixture.CreateAppContext(TenantA))
        {
            alpha.AuditLogs.Add(NewLog("sale.completed"));
            alpha.AuditLogs.Add(NewLog("sale.refunded"));
            await alpha.SaveChangesAsync();
        }

        await using (var beta = _fixture.CreateAppContext(TenantB))
        {
            beta.AuditLogs.Add(NewLog("stock.received"));
            await beta.SaveChangesAsync();
        }

        await using (var alpha = _fixture.CreateAppContext(TenantA))
        {
            var logs = await alpha.AuditLogs.ToListAsync();

            logs.Should().HaveCount(2);
            logs.Should().OnlyContain(log => log.TenantId == TenantA);
            logs.Select(log => log.Action).Should().NotContain("stock.received");
        }

        await using (var beta = _fixture.CreateAppContext(TenantB))
        {
            var logs = await beta.AuditLogs.ToListAsync();

            logs.Should().ContainSingle().Which.Action.Should().Be("stock.received");
        }
    }

    [Fact]
    public async Task Tenant_id_is_stamped_when_the_caller_omits_it()
    {
        await using (var alpha = _fixture.CreateAppContext(TenantA))
        {
            alpha.AuditLogs.Add(NewLog("sale.completed")); // TenantId intentionally left unset
            await alpha.SaveChangesAsync();
        }

        await using var reader = _fixture.CreateAppContext(TenantA);
        (await reader.AuditLogs.SingleAsync()).TenantId.Should().Be(TenantA);
    }

    [Fact]
    public async Task Caller_supplied_foreign_tenant_id_is_rejected()
    {
        await using var alpha = _fixture.CreateAppContext(TenantA);
        var foreign = NewLog("malicious.write");
        foreign.TenantId = TenantB; // attempt to write into another tenant

        alpha.AuditLogs.Add(foreign);

        var act = async () => await alpha.SaveChangesAsync();

        await act.Should().ThrowAsync<MultiTenantException>();
    }

    private static AuditLog NewLog(string action) => new()
    {
        Action = action,
        ActorSubject = "test-subject",
        OccurredAtUtc = DateTimeOffset.UtcNow,
    };
}

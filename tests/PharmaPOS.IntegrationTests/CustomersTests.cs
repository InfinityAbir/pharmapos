using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using PharmaPOS.Application.Common.Customers;
using PharmaPOS.Domain.Exceptions;
using PharmaPOS.Infrastructure.Customers;
using Xunit;

namespace PharmaPOS.IntegrationTests;

/// <summary>
/// Proves the Phase 6 customer model against PostgreSQL: CRUD, phone uniqueness per tenant,
/// delete guard while sales reference the customer, and cross-tenant invisibility.
/// </summary>
public sealed class CustomersTests : IClassFixture<PostgresFixture>, IAsyncLifetime
{
    private const string TenantA = "tenant-alpha";
    private const string TenantB = "tenant-beta";

    private readonly PostgresFixture _fixture;

    public CustomersTests(PostgresFixture fixture) => _fixture = fixture;

    public Task InitializeAsync() => _fixture.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Creates_lists_and_updates_a_customer()
    {
        await using var db = _fixture.CreateAppContext(TenantA);
        var repo = new CustomerRepository(db);

        var created = await repo.CreateAsync(
            new CustomerInput("Rahim Uddin", "01712345678", null, "Dhaka", null),
            CancellationToken.None);

        created.Name.Should().Be("Rahim Uddin");
        created.IsActive.Should().BeTrue();

        var list = await repo.ListAsync("rahim", null, 0, 25, CancellationToken.None);
        list.Should().ContainSingle().Which.Id.Should().Be(created.Id);

        var updated = await repo.UpdateAsync(
            created.Id, new CustomerInput("Rahim U.", "01712345678", null, "Mirpur", "Regular"),
            CancellationToken.None);
        updated.Address.Should().Be("Mirpur");

        var withHistory = await repo.FindWithHistoryAsync(created.Id, CancellationToken.None);
        withHistory.Should().NotBeNull();
        withHistory!.RecentSales.Should().BeEmpty();
    }

    [Fact]
    public async Task Phone_must_be_unique_per_tenant()
    {
        await using var db = _fixture.CreateAppContext(TenantA);
        var repo = new CustomerRepository(db);

        await repo.CreateAsync(
            new CustomerInput("A", "01700000001", null, null, null), CancellationToken.None);

        var dup = async () => await repo.CreateAsync(
            new CustomerInput("B", "01700000001", null, null, null), CancellationToken.None);

        (await dup.Should().ThrowAsync<ConflictException>())
            .Which.Code.Should().Be("customer.phone_exists");
    }

    [Fact]
    public async Task Delete_is_refused_while_sales_reference_the_customer()
    {
        await using var db = _fixture.CreateAppContext(TenantA);
        var repo = new CustomerRepository(db);

        var customer = await repo.CreateAsync(
            new CustomerInput("Linked", "01700000002", null, null, null), CancellationToken.None);

        db.Sales.Add(new PharmaPOS.Domain.Sales.Sale
        {
            TenantId = TenantA,
            InvoiceNumber = "INV-LINKED",
            OccurredAtUtc = DateTimeOffset.UtcNow,
            Subtotal = 10,
            DiscountAmount = 0,
            TotalAmount = 10,
            PaidAmount = 10,
            CustomerId = customer.Id,
            CustomerName = customer.Name,
        });
        await db.SaveChangesAsync();

        var delete = async () => await repo.DeleteAsync(customer.Id, CancellationToken.None);
        (await delete.Should().ThrowAsync<BusinessRuleException>())
            .Which.Code.Should().Be("customer.has_sales");

        var deactivated = await repo.SetActiveAsync(customer.Id, false, CancellationToken.None);
        deactivated.IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task Customers_do_not_leak_across_tenants()
    {
        Guid id;
        await using (var alpha = _fixture.CreateAppContext(TenantA))
        {
            id = (await new CustomerRepository(alpha).CreateAsync(
                new CustomerInput("Alpha Only", "01700000003", null, null, null),
                CancellationToken.None)).Id;
        }

        await using var beta = _fixture.CreateAppContext(TenantB);
        (await new CustomerRepository(beta).FindAsync(id, CancellationToken.None)).Should().BeNull();
        (await new CustomerRepository(beta).ListAsync(null, null, 0, 25, CancellationToken.None))
            .Should().BeEmpty();
    }
}

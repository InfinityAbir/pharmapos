using FluentAssertions;
using PharmaPOS.Application.Common.Customers;
using PharmaPOS.Application.Features.Customers.SaveCustomer;
using Xunit;

namespace PharmaPOS.UnitTests;

public sealed class CustomerValidatorTests
{
    private static CustomerInput Valid() => new(
        Name: "Rahim Uddin",
        Phone: "01712345678",
        Email: "rahim@example.com",
        Address: "Dhaka",
        Notes: null);

    [Fact]
    public async Task Accepts_a_well_formed_customer()
    {
        var result = await new CustomerInputValidator().ValidateAsync(Valid());
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task Rejects_empty_name_and_bad_email()
    {
        var noName = await new CustomerInputValidator().ValidateAsync(Valid() with { Name = "" });
        noName.IsValid.Should().BeFalse();

        var badEmail = await new CustomerInputValidator().ValidateAsync(Valid() with { Email = "not-an-email" });
        badEmail.IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task Allows_missing_optional_fields_for_quick_creation()
    {
        var minimal = await new CustomerInputValidator().ValidateAsync(new CustomerInput("Walk-in", null, null, null, null));
        minimal.IsValid.Should().BeTrue();
    }
}

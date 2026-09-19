using FluentAssertions;
using PharmaPOS.Domain.Exceptions;
using Xunit;

namespace PharmaPOS.UnitTests;

public sealed class DomainExceptionTests
{
    [Fact]
    public void NotFound_For_BuildsStableCodeAndCategory()
    {
        var exception = NotFoundException.For("medicine", 42);

        exception.Code.Should().Be("medicine.not_found");
        exception.Category.Should().Be(ErrorCategory.NotFound);
        exception.Message.Should().Contain("42");
    }

    [Fact]
    public void Validation_CarriesFieldLevelErrors()
    {
        var exception = new ValidationException("Name", "Name is required.");

        exception.Category.Should().Be(ErrorCategory.Validation);
        exception.Errors.Should().ContainKey("Name")
            .WhoseValue.Should().Contain("Name is required.");
    }

    [Fact]
    public void Validation_message_is_generic_so_details_do_not_leak_by_accident()
    {
        var exception = new ValidationException("Name", "Name is required.");

        exception.Message.Should().Be("One or more validation errors occurred.");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void NotFound_Rejects_BlankCode(string code)
    {
        var act = () => new NotFoundException(code, "message");

        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void NotFound_Rejects_BlankMessage(string message)
    {
        var act = () => new NotFoundException("medicine.not_found", message);

        act.Should().Throw<ArgumentException>();
    }
}

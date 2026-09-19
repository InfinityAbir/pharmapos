using FluentAssertions;
using FluentValidation;
using MediatR;
using PharmaPOS.Application.Common.Behaviors;
using DomainValidationException = PharmaPOS.Domain.Exceptions.ValidationException;
using Xunit;

namespace PharmaPOS.UnitTests;

public sealed class ValidationBehaviorTests
{
    private sealed record Sample(string Name) : IRequest<string>;

    private sealed class SampleValidator : AbstractValidator<Sample>
    {
        public SampleValidator() => RuleFor(sample => sample.Name).NotEmpty();
    }

    private static readonly RequestHandlerDelegate<string> Next =
        _ => Task.FromResult("handled");

    [Fact]
    public async Task Invalid_request_throws_validation_exception_with_grouped_errors()
    {
        var behavior = new ValidationBehavior<Sample, string>([new SampleValidator()]);

        var act = () => behavior.Handle(new Sample(string.Empty), Next, CancellationToken.None);

        var exception = await act.Should().ThrowAsync<DomainValidationException>();
        exception.Which.Errors.Should().ContainKey(nameof(Sample.Name));
    }

    [Fact]
    public async Task Valid_request_reaches_the_handler()
    {
        var behavior = new ValidationBehavior<Sample, string>([new SampleValidator()]);

        var result = await behavior.Handle(new Sample("Amoxicillin"), Next, CancellationToken.None);

        result.Should().Be("handled");
    }

    [Fact]
    public async Task No_validators_short_circuits_to_the_handler()
    {
        var behavior = new ValidationBehavior<Sample, string>(Array.Empty<IValidator<Sample>>());

        var result = await behavior.Handle(new Sample(string.Empty), Next, CancellationToken.None);

        result.Should().Be("handled");
    }
}

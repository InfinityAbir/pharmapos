namespace PharmaPOS.Domain.Exceptions;

/// <summary>
/// One or more business invariants were violated. Carries field-level messages so the API can
/// return an RFC 7807 <c>ValidationProblemDetails</c>.
/// </summary>
public sealed class ValidationException : DomainException
{
    public ValidationException(IReadOnlyDictionary<string, string[]> errors)
        : base("validation_failed", "One or more validation errors occurred.", ErrorCategory.Validation)
    {
        Errors = errors;
    }

    public ValidationException(string propertyName, string message)
        : this(new Dictionary<string, string[]> { [propertyName] = [message] })
    {
    }

    public IReadOnlyDictionary<string, string[]> Errors { get; }
}

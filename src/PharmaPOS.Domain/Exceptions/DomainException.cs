namespace PharmaPOS.Domain.Exceptions;

/// <summary>
/// Classifies a known application error so that transport layers (HTTP) can map it
/// to a protocol response without the domain knowing about HTTP.
/// </summary>
public enum ErrorCategory
{
    Validation = 0,
    NotFound = 1,
    Conflict = 2,
    Forbidden = 3,
    BusinessRule = 4,
}

/// <summary>
/// Base class for expected, business-meaningful failures. Anything deriving from this
/// is safe to surface to the client with a stable <see cref="Code"/>; unexpected
/// exceptions are handled separately and never leak internals.
/// </summary>
public abstract class DomainException : Exception
{
    protected DomainException(
        string code,
        string message,
        ErrorCategory category,
        Exception? innerException = null)
        : base(message, innerException)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);

        Code = code;
        Category = category;
    }

    /// <summary>Stable machine-readable error code (e.g. <c>medicine.not_found</c>).</summary>
    public string Code { get; }

    public ErrorCategory Category { get; }
}

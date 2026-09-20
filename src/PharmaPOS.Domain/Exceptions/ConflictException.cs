namespace PharmaPOS.Domain.Exceptions;

/// <summary>The request conflicts with the current state of a resource.</summary>
public sealed class ConflictException : DomainException
{
    public ConflictException(string code, string message)
        : base(code, message, ErrorCategory.Conflict)
    {
    }

    public ConflictException(string code, string message, Exception? innerException)
        : base(code, message, ErrorCategory.Conflict, innerException)
    {
    }
}

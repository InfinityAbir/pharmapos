namespace PharmaPOS.Domain.Exceptions;

/// <summary>
/// The caller is authenticated but not permitted to perform the operation. Also used when a
/// tenant selection is not backed by a real membership (never fall back to another tenant).
/// </summary>
public sealed class ForbiddenException : DomainException
{
    public ForbiddenException(string code, string message)
        : base(code, message, ErrorCategory.Forbidden)
    {
    }
}

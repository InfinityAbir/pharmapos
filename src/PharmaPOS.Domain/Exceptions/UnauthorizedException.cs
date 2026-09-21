namespace PharmaPOS.Domain.Exceptions;

/// <summary>The caller failed to authenticate (bad credentials, expired refresh token).</summary>
public sealed class UnauthorizedException : DomainException
{
    public UnauthorizedException(string code, string message)
        : base(code, message, ErrorCategory.Unauthorized)
    {
    }
}

namespace PharmaPOS.Domain.Exceptions;

/// <summary>A domain rule was broken that is not a field-level validation error.</summary>
public sealed class BusinessRuleException : DomainException
{
    public BusinessRuleException(string code, string message)
        : base(code, message, ErrorCategory.BusinessRule)
    {
    }
}

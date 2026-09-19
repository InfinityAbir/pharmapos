namespace PharmaPOS.Domain.Exceptions;

/// <summary>A requested resource does not exist or is not visible to the current tenant.</summary>
public sealed class NotFoundException : DomainException
{
    public NotFoundException(string code, string message)
        : base(code, message, ErrorCategory.NotFound)
    {
    }

    public static NotFoundException For(string entity, object key)
        => new($"{entity}.not_found", $"{entity} '{key}' was not found.");
}

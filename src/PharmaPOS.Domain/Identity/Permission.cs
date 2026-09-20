namespace PharmaPOS.Domain.Identity;

/// <summary>
/// A platform-wide permission code (e.g. <c>tenant.roles.manage</c>). This is the catalog used by
/// roles; codes are stable strings and never invented per tenant.
/// </summary>
public sealed class Permission
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Code { get; set; } = default!;

    public string Module { get; set; } = default!;

    public string? Description { get; set; }
}
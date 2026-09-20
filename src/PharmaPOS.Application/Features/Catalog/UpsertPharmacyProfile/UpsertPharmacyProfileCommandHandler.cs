using MediatR;
using PharmaPOS.Application.Common.Auditing;
using PharmaPOS.Application.Common.Catalog;

namespace PharmaPOS.Application.Features.Catalog.UpsertPharmacyProfile;

public sealed class UpsertPharmacyProfileCommandHandler
    : IRequestHandler<UpsertPharmacyProfileCommand, PharmacyProfileDto>
{
    private readonly IPharmacyProfileStore _profiles;
    private readonly IAuditLogger _auditLogger;

    public UpsertPharmacyProfileCommandHandler(
        IPharmacyProfileStore profiles,
        IAuditLogger auditLogger)
    {
        _profiles = profiles;
        _auditLogger = auditLogger;
    }

    public async Task<PharmacyProfileDto> Handle(
        UpsertPharmacyProfileCommand request,
        CancellationToken cancellationToken)
    {
        var profile = await _profiles.UpsertAsync(request.Profile, cancellationToken);

        await _auditLogger.LogAsync(
            new AuditEntry(
                Action: "pharmacy-profile.updated",
                EntityType: "PharmacyProfile",
                EntityId: profile.PharmacyName,
                Data: new Dictionary<string, string?> { ["name"] = profile.PharmacyName }),
            cancellationToken);

        return profile;
    }
}
using MediatR;
using PharmaPOS.Application.Common.Catalog;
using PharmaPOS.Domain.Exceptions;

namespace PharmaPOS.Application.Features.Catalog.GetPharmacyProfile;

public sealed class GetPharmacyProfileQueryHandler : IRequestHandler<GetPharmacyProfileQuery, PharmacyProfileDto>
{
    private readonly IPharmacyProfileStore _profiles;

    public GetPharmacyProfileQueryHandler(IPharmacyProfileStore profiles) => _profiles = profiles;

    public async Task<PharmacyProfileDto> Handle(
        GetPharmacyProfileQuery request,
        CancellationToken cancellationToken)
    {
        var profile = await _profiles.GetAsync(cancellationToken);
        if (profile is null)
        {
            throw new NotFoundException(
                "pharmacy-profile.not_found",
                "No pharmacy profile has been configured for this tenant yet.");
        }

        return profile;
    }
}
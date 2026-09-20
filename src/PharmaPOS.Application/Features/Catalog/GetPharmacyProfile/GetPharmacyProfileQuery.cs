using MediatR;

namespace PharmaPOS.Application.Features.Catalog.GetPharmacyProfile;

public sealed record GetPharmacyProfileQuery : IRequest<PharmacyProfileDto>;
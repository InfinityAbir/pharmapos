using MediatR;

namespace PharmaPOS.Application.Features.Catalog.UpsertPharmacyProfile;

public sealed record UpsertPharmacyProfileCommand(PharmacyProfileDto Profile)
    : IRequest<PharmacyProfileDto>;
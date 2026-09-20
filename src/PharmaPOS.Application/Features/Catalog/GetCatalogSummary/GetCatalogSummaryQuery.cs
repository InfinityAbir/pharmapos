using MediatR;

namespace PharmaPOS.Application.Features.Catalog.GetCatalogSummary;

public sealed record GetCatalogSummaryQuery : IRequest<CatalogSummaryDto>;
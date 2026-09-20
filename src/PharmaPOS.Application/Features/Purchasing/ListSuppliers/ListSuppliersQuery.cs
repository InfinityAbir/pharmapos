using MediatR;

namespace PharmaPOS.Application.Features.Purchasing.ListSuppliers;

public sealed record ListSuppliersQuery(string? Search) : IRequest<IReadOnlyList<SupplierDto>>;
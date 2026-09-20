using MediatR;
using PharmaPOS.Application.Common.Purchasing;

namespace PharmaPOS.Application.Features.Purchasing.ListSuppliers;

public sealed class ListSuppliersQueryHandler : IRequestHandler<ListSuppliersQuery, IReadOnlyList<SupplierDto>>
{
    private readonly ISupplierRepository _suppliers;

    public ListSuppliersQueryHandler(ISupplierRepository suppliers) => _suppliers = suppliers;

    public Task<IReadOnlyList<SupplierDto>> Handle(
        ListSuppliersQuery request,
        CancellationToken cancellationToken)
        => _suppliers.ListAsync(request.Search, cancellationToken);
}
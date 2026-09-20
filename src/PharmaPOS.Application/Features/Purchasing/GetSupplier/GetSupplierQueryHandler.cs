using MediatR;
using PharmaPOS.Application.Common.Purchasing;
using PharmaPOS.Domain.Exceptions;

namespace PharmaPOS.Application.Features.Purchasing.GetSupplier;

public sealed class GetSupplierQueryHandler : IRequestHandler<GetSupplierQuery, SupplierDto>
{
    private readonly ISupplierRepository _suppliers;

    public GetSupplierQueryHandler(ISupplierRepository suppliers) => _suppliers = suppliers;

    public async Task<SupplierDto> Handle(
        GetSupplierQuery request,
        CancellationToken cancellationToken)
    {
        var supplier = await _suppliers.FindAsync(request.Id, cancellationToken);
        if (supplier is null)
        {
            throw new NotFoundException("supplier.not_found", $"Supplier '{request.Id}' was not found.");
        }

        return supplier;
    }
}
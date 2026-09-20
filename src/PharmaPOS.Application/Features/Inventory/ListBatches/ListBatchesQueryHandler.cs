using MediatR;
using PharmaPOS.Application.Common.Inventory;

namespace PharmaPOS.Application.Features.Inventory.ListBatches;

public sealed class ListBatchesQueryHandler : IRequestHandler<ListBatchesQuery, IReadOnlyList<BatchDto>>
{
    private readonly IBatchRepository _batches;

    public ListBatchesQueryHandler(IBatchRepository batches) => _batches = batches;

    public Task<IReadOnlyList<BatchDto>> Handle(
        ListBatchesQuery request,
        CancellationToken cancellationToken)
        => _batches.ListAsync(
            request.MedicineId,
            request.ExpiringWithinDays,
            request.IncludeExpired,
            cancellationToken);
}
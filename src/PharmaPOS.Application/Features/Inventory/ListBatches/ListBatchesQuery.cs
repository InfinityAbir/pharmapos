using MediatR;

namespace PharmaPOS.Application.Features.Inventory.ListBatches;

public sealed record ListBatchesQuery(
    Guid? MedicineId,
    int? ExpiringWithinDays,
    bool IncludeExpired = true) : IRequest<IReadOnlyList<BatchDto>>;
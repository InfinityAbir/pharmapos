using MediatR;

namespace PharmaPOS.Application.Features.Inventory.AdjustStock;

public sealed record AdjustStockCommand(Guid Id, int QuantityChange, string? Reason)
    : IRequest<BatchDto>;
using MediatR;
using PharmaPOS.Application.Common.Inventory;

namespace PharmaPOS.Application.Features.Inventory.ReceiveStock;

public sealed record ReceiveStockCommand(ReceiveStockInput Input) : IRequest<BatchDto>;
using MediatR;

namespace PharmaPOS.Application.Features.Purchasing.ListPurchases;

public sealed record ListPurchasesQuery(
    Guid? SupplierId,
    string? Status,
    int Skip = 0,
    int Take = 25) : IRequest<IReadOnlyList<PurchaseDto>>;
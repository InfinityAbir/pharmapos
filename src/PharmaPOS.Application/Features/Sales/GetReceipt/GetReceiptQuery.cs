using MediatR;

namespace PharmaPOS.Application.Features.Sales.GetReceipt;

public sealed record GetReceiptQuery(Guid Id) : IRequest<ReceiptDto>;
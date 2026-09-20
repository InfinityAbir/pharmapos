using MediatR;
using PharmaPOS.Application.Common.Sales;

namespace PharmaPOS.Application.Features.Returns.ListReturns;

public sealed class ListReturnsQueryHandler : IRequestHandler<ListReturnsQuery, IReadOnlyList<ReturnDto>>
{
    private readonly IReturnReader _returns;

    public ListReturnsQueryHandler(IReturnReader returns) => _returns = returns;

    public Task<IReadOnlyList<ReturnDto>> Handle(
        ListReturnsQuery request,
        CancellationToken cancellationToken)
        => _returns.ListAsync(request.SaleId, request.Skip, request.Take, cancellationToken);
}

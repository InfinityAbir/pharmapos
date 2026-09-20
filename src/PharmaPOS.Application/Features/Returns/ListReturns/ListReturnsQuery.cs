using MediatR;

namespace PharmaPOS.Application.Features.Returns.ListReturns;

public sealed record ListReturnsQuery(Guid? SaleId, int Skip, int Take) : IRequest<IReadOnlyList<ReturnDto>>;

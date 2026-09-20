using MediatR;

namespace PharmaPOS.Application.Features.Returns.GetReturn;

public sealed record GetReturnQuery(Guid Id) : IRequest<ReturnDto>;

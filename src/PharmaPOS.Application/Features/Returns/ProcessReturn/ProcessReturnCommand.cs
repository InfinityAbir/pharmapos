using MediatR;
using PharmaPOS.Application.Common.Sales;

namespace PharmaPOS.Application.Features.Returns.ProcessReturn;

public sealed record ProcessReturnCommand(ReturnInput Input) : IRequest<ProcessReturnResult>;

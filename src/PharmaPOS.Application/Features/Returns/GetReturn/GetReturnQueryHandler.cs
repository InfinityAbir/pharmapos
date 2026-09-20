using MediatR;
using PharmaPOS.Application.Common.Sales;
using PharmaPOS.Domain.Exceptions;

namespace PharmaPOS.Application.Features.Returns.GetReturn;

public sealed class GetReturnQueryHandler : IRequestHandler<GetReturnQuery, ReturnDto>
{
    private readonly IReturnReader _returns;

    public GetReturnQueryHandler(IReturnReader returns) => _returns = returns;

    public async Task<ReturnDto> Handle(
        GetReturnQuery request,
        CancellationToken cancellationToken)
    {
        var result = await _returns.FindAsync(request.Id, cancellationToken);
        if (result is null)
        {
            throw new NotFoundException("return.not_found", "Return not found in this tenant.");
        }

        return result;
    }
}

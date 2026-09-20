using FluentValidation;
using MediatR;
using PharmaPOS.Application.Common.Reporting;

namespace PharmaPOS.Application.Features.Reports.GetSalesByMethod;

public sealed record GetSalesByMethodQuery(
    DateOnly? From,
    DateOnly? To) : IRequest<IReadOnlyList<SalesByMethodRowDto>>, IReportRange;

public sealed class GetSalesByMethodQueryHandler
    : IRequestHandler<GetSalesByMethodQuery, IReadOnlyList<SalesByMethodRowDto>>
{
    private readonly IReportReader _reports;

    public GetSalesByMethodQueryHandler(IReportReader reports) => _reports = reports;

    public Task<IReadOnlyList<SalesByMethodRowDto>> Handle(
        GetSalesByMethodQuery request,
        CancellationToken cancellationToken)
    {
        var (from, to) = ReportBounds.ResolveRange(request.From, request.To);
        return _reports.GetSalesByMethodAsync(from, to, cancellationToken);
    }
}

public sealed class GetSalesByMethodQueryValidator : AbstractValidator<GetSalesByMethodQuery>
{
    public GetSalesByMethodQueryValidator()
    {
        RuleFor(query => query).SetValidator(new ReportRangeValidator());
    }
}

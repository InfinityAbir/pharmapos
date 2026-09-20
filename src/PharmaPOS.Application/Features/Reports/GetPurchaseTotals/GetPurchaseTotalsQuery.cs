using FluentValidation;
using MediatR;
using PharmaPOS.Application.Common.Reporting;

namespace PharmaPOS.Application.Features.Reports.GetPurchaseTotals;

public sealed record GetPurchaseTotalsQuery(
    DateOnly? From,
    DateOnly? To) : IRequest<PurchaseTotalsDto>, IReportRange;

public sealed class GetPurchaseTotalsQueryHandler
    : IRequestHandler<GetPurchaseTotalsQuery, PurchaseTotalsDto>
{
    private readonly IReportReader _reports;

    public GetPurchaseTotalsQueryHandler(IReportReader reports) => _reports = reports;

    public Task<PurchaseTotalsDto> Handle(
        GetPurchaseTotalsQuery request,
        CancellationToken cancellationToken)
    {
        var (from, to) = ReportBounds.ResolveRange(request.From, request.To);
        return _reports.GetPurchaseTotalsAsync(from, to, cancellationToken);
    }
}

public sealed class GetPurchaseTotalsQueryValidator : AbstractValidator<GetPurchaseTotalsQuery>
{
    public GetPurchaseTotalsQueryValidator()
    {
        RuleFor(query => query).SetValidator(new ReportRangeValidator());
    }
}

using FluentValidation;
using MediatR;
using PharmaPOS.Application.Common.Reporting;

namespace PharmaPOS.Application.Features.Reports.GetSalesByCashier;

public sealed record GetSalesByCashierQuery(
    DateOnly? From,
    DateOnly? To,
    int Skip,
    int Take) : IRequest<IReadOnlyList<SalesByCashierRowDto>>, IReportRange, IReportPaging;

public sealed class GetSalesByCashierQueryHandler
    : IRequestHandler<GetSalesByCashierQuery, IReadOnlyList<SalesByCashierRowDto>>
{
    private readonly IReportReader _reports;

    public GetSalesByCashierQueryHandler(IReportReader reports) => _reports = reports;

    public Task<IReadOnlyList<SalesByCashierRowDto>> Handle(
        GetSalesByCashierQuery request,
        CancellationToken cancellationToken)
    {
        var (from, to) = ReportBounds.ResolveRange(request.From, request.To);
        return _reports.GetSalesByCashierAsync(from, to, request.Skip, request.Take, cancellationToken);
    }
}

public sealed class GetSalesByCashierQueryValidator : AbstractValidator<GetSalesByCashierQuery>
{
    public GetSalesByCashierQueryValidator()
    {
        RuleFor(query => query).SetValidator(new ReportRangeValidator());
        RuleFor(query => query).SetValidator(new ReportPagingValidator<GetSalesByCashierQuery>());
    }
}

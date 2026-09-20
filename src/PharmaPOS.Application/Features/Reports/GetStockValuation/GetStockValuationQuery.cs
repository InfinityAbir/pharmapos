using FluentValidation;
using MediatR;
using PharmaPOS.Application.Common.Reporting;

namespace PharmaPOS.Application.Features.Reports.GetStockValuation;

public sealed record GetStockValuationQuery(
    int Skip,
    int Take) : IRequest<StockValuationDto>, IReportPaging;

public sealed class GetStockValuationQueryHandler
    : IRequestHandler<GetStockValuationQuery, StockValuationDto>
{
    private readonly IReportReader _reports;

    public GetStockValuationQueryHandler(IReportReader reports) => _reports = reports;

    public Task<StockValuationDto> Handle(
        GetStockValuationQuery request,
        CancellationToken cancellationToken)
        => _reports.GetStockValuationAsync(request.Skip, request.Take, cancellationToken);
}

public sealed class GetStockValuationQueryValidator : AbstractValidator<GetStockValuationQuery>
{
    public GetStockValuationQueryValidator()
    {
        RuleFor(query => query).SetValidator(new ReportPagingValidator<GetStockValuationQuery>());
    }
}

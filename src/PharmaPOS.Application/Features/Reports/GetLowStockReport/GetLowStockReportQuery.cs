using FluentValidation;
using MediatR;
using PharmaPOS.Application.Common.Reporting;

namespace PharmaPOS.Application.Features.Reports.GetLowStockReport;

public sealed record GetLowStockReportQuery(
    int Skip,
    int Take) : IRequest<IReadOnlyList<LowStockRowDto>>, IReportPaging;

public sealed class GetLowStockReportQueryHandler
    : IRequestHandler<GetLowStockReportQuery, IReadOnlyList<LowStockRowDto>>
{
    private readonly IReportReader _reports;

    public GetLowStockReportQueryHandler(IReportReader reports) => _reports = reports;

    public Task<IReadOnlyList<LowStockRowDto>> Handle(
        GetLowStockReportQuery request,
        CancellationToken cancellationToken)
        => _reports.GetLowStockReportAsync(request.Skip, request.Take, cancellationToken);
}

public sealed class GetLowStockReportQueryValidator : AbstractValidator<GetLowStockReportQuery>
{
    public GetLowStockReportQueryValidator()
    {
        RuleFor(query => query).SetValidator(new ReportPagingValidator<GetLowStockReportQuery>());
    }
}

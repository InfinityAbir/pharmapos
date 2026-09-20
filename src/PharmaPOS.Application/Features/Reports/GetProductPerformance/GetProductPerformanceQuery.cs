using FluentValidation;
using MediatR;
using PharmaPOS.Application.Common.Reporting;

namespace PharmaPOS.Application.Features.Reports.GetProductPerformance;

public sealed record GetProductPerformanceQuery(
    int Days,
    bool BestFirst,
    int Skip,
    int Take) : IRequest<IReadOnlyList<ProductPerformanceRowDto>>, IReportPaging;

public sealed class GetProductPerformanceQueryHandler
    : IRequestHandler<GetProductPerformanceQuery, IReadOnlyList<ProductPerformanceRowDto>>
{
    private readonly IReportReader _reports;

    public GetProductPerformanceQueryHandler(IReportReader reports) => _reports = reports;

    public Task<IReadOnlyList<ProductPerformanceRowDto>> Handle(
        GetProductPerformanceQuery request,
        CancellationToken cancellationToken)
        => _reports.GetProductPerformanceAsync(
            request.Days, request.BestFirst, request.Skip, request.Take, cancellationToken);
}

public sealed class GetProductPerformanceQueryValidator : AbstractValidator<GetProductPerformanceQuery>
{
    public GetProductPerformanceQueryValidator()
    {
        RuleFor(query => query.Days).InclusiveBetween(1, ReportBounds.MaxWindowDays);
        RuleFor(query => query).SetValidator(new ReportPagingValidator<GetProductPerformanceQuery>());
    }
}

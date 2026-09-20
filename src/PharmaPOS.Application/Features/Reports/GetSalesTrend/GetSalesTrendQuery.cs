using FluentValidation;
using MediatR;
using PharmaPOS.Application.Common.Reporting;

namespace PharmaPOS.Application.Features.Reports.GetSalesTrend;

public sealed record GetSalesTrendQuery(
    DateOnly? From,
    DateOnly? To,
    string Bucket) : IRequest<IReadOnlyList<SalesTrendBucketDto>>, IReportRange;

public sealed class GetSalesTrendQueryHandler
    : IRequestHandler<GetSalesTrendQuery, IReadOnlyList<SalesTrendBucketDto>>
{
    private readonly IReportReader _reports;

    public GetSalesTrendQueryHandler(IReportReader reports) => _reports = reports;

    public Task<IReadOnlyList<SalesTrendBucketDto>> Handle(
        GetSalesTrendQuery request,
        CancellationToken cancellationToken)
    {
        var (from, to) = ReportBounds.ResolveRange(request.From, request.To);
        return _reports.GetSalesTrendAsync(from, to, request.Bucket, cancellationToken);
    }
}

public sealed class GetSalesTrendQueryValidator : AbstractValidator<GetSalesTrendQuery>
{
    public GetSalesTrendQueryValidator()
    {
        RuleFor(query => query.Bucket)
            .NotEmpty()
            .Must(bucket => ReportBounds.Buckets.Contains(bucket))
            .WithMessage($"Bucket must be one of: {string.Join(", ", ReportBounds.Buckets)}.");
        RuleFor(query => query).SetValidator(new ReportRangeValidator());
    }
}

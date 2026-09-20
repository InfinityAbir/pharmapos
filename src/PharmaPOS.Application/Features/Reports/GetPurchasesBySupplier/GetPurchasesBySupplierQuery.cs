using FluentValidation;
using MediatR;
using PharmaPOS.Application.Common.Reporting;

namespace PharmaPOS.Application.Features.Reports.GetPurchasesBySupplier;

public sealed record GetPurchasesBySupplierQuery(
    DateOnly? From,
    DateOnly? To,
    int Skip,
    int Take) : IRequest<IReadOnlyList<PurchasesBySupplierRowDto>>, IReportRange, IReportPaging;

public sealed class GetPurchasesBySupplierQueryHandler
    : IRequestHandler<GetPurchasesBySupplierQuery, IReadOnlyList<PurchasesBySupplierRowDto>>
{
    private readonly IReportReader _reports;

    public GetPurchasesBySupplierQueryHandler(IReportReader reports) => _reports = reports;

    public Task<IReadOnlyList<PurchasesBySupplierRowDto>> Handle(
        GetPurchasesBySupplierQuery request,
        CancellationToken cancellationToken)
    {
        var (from, to) = ReportBounds.ResolveRange(request.From, request.To);
        return _reports.GetPurchasesBySupplierAsync(from, to, request.Skip, request.Take, cancellationToken);
    }
}

public sealed class GetPurchasesBySupplierQueryValidator : AbstractValidator<GetPurchasesBySupplierQuery>
{
    public GetPurchasesBySupplierQueryValidator()
    {
        RuleFor(query => query).SetValidator(new ReportRangeValidator());
        RuleFor(query => query).SetValidator(new ReportPagingValidator<GetPurchasesBySupplierQuery>());
    }
}

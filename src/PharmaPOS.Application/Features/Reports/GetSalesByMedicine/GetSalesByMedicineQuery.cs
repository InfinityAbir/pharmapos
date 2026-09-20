using FluentValidation;
using MediatR;
using PharmaPOS.Application.Common.Reporting;

namespace PharmaPOS.Application.Features.Reports.GetSalesByMedicine;

public sealed record GetSalesByMedicineQuery(
    DateOnly? From,
    DateOnly? To,
    int Skip,
    int Take) : IRequest<IReadOnlyList<SalesByMedicineRowDto>>, IReportRange, IReportPaging;

public sealed class GetSalesByMedicineQueryHandler
    : IRequestHandler<GetSalesByMedicineQuery, IReadOnlyList<SalesByMedicineRowDto>>
{
    private readonly IReportReader _reports;

    public GetSalesByMedicineQueryHandler(IReportReader reports) => _reports = reports;

    public Task<IReadOnlyList<SalesByMedicineRowDto>> Handle(
        GetSalesByMedicineQuery request,
        CancellationToken cancellationToken)
    {
        var (from, to) = ReportBounds.ResolveRange(request.From, request.To);
        return _reports.GetSalesByMedicineAsync(from, to, request.Skip, request.Take, cancellationToken);
    }
}

public sealed class GetSalesByMedicineQueryValidator : AbstractValidator<GetSalesByMedicineQuery>
{
    public GetSalesByMedicineQueryValidator()
    {
        RuleFor(query => query).SetValidator(new ReportRangeValidator());
        RuleFor(query => query).SetValidator(new ReportPagingValidator<GetSalesByMedicineQuery>());
    }
}

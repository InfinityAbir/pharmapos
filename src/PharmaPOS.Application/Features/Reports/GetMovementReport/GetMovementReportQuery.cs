using FluentValidation;
using MediatR;
using PharmaPOS.Application.Common.Reporting;
using PharmaPOS.Application.Features.Inventory;
using PharmaPOS.Domain.Inventory;

namespace PharmaPOS.Application.Features.Reports.GetMovementReport;

public sealed record GetMovementReportQuery(
    Guid? MedicineId,
    Guid? BatchId,
    string? Type,
    DateOnly? From,
    DateOnly? To,
    int Skip,
    int Take) : IRequest<IReadOnlyList<MovementDto>>, IReportRange, IReportPaging;

public sealed class GetMovementReportQueryHandler
    : IRequestHandler<GetMovementReportQuery, IReadOnlyList<MovementDto>>
{
    private readonly IReportReader _reports;

    public GetMovementReportQueryHandler(IReportReader reports) => _reports = reports;

    public Task<IReadOnlyList<MovementDto>> Handle(
        GetMovementReportQuery request,
        CancellationToken cancellationToken)
        => _reports.GetMovementReportAsync(
            request.MedicineId, request.BatchId, request.Type?.Trim(),
            request.From, request.To, request.Skip, request.Take, cancellationToken);
}

public sealed class GetMovementReportQueryValidator : AbstractValidator<GetMovementReportQuery>
{
    public GetMovementReportQueryValidator()
    {
        RuleFor(query => query.Type)
            .Must(type => type is null || MovementTypes.All.Contains(type))
            .When(query => !string.IsNullOrWhiteSpace(query.Type))
            .WithMessage($"Movement type must be one of: {string.Join(", ", MovementTypes.All)}.");
        RuleFor(query => query.From)
            .NotEmpty()
            .When(query => query.To.HasValue && !query.From.HasValue)
            .WithMessage("A start date is required when an end date is supplied.");
        RuleFor(query => query.To)
            .NotEmpty()
            .When(query => query.From.HasValue && !query.To.HasValue)
            .WithMessage("An end date is required when a start date is supplied.");
        RuleFor(query => query).SetValidator(new ReportRangeValidator());
        RuleFor(query => query).SetValidator(new ReportPagingValidator<GetMovementReportQuery>());
    }
}

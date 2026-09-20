using FluentValidation;

namespace PharmaPOS.Application.Features.Reports;

/// <summary>
/// Shared bounds for report queries: tenant-local date ranges default to the trailing 30
/// days and are capped at 366 days so no report can trigger an unbounded table scan;
/// paging is capped at 100 rows per call.
/// </summary>
public static class ReportBounds
{
    public const int DefaultSpanDays = 30;
    public const int MaxSpanDays = 366;
    public const int MaxTake = 100;
    public const int MaxWindowDays = 365;

    public static IReadOnlyList<string> Buckets { get; } = ["day", "week", "month"];

    public static (DateOnly From, DateOnly To) ResolveRange(DateOnly? from, DateOnly? to)
    {
        var end = to ?? DateOnly.FromDateTime(DateTime.UtcNow.Date);
        var start = from ?? end.AddDays(-(DefaultSpanDays - 1));
        return (start, end);
    }
}

/// <summary>Validates an optional tenant-local date range (ordering + span cap).</summary>
public sealed class ReportRangeValidator : AbstractValidator<IReportRange>
{
    public ReportRangeValidator()
    {
        RuleFor(query => query.To)
            .GreaterThanOrEqualTo(query => query.From!.Value)
            .When(query => query.From.HasValue && query.To.HasValue)
            .WithMessage("The end date must not precede the start date.");
        RuleFor(query => query)
            .Must(query =>
                !query.From.HasValue || !query.To.HasValue
                || query.To.Value.DayNumber - query.From.Value.DayNumber <= ReportBounds.MaxSpanDays)
            .WithMessage($"Report ranges are capped at {ReportBounds.MaxSpanDays} days.");
    }
}

public interface IReportRange
{
    DateOnly? From { get; }
    DateOnly? To { get; }
}

/// <summary>Validates skip/take paging (take capped so reports stay compact).</summary>
public sealed class ReportPagingValidator<T> : AbstractValidator<T>
    where T : IReportPaging
{
    public ReportPagingValidator()
    {
        RuleFor(query => query.Skip).GreaterThanOrEqualTo(0);
        RuleFor(query => query.Take).InclusiveBetween(1, ReportBounds.MaxTake);
    }
}

public interface IReportPaging
{
    int Skip { get; }
    int Take { get; }
}

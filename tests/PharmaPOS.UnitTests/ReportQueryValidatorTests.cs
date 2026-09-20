using FluentAssertions;
using PharmaPOS.Application.Features.Reports;
using PharmaPOS.Application.Features.Reports.GetMovementReport;
using PharmaPOS.Application.Features.Reports.GetProductPerformance;
using PharmaPOS.Application.Features.Reports.GetSalesByCashier;
using PharmaPOS.Application.Features.Reports.GetSalesTrend;
using Xunit;

namespace PharmaPOS.UnitTests;

public sealed class ReportQueryValidatorTests
{
    private static readonly DateOnly From = new(2026, 9, 1);
    private static readonly DateOnly To = new(2026, 9, 20);

    [Theory]
    [InlineData("day")]
    [InlineData("week")]
    [InlineData("month")]
    public async Task Accepts_allow_listed_buckets(string bucket)
    {
        var result = await new GetSalesTrendQueryValidator()
            .ValidateAsync(new GetSalesTrendQuery(From, To, bucket));

        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("year")]
    [InlineData("DAY")]
    public async Task Rejects_unknown_buckets(string bucket)
    {
        var result = await new GetSalesTrendQueryValidator()
            .ValidateAsync(new GetSalesTrendQuery(From, To, bucket));

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task Rejects_inverted_and_overlong_ranges()
    {
        var validator = new GetSalesByCashierQueryValidator();

        var inverted = await validator.ValidateAsync(
            new GetSalesByCashierQuery(To, From, 0, 25));
        inverted.IsValid.Should().BeFalse();

        var overlong = await validator.ValidateAsync(
            new GetSalesByCashierQuery(From, From.AddDays(ReportBounds.MaxSpanDays + 1), 0, 25));
        overlong.IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task Caps_page_sizes_and_rejects_negative_skips()
    {
        var result = await new GetSalesByCashierQueryValidator().ValidateAsync(
            new GetSalesByCashierQuery(From, To, -1, ReportBounds.MaxTake + 1));

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task Bounds_the_performance_window()
    {
        var validator = new GetProductPerformanceQueryValidator();

        var ok = await validator.ValidateAsync(new GetProductPerformanceQuery(30, true, 0, 10));
        ok.IsValid.Should().BeTrue();

        var tooWide = await validator.ValidateAsync(
            new GetProductPerformanceQuery(ReportBounds.MaxWindowDays + 1, true, 0, 10));
        tooWide.IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task Rejects_unknown_movement_types_but_allows_unfiltered_reports()
    {
        var validator = new GetMovementReportQueryValidator();

        var bad = await validator.ValidateAsync(
            new GetMovementReportQuery(null, null, "NOPE", null, null, 0, 25));
        bad.IsValid.Should().BeFalse();

        var open = await validator.ValidateAsync(
            new GetMovementReportQuery(null, null, null, null, null, 0, 25));
        open.IsValid.Should().BeTrue();
    }
}

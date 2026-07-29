using System.Text.Json;
using AFK4.Shared.Contracts.Billing;
using AFK4.Shared.Contracts.Reports;

namespace AFK4.Shared.Contracts.Tests;

public sealed class OrganizationAdminReportContractSerializationTests
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    [Fact]
    public void SummaryProjection_RoundTripsTrendAttentionAndPeriod()
    {
        var period = new OrganizationAdminReportPeriodDto(
            new DateOnly(2026, 7, 29), new DateOnly(2026, 7, 29), "Asia/Dushanbe",
            DateTimeOffset.Parse("2026-07-28T19:00:00Z"), DateTimeOffset.Parse("2026-07-29T19:00:00Z"));
        var source = new OrganizationAdminSummaryReportDto(
            period,
            4,
            [new OrganizationAdminReportAttentionDto("shift_discrepancy", "Смена", "Расхождение", Guid.NewGuid(), new MoneyDto("TJS", -500))],
            new OrganizationAdminReportFiguresDto(new MoneyDto("TJS", 10_000), new MoneyDto("TJS", 4_000), new MoneyDto("TJS", 6_000), 7_200),
            [new OrganizationAdminRevenueTrendPointDto(new DateOnly(2026, 7, 29), new MoneyDto("TJS", 10_000))],
            null);

        var copy = JsonSerializer.Deserialize<OrganizationAdminSummaryReportDto>(JsonSerializer.Serialize(source, Options), Options);

        Assert.NotNull(copy);
        Assert.Equal(source.Period, copy.Period);
        Assert.Equal(source.Figures, copy.Figures);
        Assert.Equal(4, copy.AttentionTotalCount);
        Assert.Single(copy.AttentionItems);
        Assert.Single(copy.Trend);
    }
}

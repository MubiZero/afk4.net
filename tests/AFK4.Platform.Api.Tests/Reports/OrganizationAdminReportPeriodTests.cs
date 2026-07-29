using AFK4.Platform.Api.Reports;

namespace AFK4.Platform.Api.Tests.Reports;

public sealed class OrganizationAdminReportPeriodTests
{
    [Fact]
    public void Resolve_UsesBranchTimezoneAndHalfOpenEndDate()
    {
        var period = OrganizationAdminReportPeriod.Resolve(
            new DateOnly(2026, 7, 29),
            new DateOnly(2026, 7, 29),
            "Asia/Dushanbe");

        Assert.Equal(DateTimeOffset.Parse("2026-07-28T19:00:00Z"), period.FromUtc);
        Assert.Equal(DateTimeOffset.Parse("2026-07-29T19:00:00Z"), period.ToUtc);
        Assert.Equal("Asia/Dushanbe", period.TimeZone);
    }

    [Fact]
    public void Resolve_RejectsAnInvertedRange()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => OrganizationAdminReportPeriod.Resolve(
            new DateOnly(2026, 7, 30),
            new DateOnly(2026, 7, 29),
            "Asia/Dushanbe"));
    }

    [Fact]
    public void Resolve_RejectsRangesLongerThanOneYear()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => OrganizationAdminReportPeriod.Resolve(
            new DateOnly(2025, 1, 1),
            new DateOnly(2026, 1, 2),
            "Asia/Dushanbe"));
    }
}

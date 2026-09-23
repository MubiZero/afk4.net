using AFK4.Platform.Api.Reports;
using AFK4.Shared.Contracts.Billing;
using AFK4.Shared.Contracts.Reports;

namespace AFK4.Platform.Api.Tests.Reports;

public sealed class OrganizationAdminReportCsvExporterTests
{
    private static readonly DateOnly Day = new(2026, 7, 29);

    // Выгрузка — та же проекция, что на экране «Выручка»: всё, что владелец видит там,
    // должно доехать и до файла, иначе сверять таблицу с экраном нечем.
    [Fact]
    public void ExportRevenue_CarriesEveryFigureOfTheScreen()
    {
        var report = new OrganizationAdminRevenueReportDto(
            new OrganizationAdminReportPeriodDto(Day, Day, "Asia/Dushanbe",
                DateTimeOffset.Parse("2026-07-28T19:00:00Z"), DateTimeOffset.Parse("2026-07-29T19:00:00Z")),
            Money(7_000), Money(-500), Money(6_500), Money(1_500), 3_600, Money(5_000),
            new OrganizationAdminRevenueComparisonDto(Money(4_000), 2_500, 62.5m),
            [new("gameplay", Money(1_500)), new("pos", Money(5_000))],
            [new("cash", "cash", Money(3_000)), new("card_manual", "card_manual", Money(2_000))],
            [new("f3f3f3f3-f3f3-4f3f-8f3f-f3f3f3f3f3f3", "Cashier, \"Dilnoza\"", Money(6_500))]);

        var lines = OrganizationAdminReportCsvExporter.Export(report).Split("\r\n", StringSplitOptions.RemoveEmptyEntries);

        Assert.Equal(
        [
            "section,source,currency,amount_minor_units,from_date,to_date",
            "\"total\",\"gross\",\"TJS\",7000,2026-07-29,2026-07-29",
            "\"total\",\"refunds\",\"TJS\",-500,2026-07-29,2026-07-29",
            "\"total\",\"net\",\"TJS\",6500,2026-07-29,2026-07-29",
            "\"source\",\"gameplay\",\"TJS\",1500,2026-07-29,2026-07-29",
            "\"source\",\"pos\",\"TJS\",5000,2026-07-29,2026-07-29",
            "\"comparison\",\"previous_net\",\"TJS\",4000,2026-07-28,2026-07-28",
            "\"comparison\",\"difference\",\"TJS\",2500,2026-07-29,2026-07-29",
            "\"payment_method\",\"cash\",\"TJS\",3000,2026-07-29,2026-07-29",
            "\"payment_method\",\"card_manual\",\"TJS\",2000,2026-07-29,2026-07-29",
            "\"operator\",\"Cashier, \"\"Dilnoza\"\"\",\"TJS\",6500,2026-07-29,2026-07-29"
        ], lines);
    }

    private static MoneyDto Money(long minorUnits) => new("TJS", minorUnits);
}

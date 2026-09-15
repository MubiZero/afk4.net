using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AFK4.Platform.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class UniqueReportScheduleTriple : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Уникальный индекс не ляжет на таблицу с дублями, поэтому сначала чистка.
            //
            // Удаляется ровно лишнее: из каждой тройки «филиал + отчёт + частота» остаётся самая
            // ранняя запись. Дубль рассылки не несёт ни денег, ни истории — только второе такое же
            // письмо владельцу каждый период, так что терять здесь нечего.
            migrationBuilder.Sql("""
                DELETE FROM report_schedules older
                USING report_schedules newer
                WHERE older."OrganizationId" = newer."OrganizationId"
                  AND older."BranchId" = newer."BranchId"
                  AND older."ReportType" = newer."ReportType"
                  AND older."Frequency" = newer."Frequency"
                  AND older."CreatedAtUtc" > newer."CreatedAtUtc";
                """);

            migrationBuilder.CreateIndex(
                name: "IX_report_schedules_OrganizationId_BranchId_ReportType_Frequen~",
                table: "report_schedules",
                columns: new[] { "OrganizationId", "BranchId", "ReportType", "Frequency" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_report_schedules_OrganizationId_BranchId_ReportType_Frequen~",
                table: "report_schedules");
        }
    }
}

using Npgsql;

namespace AFK4.Platform.Api.Tests.Migrations;

/// <summary>
/// Уникальность тройки «филиал + отчёт + частота» на настоящей PostgreSQL.
///
/// Проверка «такая уже есть» в службе держит обычный случай — человек не помнит, заводил ли он
/// рассылку, — но не гонку: два одновременных запроса оба читают «нет» до того, как первый успеет
/// записать. Единственный, кто это закрывает, — индекс, и проверить его in-memory провайдером
/// нельзя: он миграции не исполняет и уникальность так не соблюдает.
/// </summary>
public sealed class UniqueReportScheduleTripleMigrationTests
{
    private const string PreviousMigration = "AddPersonNotificationsReadMarker";
    private const string ThisMigration = "UniqueReportScheduleTriple";

    private static readonly Guid Organization = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");
    private static readonly Guid Branch = Guid.Parse("bbbbbbbb-0000-0000-0000-000000000001");

    [MigrationPostgresFact]
    public async Task Up_RejectsASecondScheduleForTheSameReportAndFrequency()
    {
        await using var schema = await MigrationSchema.CreateAsync("report_schedule_unique");
        await schema.MigrateToAsync(ThisMigration);
        await schema.ExecuteAsync(InsertSql(Guid.NewGuid(), "sales", "daily", "2026-09-01T10:00:00Z"));

        var second = await Record.ExceptionAsync(() =>
            schema.ExecuteAsync(InsertSql(Guid.NewGuid(), "sales", "daily", "2026-09-02T10:00:00Z")));

        var postgres = Assert.IsType<PostgresException>(second);
        // 23505 — unique_violation. Проверяется код, а не текст: текст зависит от локали сервера.
        Assert.Equal("23505", postgres.SqlState);
    }

    [MigrationPostgresFact]
    public async Task Up_LeavesDifferentFrequenciesOfTheSameReportAlone()
    {
        await using var schema = await MigrationSchema.CreateAsync("report_schedule_unique_ok");
        await schema.MigrateToAsync(ThisMigration);

        await schema.ExecuteAsync(InsertSql(Guid.NewGuid(), "sales", "daily", "2026-09-01T10:00:00Z"));
        await schema.ExecuteAsync(InsertSql(Guid.NewGuid(), "sales", "weekly", "2026-09-01T10:00:00Z"));

        Assert.Equal("2", await schema.ScalarAsync(@"SELECT count(*) FROM report_schedules"));
    }

    /// <summary>
    /// Индекс не ляжет на таблицу с дублями, поэтому миграция их сначала убирает. Убирает ровно
    /// лишнее: из каждой тройки остаётся самая ранняя запись. Дубль рассылки не несёт ни денег, ни
    /// истории — только второе такое же письмо владельцу каждый период.
    /// </summary>
    [MigrationPostgresFact]
    public async Task Up_KeepsTheEarliestOfPreExistingDuplicates()
    {
        await using var schema = await MigrationSchema.CreateAsync("report_schedule_dedup");
        await schema.MigrateToAsync(PreviousMigration);
        var earliest = Guid.Parse("cccccccc-0000-0000-0000-000000000001");
        await schema.ExecuteAsync(InsertSql(earliest, "sales", "daily", "2026-09-01T10:00:00Z"));
        await schema.ExecuteAsync(InsertSql(Guid.NewGuid(), "sales", "daily", "2026-09-02T10:00:00Z"));
        await schema.ExecuteAsync(InsertSql(Guid.NewGuid(), "sales", "daily", "2026-09-03T10:00:00Z"));

        await schema.MigrateToAsync(ThisMigration);

        Assert.Equal("1", await schema.ScalarAsync(@"SELECT count(*) FROM report_schedules"));
        Assert.Equal(
            earliest.ToString(),
            await schema.ScalarAsync(@"SELECT ""ReportScheduleId"" FROM report_schedules"));
    }

    private static string InsertSql(Guid scheduleId, string reportType, string frequency, string createdAtUtc) =>
        $"""
        INSERT INTO report_schedules (
            "ReportScheduleId", "OrganizationId", "BranchId", "ReportType", "Frequency",
            "IsActive", "NextRunUtc", "LastRunUtc", "CreatedByStaffUserId", "CreatedAtUtc", "UpdatedAtUtc")
        VALUES (
            '{scheduleId}', '{Organization}', '{Branch}', '{reportType}', '{frequency}',
            true, '{createdAtUtc}', NULL, '{Guid.Empty}', '{createdAtUtc}', '{createdAtUtc}');
        """;
}

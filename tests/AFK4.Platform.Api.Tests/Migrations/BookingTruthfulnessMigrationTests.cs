using Npgsql;

namespace AFK4.Platform.Api.Tests.Migrations;

/// <summary>
/// Схема волны 2 на настоящей PostgreSQL. Одна миграция на всю волну — как и в первой: снапшот
/// модели один на проект, и две параллельные ветки со своими миграциями гарантированно дают
/// конфликт в нём. Поэтому колонки заводятся все сразу, до того как написана бизнес-логика.
///
/// In-memory провайдер такую проверку не заменяет вовсе: он миграции не исполняет.
/// </summary>
public sealed class BookingTruthfulnessMigrationTests
{
    private const string PreviousMigration = "AddPlatformAccountPool";
    private const string ThisMigration = "AddBookingTruthfulness";

    private static readonly (string Table, string Column)[] NewColumns =
    [
        // Пункт 10: «не приехал» становится состоянием, а не отменой с пометкой в тексте.
        ("reservations", "NoShowAtUtc"),
        ("reservations", "RetainedAmountMinorUnits"),
        // Пункт 11: отказ клуба — со своей причиной, отличимой от «игрок передумал».
        ("reservations", "RejectedAtUtc"),
        ("reservations", "RejectReasonCode"),
        ("reservations", "RejectReasonNote"),
        // Пункт 13: откуда взялась сессия — стойка, самопосадка по PIN или бронь.
        ("sessions", "Origin")
    ];

    [MigrationPostgresFact]
    public async Task Up_AddsEveryColumnTheWaveNeeds()
    {
        await using var schema = await MigrationSchema.CreateAsync("booking_truthfulness");

        await schema.MigrateToAsync(ThisMigration);

        foreach (var (table, column) in NewColumns)
        {
            Assert.Equal("1", await schema.ScalarAsync(ColumnExistsSql(table, column)));
        }
    }

    /// <summary>
    /// Ни одна новая колонка не обязательна к заполнению: строки, заведённые до волны, остаются
    /// валидными. Исключение — происхождение сессии: оно не пустое по умолчанию, потому что
    /// «неизвестно» и «оператор» — разные ответы, и путать их нельзя.
    /// </summary>
    [MigrationPostgresFact]
    public async Task Up_LeavesExistingRowsValid()
    {
        await using var schema = await MigrationSchema.CreateAsync("booking_truthfulness_rows");

        await schema.MigrateToAsync(ThisMigration);

        foreach (var (table, column) in NewColumns)
        {
            var nullable = await schema.ScalarAsync(IsNullableSql(table, column));
            var expected = column == "Origin" ? "NO" : "YES";
            Assert.Equal(expected, nullable);
        }
    }

    [MigrationPostgresFact]
    public async Task Down_ReturnsTheSchemaToWhereItWas()
    {
        await using var schema = await MigrationSchema.CreateAsync("booking_truthfulness_down");
        await schema.MigrateToAsync(ThisMigration);
        Assert.Equal("1", await schema.ScalarAsync(ColumnExistsSql("reservations", "NoShowAtUtc")));

        await schema.MigrateToAsync(PreviousMigration);

        foreach (var (table, column) in NewColumns)
        {
            Assert.Equal("0", await schema.ScalarAsync(ColumnExistsSql(table, column)));
        }

        // Волна 1 при этом на месте: откат волны 2 не имеет права её задеть.
        Assert.Equal("1", await schema.ScalarAsync(ColumnExistsSql("reservations", "RespondByUtc")));
        Assert.Equal("1", await schema.ScalarAsync(ColumnExistsSql("player_accounts", "PlatformPersonId")));
    }

    private static string ColumnExistsSql(string table, string column) => $"""
        SELECT count(*)::text FROM information_schema.columns
        WHERE table_schema = current_schema() AND table_name = '{table}' AND column_name = '{column}'
        """;

    private static string IsNullableSql(string table, string column) => $"""
        SELECT is_nullable FROM information_schema.columns
        WHERE table_schema = current_schema() AND table_name = '{table}' AND column_name = '{column}'
        """;
}

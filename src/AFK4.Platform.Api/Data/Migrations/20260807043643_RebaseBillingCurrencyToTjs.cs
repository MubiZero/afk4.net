using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AFK4.Platform.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class RebaseBillingCurrencyToTjs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE "subscription_plans" SET "CurrencyCode" = 'TJS' WHERE "CurrencyCode" = 'RUB';
                UPDATE "tenant_subscriptions" SET "CurrencyCode" = 'TJS' WHERE "CurrencyCode" = 'RUB';
                UPDATE "invoices" SET "CurrencyCode" = 'TJS' WHERE "CurrencyCode" = 'RUB';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Обратная перекодировка вернула бы неверную валюту суммам, которые с тех пор
            // могли быть выставлены в сомони.
        }
    }
}

using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AFK4.Platform.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddDcGateColumnsAndWebhookEvents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "Disputed",
                table: "payment_intents",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "GatewayComment",
                table: "payment_intents",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "GatewayExpiresAtUtc",
                table: "payment_intents",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GatewayPayUrl",
                table: "payment_intents",
                type: "character varying(1024)",
                maxLength: 1024,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GatewayPaymentId",
                table: "payment_intents",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "dcgate_webhook_events",
                columns: table => new
                {
                    DcGateWebhookEventId = table.Column<Guid>(type: "uuid", nullable: false),
                    EventId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    EventType = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ProcessedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_dcgate_webhook_events", x => x.DcGateWebhookEventId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_dcgate_webhook_events_EventId",
                table: "dcgate_webhook_events",
                column: "EventId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "dcgate_webhook_events");

            migrationBuilder.DropColumn(
                name: "Disputed",
                table: "payment_intents");

            migrationBuilder.DropColumn(
                name: "GatewayComment",
                table: "payment_intents");

            migrationBuilder.DropColumn(
                name: "GatewayExpiresAtUtc",
                table: "payment_intents");

            migrationBuilder.DropColumn(
                name: "GatewayPayUrl",
                table: "payment_intents");

            migrationBuilder.DropColumn(
                name: "GatewayPaymentId",
                table: "payment_intents");
        }
    }
}

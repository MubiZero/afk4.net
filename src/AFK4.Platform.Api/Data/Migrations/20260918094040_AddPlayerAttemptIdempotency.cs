using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AFK4.Platform.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPlayerAttemptIdempotency : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "IdempotencyKeyHash",
                table: "reservations",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "IdempotencyKeyHash",
                table: "payment_intents",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_reservations_PlayerAccountId_IdempotencyKeyHash",
                table: "reservations",
                columns: new[] { "PlayerAccountId", "IdempotencyKeyHash" },
                unique: true,
                filter: "\"IdempotencyKeyHash\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_payment_intents_PlayerAccountId_IdempotencyKeyHash",
                table: "payment_intents",
                columns: new[] { "PlayerAccountId", "IdempotencyKeyHash" },
                unique: true,
                filter: "\"IdempotencyKeyHash\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_reservations_PlayerAccountId_IdempotencyKeyHash",
                table: "reservations");

            migrationBuilder.DropIndex(
                name: "IX_payment_intents_PlayerAccountId_IdempotencyKeyHash",
                table: "payment_intents");

            migrationBuilder.DropColumn(
                name: "IdempotencyKeyHash",
                table: "reservations");

            migrationBuilder.DropColumn(
                name: "IdempotencyKeyHash",
                table: "payment_intents");
        }
    }
}

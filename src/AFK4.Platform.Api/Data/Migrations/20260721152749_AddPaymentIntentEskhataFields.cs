using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AFK4.Platform.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPaymentIntentEskhataFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "GatewayPosId",
                table: "payment_intents",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GatewayQrPayload",
                table: "payment_intents",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "GatewayPosId",
                table: "payment_intents");

            migrationBuilder.DropColumn(
                name: "GatewayQrPayload",
                table: "payment_intents");
        }
    }
}

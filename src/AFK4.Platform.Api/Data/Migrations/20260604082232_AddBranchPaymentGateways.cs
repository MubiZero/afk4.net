using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AFK4.Platform.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddBranchPaymentGateways : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "branch_payment_gateways",
                columns: table => new
                {
                    BranchPaymentGatewayId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: true),
                    DcgateProjectId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    ApiKeyEncrypted = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                    WebhookSecretEncrypted = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                    CardLast4 = table.Column<string>(type: "character varying(4)", maxLength: 4, nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_branch_payment_gateways", x => x.BranchPaymentGatewayId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_branch_payment_gateways_DcgateProjectId",
                table: "branch_payment_gateways",
                column: "DcgateProjectId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_branch_payment_gateways_OrganizationId_BranchId",
                table: "branch_payment_gateways",
                columns: new[] { "OrganizationId", "BranchId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "branch_payment_gateways");
        }
    }
}

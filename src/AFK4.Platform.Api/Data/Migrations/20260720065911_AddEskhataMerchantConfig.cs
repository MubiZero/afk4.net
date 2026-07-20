using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AFK4.Platform.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddEskhataMerchantConfig : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "eskhata_merchant_configs",
                columns: table => new
                {
                    EskhataMerchantConfigId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: true),
                    BaseUrl = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    CompanyId = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    PosId = table.Column<int>(type: "integer", nullable: false),
                    HashKeyEncrypted = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_eskhata_merchant_configs", x => x.EskhataMerchantConfigId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_eskhata_merchant_configs_OrganizationId_BranchId",
                table: "eskhata_merchant_configs",
                columns: new[] { "OrganizationId", "BranchId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "eskhata_merchant_configs");
        }
    }
}

using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AFK4.Platform.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddOrganizationTelegramApiCredentials : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "organization_telegram_api_credentials",
                columns: table => new
                {
                    OrganizationTelegramApiCredentialId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    PhoneNumber = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ApiIdEncrypted = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                    ApiHashEncrypted = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_organization_telegram_api_credentials", x => x.OrganizationTelegramApiCredentialId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_organization_telegram_api_credentials_OrganizationId_PhoneN~",
                table: "organization_telegram_api_credentials",
                columns: new[] { "OrganizationId", "PhoneNumber" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "organization_telegram_api_credentials");
        }
    }
}

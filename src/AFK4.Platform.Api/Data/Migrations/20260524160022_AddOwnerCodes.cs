using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AFK4.Platform.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddOwnerCodes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "owner_codes",
                columns: table => new
                {
                    OwnerCodeId = table.Column<Guid>(type: "uuid", nullable: false),
                    StaffUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CodeHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    CodeSuffix = table.Column<string>(type: "character varying(4)", maxLength: 4, nullable: false),
                    ExpiresAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastUsedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    FailedAttemptCount = table.Column<int>(type: "integer", nullable: false),
                    RevokedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    RevokedReason = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_owner_codes", x => x.OwnerCodeId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_owner_codes_CodeHash",
                table: "owner_codes",
                column: "CodeHash");

            migrationBuilder.CreateIndex(
                name: "IX_owner_codes_ExpiresAtUtc",
                table: "owner_codes",
                column: "ExpiresAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_owner_codes_StaffUserId",
                table: "owner_codes",
                column: "StaffUserId",
                filter: "\"RevokedAtUtc\" IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "owner_codes");
        }
    }
}

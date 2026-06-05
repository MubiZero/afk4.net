using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AFK4.Platform.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddStaffPhoneIdentity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "NormalizedPhone",
                table: "staff_users",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Phone",
                table: "staff_users",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "PhoneVerifiedAtUtc",
                table: "staff_users",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_staff_users_NormalizedPhone",
                table: "staff_users",
                column: "NormalizedPhone",
                unique: true,
                filter: "\"NormalizedPhone\" IS NOT NULL AND \"PhoneVerifiedAtUtc\" IS NOT NULL AND \"IsActive\"");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_staff_users_NormalizedPhone",
                table: "staff_users");

            migrationBuilder.DropColumn(
                name: "NormalizedPhone",
                table: "staff_users");

            migrationBuilder.DropColumn(
                name: "Phone",
                table: "staff_users");

            migrationBuilder.DropColumn(
                name: "PhoneVerifiedAtUtc",
                table: "staff_users");
        }
    }
}

using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AFK4.Platform.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddBranchClubProfile : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Address",
                table: "branches",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Description",
                table: "branches",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "LogoMediaId",
                table: "branches",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LogoUrl",
                table: "branches",
                type: "character varying(600)",
                maxLength: 600,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Phone",
                table: "branches",
                type: "character varying(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Telegram",
                table: "branches",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Website",
                table: "branches",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WorkingHoursJson",
                table: "branches",
                type: "jsonb",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Address",
                table: "branches");

            migrationBuilder.DropColumn(
                name: "Description",
                table: "branches");

            migrationBuilder.DropColumn(
                name: "LogoMediaId",
                table: "branches");

            migrationBuilder.DropColumn(
                name: "LogoUrl",
                table: "branches");

            migrationBuilder.DropColumn(
                name: "Phone",
                table: "branches");

            migrationBuilder.DropColumn(
                name: "Telegram",
                table: "branches");

            migrationBuilder.DropColumn(
                name: "Website",
                table: "branches");

            migrationBuilder.DropColumn(
                name: "WorkingHoursJson",
                table: "branches");
        }
    }
}

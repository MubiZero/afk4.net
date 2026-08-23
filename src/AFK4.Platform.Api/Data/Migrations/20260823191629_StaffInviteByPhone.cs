using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AFK4.Platform.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class StaffInviteByPhone : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_staff_invites_TokenHash",
                table: "staff_invites");

            migrationBuilder.DropColumn(
                name: "TokenHash",
                table: "staff_invites");

            migrationBuilder.AlterColumn<string>(
                name: "Email",
                table: "staff_invites",
                type: "character varying(320)",
                maxLength: 320,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(320)",
                oldMaxLength: 320);

            migrationBuilder.AddColumn<int>(
                name: "AttemptCount",
                table: "staff_invites",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "CodeHash",
                table: "staff_invites",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "NormalizedPhone",
                table: "staff_invites",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "PhoneNumber",
                table: "staff_invites",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_staff_invites_NormalizedPhone",
                table: "staff_invites",
                column: "NormalizedPhone");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_staff_invites_NormalizedPhone",
                table: "staff_invites");

            migrationBuilder.DropColumn(
                name: "AttemptCount",
                table: "staff_invites");

            migrationBuilder.DropColumn(
                name: "CodeHash",
                table: "staff_invites");

            migrationBuilder.DropColumn(
                name: "NormalizedPhone",
                table: "staff_invites");

            migrationBuilder.DropColumn(
                name: "PhoneNumber",
                table: "staff_invites");

            migrationBuilder.AlterColumn<string>(
                name: "Email",
                table: "staff_invites",
                type: "character varying(320)",
                maxLength: 320,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(320)",
                oldMaxLength: 320,
                oldNullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "TokenHash",
                table: "staff_invites",
                type: "bytea",
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.CreateIndex(
                name: "IX_staff_invites_TokenHash",
                table: "staff_invites",
                column: "TokenHash");
        }
    }
}

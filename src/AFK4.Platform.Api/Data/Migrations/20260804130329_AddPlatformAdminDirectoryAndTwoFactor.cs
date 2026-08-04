using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AFK4.Platform.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPlatformAdminDirectoryAndTwoFactor : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "FailedTwoFactorAttempts",
                table: "platform_admin_users",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LastSignInAtUtc",
                table: "platform_admin_users",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RecoveryCodeHashesJson",
                table: "platform_admin_users",
                type: "text",
                nullable: false,
                defaultValue: "[]");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "TotpEnabledAtUtc",
                table: "platform_admin_users",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TotpSecretEncrypted",
                table: "platform_admin_users",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "TwoFactorLockedUntilUtc",
                table: "platform_admin_users",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "platform_admin_invitations",
                columns: table => new
                {
                    InvitationId = table.Column<Guid>(type: "uuid", nullable: false),
                    CodeHash = table.Column<byte[]>(type: "bytea", nullable: false),
                    Role = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ExpiresAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedByPlatformAdminUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    AcceptedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    AcceptedPlatformAdminUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    RevokedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_platform_admin_invitations", x => x.InvitationId);
                });

            migrationBuilder.CreateTable(
                name: "platform_admin_sign_in_challenges",
                columns: table => new
                {
                    ChallengeId = table.Column<Guid>(type: "uuid", nullable: false),
                    PlatformAdminUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    TokenHash = table.Column<byte[]>(type: "bytea", nullable: false),
                    ExpiresAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ConsumedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_platform_admin_sign_in_challenges", x => x.ChallengeId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_platform_admin_invitations_CodeHash",
                table: "platform_admin_invitations",
                column: "CodeHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_platform_admin_invitations_Status_ExpiresAtUtc",
                table: "platform_admin_invitations",
                columns: new[] { "Status", "ExpiresAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_platform_admin_sign_in_challenges_PlatformAdminUserId_Expir~",
                table: "platform_admin_sign_in_challenges",
                columns: new[] { "PlatformAdminUserId", "ExpiresAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_platform_admin_sign_in_challenges_TokenHash",
                table: "platform_admin_sign_in_challenges",
                column: "TokenHash",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "platform_admin_invitations");

            migrationBuilder.DropTable(
                name: "platform_admin_sign_in_challenges");

            migrationBuilder.DropColumn(
                name: "FailedTwoFactorAttempts",
                table: "platform_admin_users");

            migrationBuilder.DropColumn(
                name: "LastSignInAtUtc",
                table: "platform_admin_users");

            migrationBuilder.DropColumn(
                name: "RecoveryCodeHashesJson",
                table: "platform_admin_users");

            migrationBuilder.DropColumn(
                name: "TotpEnabledAtUtc",
                table: "platform_admin_users");

            migrationBuilder.DropColumn(
                name: "TotpSecretEncrypted",
                table: "platform_admin_users");

            migrationBuilder.DropColumn(
                name: "TwoFactorLockedUntilUtc",
                table: "platform_admin_users");
        }
    }
}

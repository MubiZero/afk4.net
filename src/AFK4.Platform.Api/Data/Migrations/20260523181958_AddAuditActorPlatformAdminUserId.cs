using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AFK4.Platform.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAuditActorPlatformAdminUserId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ActorPlatformAdminUserId",
                table: "audit_records",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_audit_records_ActorPlatformAdminUserId_CreatedAtUtc",
                table: "audit_records",
                columns: new[] { "ActorPlatformAdminUserId", "CreatedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_audit_records_ActorPlatformAdminUserId_CreatedAtUtc",
                table: "audit_records");

            migrationBuilder.DropColumn(
                name: "ActorPlatformAdminUserId",
                table: "audit_records");
        }
    }
}

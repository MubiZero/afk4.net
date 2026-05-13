using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AFK4.Platform.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSessions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "session_command_idempotency",
                columns: table => new
                {
                    SessionCommandIdempotencyId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: false),
                    IdempotencyKeyHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Operation = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    RequestHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    ResponseJson = table.Column<string>(type: "jsonb", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ExpiresAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_session_command_idempotency", x => x.SessionCommandIdempotencyId);
                });

            migrationBuilder.CreateTable(
                name: "session_events",
                columns: table => new
                {
                    SessionEventId = table.Column<Guid>(type: "uuid", nullable: false),
                    SessionId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: false),
                    EventType = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    ActorStaffUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeviceId = table.Column<Guid>(type: "uuid", nullable: true),
                    DetailsJson = table.Column<string>(type: "jsonb", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_session_events", x => x.SessionEventId);
                });

            migrationBuilder.CreateTable(
                name: "session_leases",
                columns: table => new
                {
                    SessionLeaseId = table.Column<Guid>(type: "uuid", nullable: false),
                    SessionId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: false),
                    SeatId = table.Column<Guid>(type: "uuid", nullable: false),
                    DeviceId = table.Column<Guid>(type: "uuid", nullable: false),
                    State = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Sequence = table.Column<int>(type: "integer", nullable: false),
                    IssuedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ExpiresAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    SignatureAlgorithm = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Signature = table.Column<string>(type: "text", nullable: false),
                    PayloadJson = table.Column<string>(type: "jsonb", nullable: false),
                    RevokedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_session_leases", x => x.SessionLeaseId);
                });

            migrationBuilder.CreateTable(
                name: "sessions",
                columns: table => new
                {
                    SessionId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: false),
                    SeatId = table.Column<Guid>(type: "uuid", nullable: false),
                    DeviceId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedByStaffUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    PlayerKind = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    PlayerAccountId = table.Column<Guid>(type: "uuid", nullable: true),
                    TariffRuleVersionId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    State = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    RequestedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    StartedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    EndsAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    EndedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CurrentLeaseId = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sessions", x => x.SessionId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_session_command_idempotency_OrganizationId_BranchId_Idempot~",
                table: "session_command_idempotency",
                columns: new[] { "OrganizationId", "BranchId", "IdempotencyKeyHash", "Operation" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_session_events_SessionId_CreatedAtUtc",
                table: "session_events",
                columns: new[] { "SessionId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_session_leases_DeviceId_ExpiresAtUtc",
                table: "session_leases",
                columns: new[] { "DeviceId", "ExpiresAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_session_leases_SessionId_Sequence",
                table: "session_leases",
                columns: new[] { "SessionId", "Sequence" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_sessions_CurrentLeaseId",
                table: "sessions",
                column: "CurrentLeaseId");

            migrationBuilder.CreateIndex(
                name: "IX_sessions_OrganizationId_BranchId_DeviceId_State",
                table: "sessions",
                columns: new[] { "OrganizationId", "BranchId", "DeviceId", "State" });

            migrationBuilder.CreateIndex(
                name: "IX_sessions_OrganizationId_BranchId_SeatId_State",
                table: "sessions",
                columns: new[] { "OrganizationId", "BranchId", "SeatId", "State" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "session_command_idempotency");

            migrationBuilder.DropTable(
                name: "session_events");

            migrationBuilder.DropTable(
                name: "session_leases");

            migrationBuilder.DropTable(
                name: "sessions");
        }
    }
}

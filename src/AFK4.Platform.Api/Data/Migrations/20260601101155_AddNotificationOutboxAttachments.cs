using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AFK4.Platform.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddNotificationOutboxAttachments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "notification_outbox_attachments",
                columns: table => new
                {
                    NotificationOutboxAttachmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    NotificationOutboxId = table.Column<Guid>(type: "uuid", nullable: false),
                    FileName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    ContentType = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Content = table.Column<byte[]>(type: "bytea", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_notification_outbox_attachments", x => x.NotificationOutboxAttachmentId);
                    table.ForeignKey(
                        name: "FK_notification_outbox_attachments_notification_outbox_Notific~",
                        column: x => x.NotificationOutboxId,
                        principalTable: "notification_outbox",
                        principalColumn: "NotificationOutboxId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_notification_outbox_attachments_NotificationOutboxId",
                table: "notification_outbox_attachments",
                column: "NotificationOutboxId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "notification_outbox_attachments");
        }
    }
}

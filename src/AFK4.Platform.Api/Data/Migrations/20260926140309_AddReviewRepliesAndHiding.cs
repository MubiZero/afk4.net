using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AFK4.Platform.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddReviewRepliesAndHiding : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CommentHiddenAtUtc",
                table: "club_reviews",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CommentHiddenByStaffUserId",
                table: "club_reviews",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CommentHiddenReason",
                table: "club_reviews",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "RepliedAtUtc",
                table: "club_reviews",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "RepliedByStaffUserId",
                table: "club_reviews",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Reply",
                table: "club_reviews",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CommentHiddenAtUtc",
                table: "club_reviews");

            migrationBuilder.DropColumn(
                name: "CommentHiddenByStaffUserId",
                table: "club_reviews");

            migrationBuilder.DropColumn(
                name: "CommentHiddenReason",
                table: "club_reviews");

            migrationBuilder.DropColumn(
                name: "RepliedAtUtc",
                table: "club_reviews");

            migrationBuilder.DropColumn(
                name: "RepliedByStaffUserId",
                table: "club_reviews");

            migrationBuilder.DropColumn(
                name: "Reply",
                table: "club_reviews");
        }
    }
}

using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AFK4.Platform.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPersonFriendships : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "ShowsPresenceToFriends",
                table: "platform_persons",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.CreateTable(
                name: "person_friendships",
                columns: table => new
                {
                    PersonFriendshipId = table.Column<Guid>(type: "uuid", nullable: false),
                    RequesterPersonId = table.Column<Guid>(type: "uuid", nullable: false),
                    AddresseePersonId = table.Column<Guid>(type: "uuid", nullable: false),
                    State = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    RespondedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_person_friendships", x => x.PersonFriendshipId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_person_friendships_AddresseePersonId",
                table: "person_friendships",
                column: "AddresseePersonId");

            migrationBuilder.CreateIndex(
                name: "IX_person_friendships_RequesterPersonId_AddresseePersonId",
                table: "person_friendships",
                columns: new[] { "RequesterPersonId", "AddresseePersonId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "person_friendships");

            migrationBuilder.DropColumn(
                name: "ShowsPresenceToFriends",
                table: "platform_persons");
        }
    }
}

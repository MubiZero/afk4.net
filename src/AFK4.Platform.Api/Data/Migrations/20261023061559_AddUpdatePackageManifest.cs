using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AFK4.Platform.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddUpdatePackageManifest : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ManifestSha256",
                table: "update_packages",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ManifestSignature",
                table: "update_packages",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ManifestSignatureAlgorithm",
                table: "update_packages",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "ManifestSizeBytes",
                table: "update_packages",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ManifestUri",
                table: "update_packages",
                type: "character varying(1024)",
                maxLength: 1024,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ManifestSha256",
                table: "update_packages");

            migrationBuilder.DropColumn(
                name: "ManifestSignature",
                table: "update_packages");

            migrationBuilder.DropColumn(
                name: "ManifestSignatureAlgorithm",
                table: "update_packages");

            migrationBuilder.DropColumn(
                name: "ManifestSizeBytes",
                table: "update_packages");

            migrationBuilder.DropColumn(
                name: "ManifestUri",
                table: "update_packages");
        }
    }
}

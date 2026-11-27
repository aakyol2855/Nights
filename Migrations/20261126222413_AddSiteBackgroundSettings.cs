using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nights.Migrations
{
    /// <inheritdoc />
    public partial class AddSiteBackgroundSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BackgroundMode",
                table: "SiteSettings",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "DarkBackgroundColor",
                table: "SiteSettings",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DarkBackgroundImageUrl",
                table: "SiteSettings",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LightBackgroundColor",
                table: "SiteSettings",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LightBackgroundImageUrl",
                table: "SiteSettings",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BackgroundMode",
                table: "SiteSettings");

            migrationBuilder.DropColumn(
                name: "DarkBackgroundColor",
                table: "SiteSettings");

            migrationBuilder.DropColumn(
                name: "DarkBackgroundImageUrl",
                table: "SiteSettings");

            migrationBuilder.DropColumn(
                name: "LightBackgroundColor",
                table: "SiteSettings");

            migrationBuilder.DropColumn(
                name: "LightBackgroundImageUrl",
                table: "SiteSettings");
        }
    }
}

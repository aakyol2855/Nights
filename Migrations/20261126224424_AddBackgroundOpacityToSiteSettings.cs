using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nights.Migrations
{
    /// <inheritdoc />
    public partial class AddBackgroundOpacityToSiteSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "BackgroundOpacity",
                table: "SiteSettings",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BackgroundOpacity",
                table: "SiteSettings");
        }
    }
}

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nights.Migrations
{
    /// <inheritdoc />
    public partial class AddBackgroundBlurToSiteSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "BackgroundBlur",
                table: "SiteSettings",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BackgroundBlur",
                table: "SiteSettings");
        }
    }
}

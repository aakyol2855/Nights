using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nights.Migrations
{
    /// <inheritdoc />
    public partial class AddSiteSettingsTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SiteSettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    GithubUrl = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LinkedinUrl = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ContactEmail = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    QuoteText = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    QuoteAuthor = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LogoText = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LogoSubText = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CustomLogoUrl = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SiteSettings", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SiteSettings");
        }
    }
}

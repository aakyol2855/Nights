using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nights.Migrations
{
    /// <inheritdoc />
    public partial class AddNoteSoftDeleteFlag : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Notes_IsPinned_CreatedAt",
                table: "Notes");

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "Notes",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_Notes_IsDeleted_IsPinned_CreatedAt",
                table: "Notes",
                columns: new[] { "IsDeleted", "IsPinned", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Notes_IsDeleted_IsPinned_CreatedAt",
                table: "Notes");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "Notes");

            migrationBuilder.CreateIndex(
                name: "IX_Notes_IsPinned_CreatedAt",
                table: "Notes",
                columns: new[] { "IsPinned", "CreatedAt" });
        }
    }
}

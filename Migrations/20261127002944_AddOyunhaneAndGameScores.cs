using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nights.Migrations
{
    /// <inheritdoc />
    public partial class AddOyunhaneAndGameScores : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "GameScores",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    GameSlug = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Difficulty = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    PlayerDisplayName = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Score = table.Column<int>(type: "int", nullable: false),
                    DurationSeconds = table.Column<int>(type: "int", nullable: false),
                    Mistakes = table.Column<int>(type: "int", nullable: false),
                    AchievedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GameScores", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_GameScores_GameSlug_Difficulty_IsDeleted_Score_DurationSeconds",
                table: "GameScores",
                columns: new[] { "GameSlug", "Difficulty", "IsDeleted", "Score", "DurationSeconds" });

            migrationBuilder.CreateIndex(
                name: "IX_GameScores_UserId_GameSlug_Difficulty_IsDeleted",
                table: "GameScores",
                columns: new[] { "UserId", "GameSlug", "Difficulty", "IsDeleted" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "GameScores");
        }
    }
}

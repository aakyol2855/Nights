using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nights.Migrations
{
    /// <inheritdoc />
    public partial class AddDockerDemoOrchestration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ContainerInternalPort",
                table: "Projects",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "DockerImageName",
                table: "Projects",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EnvironmentVariables",
                table: "Projects",
                type: "nvarchar(4000)",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDemoEnabled",
                table: "Projects",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "MarkdownGuide",
                table: "Projects",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Slug",
                table: "Projects",
                type: "nvarchar(120)",
                maxLength: 120,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "UseSharedDemoNetwork",
                table: "Projects",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "ActiveDemoSessions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProjectId = table.Column<int>(type: "int", nullable: false),
                    Slug = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    ContainerId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    ContainerName = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    ImageName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    HostPort = table.Column<int>(type: "int", nullable: false),
                    ContainerPort = table.Column<int>(type: "int", nullable: false),
                    StartedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastActivityUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    StoppedReason = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    StoppedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ActiveDemoSessions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ActiveDemoSessions_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Projects_Slug",
                table: "Projects",
                column: "Slug");

            migrationBuilder.CreateIndex(
                name: "IX_ActiveDemoSessions_IsActive",
                table: "ActiveDemoSessions",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_ActiveDemoSessions_ProjectId",
                table: "ActiveDemoSessions",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_ActiveDemoSessions_Slug",
                table: "ActiveDemoSessions",
                column: "Slug");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ActiveDemoSessions");

            migrationBuilder.DropIndex(
                name: "IX_Projects_Slug",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "ContainerInternalPort",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "DockerImageName",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "EnvironmentVariables",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "IsDemoEnabled",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "MarkdownGuide",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "Slug",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "UseSharedDemoNetwork",
                table: "Projects");
        }
    }
}

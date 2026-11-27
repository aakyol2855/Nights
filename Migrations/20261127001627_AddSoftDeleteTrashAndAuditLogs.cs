using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nights.Migrations
{
    /// <inheritdoc />
    public partial class AddSoftDeleteTrashAndAuditLogs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAtUtc",
                table: "Projects",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeletedBy",
                table: "Projects",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "Projects",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAtUtc",
                table: "Notes",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeletedBy",
                table: "Notes",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAtUtc",
                table: "Memories",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeletedBy",
                table: "Memories",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "Memories",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "SystemAuditLogs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TimestampUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Category = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    LogLevel = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Username = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    TargetEntityType = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true),
                    TargetEntityId = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    Summary = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    IpAddress = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Component = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SystemAuditLogs", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SystemAuditLogs_Category",
                table: "SystemAuditLogs",
                column: "Category");

            migrationBuilder.CreateIndex(
                name: "IX_SystemAuditLogs_LogLevel",
                table: "SystemAuditLogs",
                column: "LogLevel");

            migrationBuilder.CreateIndex(
                name: "IX_SystemAuditLogs_TimestampUtc",
                table: "SystemAuditLogs",
                column: "TimestampUtc");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SystemAuditLogs");

            migrationBuilder.DropColumn(
                name: "DeletedAtUtc",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "DeletedBy",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "DeletedAtUtc",
                table: "Notes");

            migrationBuilder.DropColumn(
                name: "DeletedBy",
                table: "Notes");

            migrationBuilder.DropColumn(
                name: "DeletedAtUtc",
                table: "Memories");

            migrationBuilder.DropColumn(
                name: "DeletedBy",
                table: "Memories");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "Memories");
        }
    }
}

using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MitLicenseCenter.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class MLC076DatabaseBackups : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DatabaseBackups",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InfobaseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DatabaseServer = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    DatabaseName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    RequestedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    RequestedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    StartedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CompletedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    FilePath = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    FileSizeBytes = table.Column<long>(type: "bigint", nullable: true),
                    FailureReason = table.Column<int>(type: "int", nullable: false),
                    ErrorMessage = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DatabaseBackups", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DatabaseBackups_DatabaseServer_DatabaseName_Status",
                schema: "dbo",
                table: "DatabaseBackups",
                columns: new[] { "DatabaseServer", "DatabaseName", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_DatabaseBackups_RequestedAtUtc",
                schema: "dbo",
                table: "DatabaseBackups",
                column: "RequestedAtUtc");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DatabaseBackups",
                schema: "dbo");
        }
    }
}

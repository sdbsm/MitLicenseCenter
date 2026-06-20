using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MitLicenseCenter.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class MLC230TechLogCollections : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TechLogCollections",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    StartedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    StoppedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    StopReason = table.Column<int>(type: "int", nullable: true),
                    Scenario = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    InfobaseProcessName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    CollectionDirectory = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    ConfigMarker = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TechLogCollections", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TechLogCollections_StartedAtUtc",
                schema: "dbo",
                table: "TechLogCollections",
                column: "StartedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_TechLogCollections_Status",
                schema: "dbo",
                table: "TechLogCollections",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TechLogCollections",
                schema: "dbo");
        }
    }
}

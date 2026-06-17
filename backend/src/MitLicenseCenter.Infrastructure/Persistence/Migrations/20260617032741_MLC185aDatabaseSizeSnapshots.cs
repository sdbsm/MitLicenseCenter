using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MitLicenseCenter.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class MLC185aDatabaseSizeSnapshots : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DatabaseSizeSnapshots",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DatabaseName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    SnapshotAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DataBytes = table.Column<long>(type: "bigint", nullable: false),
                    LogBytes = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DatabaseSizeSnapshots", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DatabaseSizeSnapshots_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalSchema: "dbo",
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DatabaseSizeSnapshots_DatabaseName_SnapshotAtUtc",
                schema: "dbo",
                table: "DatabaseSizeSnapshots",
                columns: new[] { "DatabaseName", "SnapshotAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_DatabaseSizeSnapshots_TenantId_SnapshotAtUtc",
                schema: "dbo",
                table: "DatabaseSizeSnapshots",
                columns: new[] { "TenantId", "SnapshotAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DatabaseSizeSnapshots",
                schema: "dbo");
        }
    }
}

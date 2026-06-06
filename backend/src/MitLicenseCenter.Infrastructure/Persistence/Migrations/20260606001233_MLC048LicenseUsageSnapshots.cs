using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MitLicenseCenter.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class MLC048LicenseUsageSnapshots : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "LicenseUsageSnapshots",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    BucketStartUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ConsumedMin = table.Column<int>(type: "int", nullable: false),
                    ConsumedMax = table.Column<int>(type: "int", nullable: false),
                    ConsumedAvg = table.Column<double>(type: "float", nullable: false),
                    Limit = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LicenseUsageSnapshots", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LicenseUsageSnapshots_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalSchema: "dbo",
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LicenseUsageSnapshots_TenantId_BucketStartUtc",
                schema: "dbo",
                table: "LicenseUsageSnapshots",
                columns: new[] { "TenantId", "BucketStartUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LicenseUsageSnapshots",
                schema: "dbo");
        }
    }
}

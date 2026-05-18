using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MitLicenseCenter.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Stage2InfobasesPublications : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Infobases",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ClusterInfobaseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DatabaseServer = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    DatabaseName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Infobases", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Infobases_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalSchema: "dbo",
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Publications",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InfobaseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SiteName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    VirtualPath = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    PlatformVersion = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    EnableOData = table.Column<bool>(type: "bit", nullable: false),
                    EnableHttpServices = table.Column<bool>(type: "bit", nullable: false),
                    VrdCustomXml = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Publications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Publications_Infobases_InfobaseId",
                        column: x => x.InfobaseId,
                        principalSchema: "dbo",
                        principalTable: "Infobases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Infobases_TenantId_Name",
                schema: "dbo",
                table: "Infobases",
                columns: new[] { "TenantId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Publications_InfobaseId",
                schema: "dbo",
                table: "Publications",
                column: "InfobaseId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Publications",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "Infobases",
                schema: "dbo");
        }
    }
}

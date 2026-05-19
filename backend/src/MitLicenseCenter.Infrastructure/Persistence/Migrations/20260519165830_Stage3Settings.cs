using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MitLicenseCenter.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Stage3Settings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Settings",
                schema: "dbo",
                columns: table => new
                {
                    Key = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ValueText = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Value = table.Column<byte[]>(type: "varbinary(max)", nullable: true),
                    IsSecret = table.Column<bool>(type: "bit", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Settings", x => x.Key);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Settings",
                schema: "dbo");
        }
    }
}

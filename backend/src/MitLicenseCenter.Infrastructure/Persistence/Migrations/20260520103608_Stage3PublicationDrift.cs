using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MitLicenseCenter.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Stage3PublicationDrift : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "LastDriftCheckAt",
                schema: "dbo",
                table: "Publications",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LastDriftDetails",
                schema: "dbo",
                table: "Publications",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "LastDriftStatus",
                schema: "dbo",
                table: "Publications",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LastDriftCheckAt",
                schema: "dbo",
                table: "Publications");

            migrationBuilder.DropColumn(
                name: "LastDriftDetails",
                schema: "dbo",
                table: "Publications");

            migrationBuilder.DropColumn(
                name: "LastDriftStatus",
                schema: "dbo",
                table: "Publications");
        }
    }
}

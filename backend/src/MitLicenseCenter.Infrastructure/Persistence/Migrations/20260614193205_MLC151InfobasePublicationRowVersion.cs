using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MitLicenseCenter.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class MLC151InfobasePublicationRowVersion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                schema: "dbo",
                table: "Publications",
                type: "rowversion",
                rowVersion: true,
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                schema: "dbo",
                table: "Infobases",
                type: "rowversion",
                rowVersion: true,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RowVersion",
                schema: "dbo",
                table: "Publications");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                schema: "dbo",
                table: "Infobases");
        }
    }
}

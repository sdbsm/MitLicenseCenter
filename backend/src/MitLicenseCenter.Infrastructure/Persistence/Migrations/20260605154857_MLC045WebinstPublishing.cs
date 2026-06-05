using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MitLicenseCenter.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class MLC045WebinstPublishing : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // MLC-045: webinst-публикация + смена платформы через web.config, отказ от
            // drift-enforcement. OData/HTTP/VrdCustomXml убраны из панели; drift-поля
            // переосмыслены в read-only статус публикации (Unknown/Published/NotPublished/Error).

            migrationBuilder.DropColumn(
                name: "EnableHttpServices",
                schema: "dbo",
                table: "Publications");

            migrationBuilder.DropColumn(
                name: "EnableOData",
                schema: "dbo",
                table: "Publications");

            migrationBuilder.DropColumn(
                name: "VrdCustomXml",
                schema: "dbo",
                table: "Publications");

            migrationBuilder.DropColumn(
                name: "LastDriftDetails",
                schema: "dbo",
                table: "Publications");

            // Drift-поля → read-only статус-поля. LastDriftStatus(0..3 = drift-семантика)
            // и LastDriftCheckAt относились к удалённой модели — переименовываем колонки и
            // СБРАСЫВАЕМ значения (статус → Unknown=0, дата → NULL): под новой моделью
            // публикация считается «ещё не проверенной», следующий refresh заполнит факт.
            migrationBuilder.RenameColumn(
                name: "LastDriftStatus",
                schema: "dbo",
                table: "Publications",
                newName: "LastCheckStatus");

            migrationBuilder.RenameColumn(
                name: "LastDriftCheckAt",
                schema: "dbo",
                table: "Publications",
                newName: "LastCheckAt");

            // Происхождение публикации (Unknown=0 для всех существующих строк).
            migrationBuilder.AddColumn<int>(
                name: "Source",
                schema: "dbo",
                table: "Publications",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "LastCheckDetails",
                schema: "dbo",
                table: "Publications",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.Sql(
                "UPDATE [dbo].[Publications] SET [LastCheckStatus] = 0, [LastCheckAt] = NULL;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LastCheckDetails",
                schema: "dbo",
                table: "Publications");

            migrationBuilder.DropColumn(
                name: "Source",
                schema: "dbo",
                table: "Publications");

            migrationBuilder.RenameColumn(
                name: "LastCheckStatus",
                schema: "dbo",
                table: "Publications",
                newName: "LastDriftStatus");

            migrationBuilder.RenameColumn(
                name: "LastCheckAt",
                schema: "dbo",
                table: "Publications",
                newName: "LastDriftCheckAt");

            migrationBuilder.AddColumn<string>(
                name: "LastDriftDetails",
                schema: "dbo",
                table: "Publications",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "EnableOData",
                schema: "dbo",
                table: "Publications",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "EnableHttpServices",
                schema: "dbo",
                table: "Publications",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "VrdCustomXml",
                schema: "dbo",
                table: "Publications",
                type: "nvarchar(max)",
                nullable: true);
        }
    }
}

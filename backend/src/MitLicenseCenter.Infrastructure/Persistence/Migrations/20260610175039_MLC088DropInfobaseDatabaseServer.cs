using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MitLicenseCenter.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class MLC088DropInfobaseDatabaseServer : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // MLC-088 (single-host): серверное поле на каждой инфобазе избыточно — SQL-инстанс
            // задан одной настройкой Sql.Server. Значения во всех строках одинаковы и
            // восстановимы из настройки, поэтому дроп без переноса данных — потери нет.
            migrationBuilder.DropColumn(
                name: "DatabaseServer",
                schema: "dbo",
                table: "Infobases");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Откат воссоздаёт колонку пустой ("") — единый сервер берётся из настройки
            // Sql.Server; пер-строчные значения исторически совпадали с ней.
            migrationBuilder.AddColumn<string>(
                name: "DatabaseServer",
                schema: "dbo",
                table: "Infobases",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");
        }
    }
}

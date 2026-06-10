using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MitLicenseCenter.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class MLC087RenameSqlServerSetting : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // MLC-087 (single-host): ключ Defaults.DatabaseServer переименован в Sql.Server —
            // он перестал быть «дефолтом для форм» и стал единственным местом, где задан
            // SQL-инстанс. Значение сохраняем (UPDATE, не пересоздание); описание ровняем на
            // новый каталог. На свежей БД строки ещё нет (сеется позже под новым ключом) —
            // UPDATE затронет 0 строк, что корректно.
            migrationBuilder.Sql(@"
UPDATE dbo.Settings
SET [Key] = 'Sql.Server',
    [Description] = N'SQL-инстанс, на котором живут базы клиентов (например, sql.local или (local)).'
WHERE [Key] = 'Defaults.DatabaseServer';
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
UPDATE dbo.Settings
SET [Key] = 'Defaults.DatabaseServer',
    [Description] = N'SQL-сервер по умолчанию для новых инфобаз (например, sql.local или (local)).'
WHERE [Key] = 'Sql.Server';
");
        }
    }
}

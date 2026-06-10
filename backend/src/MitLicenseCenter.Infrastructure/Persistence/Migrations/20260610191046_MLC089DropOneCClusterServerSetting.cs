using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MitLicenseCenter.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class MLC089DropOneCClusterServerSetting : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // MLC-089 (single-host): ключ OneC.Cluster.Server снят с каталога
            // SettingDefinitions — адрес кластера для webinst деривируется из
            // OneC.RAS.Endpoint (кластер и RAS на одном хосте). Чистим осиротевшую
            // row в БД. На свежей БД строки нет — DELETE затронет 0 строк, что корректно.
            migrationBuilder.Sql(@"
DELETE FROM dbo.Settings
WHERE [Key] = 'OneC.Cluster.Server';
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            throw new System.NotSupportedException(
                "Миграция MLC089DropOneCClusterServerSetting — roll-forward only. " +
                "Ключ OneC.Cluster.Server снят с каталога SettingDefinitions (single-host); " +
                "его пересоздание — часть отдельного решения, не часть отката миграции.");
        }
    }
}

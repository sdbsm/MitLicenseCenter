using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MitLicenseCenter.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class DropIisServiceAccountSetting : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Удаляем informational-ключ IIS.ServiceAccount.UserName: его никто не
            // читал (ни impersonation, ни ACL, ни auth) — чисто справочное поле.
            // Снят с каталога SettingDefinitions; чистим осиротевшую row в БД.
            migrationBuilder.Sql(@"
DELETE FROM dbo.Settings
WHERE [Key] = 'IIS.ServiceAccount.UserName';
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            throw new System.NotSupportedException(
                "Миграция DropIisServiceAccountSetting — roll-forward only. " +
                "Ключ IIS.ServiceAccount.UserName был informational и не использовался; " +
                "его пересоздание — часть отдельного решения, не часть отката миграции.");
        }
    }
}

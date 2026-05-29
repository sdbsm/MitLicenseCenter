using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MitLicenseCenter.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Stage5DropRestClusterSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Stage 5 PR 5.1, ADR-16: удаляем 4 ключа Settings, относящиеся к
            // REST-адаптеру и Polly circuit-breaker'у. OneC.Cluster.AdminUser /
            // OneC.Cluster.AdminPassword остаются — они переиспользуются rac.exe
            // RAS-адаптером (флаги --cluster-user / --cluster-pwd, см. ADR-3.3).
            migrationBuilder.Sql(@"
DELETE FROM dbo.Settings
WHERE [Key] IN (
    'OneC.Cluster.RestApiUrl',
    'OneC.Cluster.RestApiTimeoutSeconds',
    'CircuitBreaker.ProbeIntervalSeconds',
    'CircuitBreaker.FailureCount');
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            throw new System.NotSupportedException(
                "Миграция Stage5DropRestClusterSettings — roll-forward only. " +
                "Возврат REST-адаптера 1С Cluster требует явной отмены ADR-16; " +
                "пересоздание ключей Settings — часть этой отмены, не часть миграции.");
        }
    }
}

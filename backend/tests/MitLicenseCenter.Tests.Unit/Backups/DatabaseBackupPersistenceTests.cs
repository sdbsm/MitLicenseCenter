using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using MitLicenseCenter.Application.Backups;
using MitLicenseCenter.Infrastructure.Reporting;
using MitLicenseCenter.Tests.Unit.Endpoints;
using Xunit;

namespace MitLicenseCenter.Tests.Unit.Backups;

// MLC-076 (ADR-27): persistence-инварианты сущности учёта бэкапов на реальном провайдере
// (SQLite-in-memory, схема из той же модели). Канон — AppDbContext.OnModelCreating.
public sealed class DatabaseBackupPersistenceTests
{
    private static readonly string[] RequestedAtIndex = ["RequestedAtUtc"];
    private static readonly string[] PumpIndex = ["DatabaseServer", "DatabaseName", "Status"];

    [Fact]
    public void Entity_is_mapped_to_dbo_table_with_expected_indexes()
    {
        using var sqlite = TestHelpers.SqliteTestDb.Create();
        using var db = sqlite.NewContext();

        var entity = db.Model.FindEntityType(typeof(DatabaseBackup))!;
        entity.GetTableName().Should().Be("DatabaseBackups");
        entity.GetSchema().Should().Be("dbo");
        entity.GetIndexes().Should().Contain(
            i => i.Properties.Select(p => p.Name).SequenceEqual(RequestedAtIndex));
        entity.GetIndexes().Should().Contain(
            i => i.Properties.Select(p => p.Name).SequenceEqual(PumpIndex));
        entity.GetForeignKeys().Should().BeEmpty(
            "InfobaseId — простой Guid без FK: запись бэкапа переживает удаление инфобазы " +
            "(образец LicenseUsageSnapshot/PerfRecording)");
    }

    [Fact]
    public void Row_with_nonexistent_infobase_id_is_accepted_and_roundtrips_enums()
    {
        using var sqlite = TestHelpers.SqliteTestDb.Create();

        var id = Guid.NewGuid();
        using (var seed = sqlite.NewContext())
        {
            seed.DatabaseBackups.Add(new DatabaseBackup
            {
                Id = id,
                InfobaseId = Guid.NewGuid(), // инфобазы с таким Id нет — FK отсутствует намеренно
                DatabaseServer = "sql.local",
                DatabaseName = "acme_bp",
                Status = BackupStatus.Failed,
                RequestedBy = "operator",
                RequestedAtUtc = DateTime.UtcNow,
                StartedAtUtc = DateTime.UtcNow,
                CompletedAtUtc = DateTime.UtcNow,
                FailureReason = BackupFailureReason.InsufficientSpace,
                ErrorMessage = "Недостаточно места.",
            });

            var act = () => seed.SaveChanges();
            act.Should().NotThrow("FK на Infobase нет — строка живёт независимо от инфобазы");
        }

        using var verify = sqlite.NewContext();
        var saved = verify.DatabaseBackups.Single(x => x.Id == id);
        saved.Status.Should().Be(BackupStatus.Failed);
        saved.FailureReason.Should().Be(BackupFailureReason.InsufficientSpace);
        saved.FilePath.Should().BeNull();
        saved.FileSizeBytes.Should().BeNull();
    }
}

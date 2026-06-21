using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using MitLicenseCenter.Domain.TechLog;
using MitLicenseCenter.Tests.Unit.Endpoints;
using Xunit;

namespace MitLicenseCenter.Tests.Unit.TechLog;

// MLC-237: optimistic-concurrency токен Investigation.RowVersion на РЕАЛЬНОМ провайдере (SQLite, НЕ
// InMemory — InMemory не энфорсит rowversion-токены, прецедент MLC-163). У TechLogCollection токена не
// было; здесь дело получает самостоятельные мутации оркестрацией (MLC-238) + конкуренцию сторожа авто-
// стопа с ручным снятием → targeted-UPDATE двух контекстов должен ловиться DbUpdateConcurrencyException,
// а не молча терять запись.
//
// SQL Server генерирует/бампит `rowversion` сам; SQLite — нет (маппит на BLOB и не трогает на UPDATE),
// поэтому семантику воспроизводим триггером AFTER-UPDATE, который меняет RowVersion на каждом апдейте —
// ровно как `rowversion` на SQL Server. EF включает токен в `WHERE RowVersion = @original`; второй
// SaveChanges затрагивает 0 строк → DbUpdateConcurrencyException. Это и есть прод-семантика 409.
public sealed class InvestigationConcurrencyTests
{
    [Fact]
    public void Concurrent_targeted_update_throws_DbUpdateConcurrencyException()
    {
        using var sqlite = TestHelpers.SqliteTestDb.Create();
        InstallRowVersionTrigger(sqlite.Connection);

        var id = Guid.NewGuid();
        using (var seed = sqlite.NewContext())
        {
            seed.Investigations.Add(new Investigation
            {
                Id = id,
                Scenario = InvestigationScenario.Locks,
                Status = InvestigationStatus.Collecting,
                StartedAtUtc = DateTime.UtcNow,
                StartedBy = "admin",
                CollectionDirectory = @"C:\techlog",
                ConfigMarker = "managed by MitLicenseCenter",
                // SQLite не генерирует токен на INSERT — задаём явный стартовый, дальше его бампит триггер.
                RowVersion = [0, 0, 0, 0, 0, 0, 0, 1],
            });
            seed.SaveChanges();
        }

        // Два контекста читают одно дело с одинаковым RowVersion (как сторож авто-стопа и ручное снятие).
        using var ctx1 = sqlite.NewContext();
        using var ctx2 = sqlite.NewContext();
        var row1 = ctx1.Investigations.Single(x => x.Id == id);
        var row2 = ctx2.Investigations.Single(x => x.Id == id);

        // Первый targeted-UPDATE проходит; триггер бампит RowVersion в БД.
        row1.Status = InvestigationStatus.Completed;
        row1.StopReason = InvestigationStopReason.Manual;
        ctx1.SaveChanges();

        // Второй UPDATE со СТАРЫМ токеном затрагивает 0 строк → конфликт.
        row2.Status = InvestigationStatus.Completed;
        row2.StopReason = InvestigationStopReason.TimeLimit;
        var act = () => ctx2.SaveChanges();

        act.Should().Throw<DbUpdateConcurrencyException>(
            "конкурентный targeted-UPDATE с устаревшим RowVersion должен ловиться, а не молча терять запись");
    }

    // Триггер, эмулирующий серверный rowversion: после каждого UPDATE строки RowVersion получает новое
    // значение (randomblob(8) гарантированно отличается от старого). FOR EACH ROW + AFTER UPDATE.
    private static void InstallRowVersionTrigger(SqliteConnection connection)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            CREATE TRIGGER IF NOT EXISTS trg_Investigations_RowVersion
            AFTER UPDATE OF Status, StopReason, StoppedAtUtc ON Investigations
            FOR EACH ROW
            BEGIN
                UPDATE Investigations SET RowVersion = randomblob(8) WHERE Id = OLD.Id;
            END;
            """;
        cmd.ExecuteNonQuery();
    }
}

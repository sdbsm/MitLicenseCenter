using MitLicenseCenter.Application.Performance;

namespace MitLicenseCenter.Infrastructure.Performance.Testing;

// Заглушка для unit-тестов эндпоинта /performance/sql, которым не нужен живой MSSQL/DMV.
// В production-DI не регистрируется — реальный SqlPerformanceProbe ходит в SQL. Настраивается
// публичным полем Snapshot (правдоподобный «всё спокойно» по умолчанию).
internal sealed class StubSqlPerformanceProbe : ISqlPerformanceProbe
{
    public SqlPerformanceSnapshot Snapshot { get; set; } = new(
        CapturedAtUtc: new DateTime(2026, 6, 9, 12, 0, 0, DateTimeKind.Utc),
        Status: SqlProbeStatus.Ok,
        Measuring: false,
        ActiveRequests: [],
        DatabaseIo: [],
        TopWaits: []);

    public int CaptureCalls { get; private set; }

    public Task<SqlPerformanceSnapshot> CaptureAsync(CancellationToken ct)
    {
        CaptureCalls++;
        return Task.FromResult(Snapshot);
    }
}

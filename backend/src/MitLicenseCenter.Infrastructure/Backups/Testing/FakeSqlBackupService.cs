using MitLicenseCenter.Application.Backups;

namespace MitLicenseCenter.Infrastructure.Backups.Testing;

// Программируемый фейк порта бэкапа для unit-тестов (образец StubSqlPerformanceProbe):
// настраиваемые результаты + запись аргументов вызовов — на нём строятся тесты
// оркестратора/насоса MLC-077 (переходы статусов, keep-latest, потолок параллельных).
// В production-DI не регистрируется — реальный SqlBackupAdapter ходит в SQL.
// Опциональный BackupGate позволяет тесту «подвесить» бэкап (проверки потолка/per-db
// эксклюзии требуют долгоиграющей операции). Списки вызовов — под lock: оркестратор
// гоняет бэкапы параллельно по замыслу.
internal sealed class FakeSqlBackupService : ISqlBackupService
{
    private readonly object _gate = new();
    private readonly List<BackupCall> _backupCalls = [];
    private readonly List<DeleteCall> _deleteCalls = [];

    public SqlBackupResult NextBackupResult { get; set; } = new(
        Succeeded: true,
        Reason: BackupFailureReason.None,
        FilePath: @"D:\Backups\db\db_20260609_120000.bak",
        FileSizeBytes: 1024,
        ErrorMessage: null);

    public SqlDeleteResult NextDeleteResult { get; set; } = new(Succeeded: true, ErrorMessage: null);

    // Тест может задать «ворота», на которых BackupAsync повиснет до сигнала.
    public TaskCompletionSource? BackupGate { get; set; }

    public IReadOnlyList<BackupCall> BackupCalls
    {
        get { lock (_gate) { return _backupCalls.ToList(); } }
    }

    public IReadOnlyList<DeleteCall> DeleteCalls
    {
        get { lock (_gate) { return _deleteCalls.ToList(); } }
    }

    public async Task<SqlBackupResult> BackupAsync(
        string server, string databaseName, string folderRoot, int safetyMarginMb, CancellationToken ct)
    {
        lock (_gate)
        {
            _backupCalls.Add(new BackupCall(server, databaseName, folderRoot, safetyMarginMb));
        }

        if (BackupGate is { } gate)
        {
            await gate.Task.WaitAsync(ct).ConfigureAwait(false);
        }

        return NextBackupResult;
    }

    public Task<SqlDeleteResult> DeleteBackupsOlderThanAsync(
        string server, string folderPath, DateTime cutoffUtc, CancellationToken ct)
    {
        lock (_gate)
        {
            _deleteCalls.Add(new DeleteCall(server, folderPath, cutoffUtc));
        }

        return Task.FromResult(NextDeleteResult);
    }

    internal sealed record BackupCall(string Server, string DatabaseName, string FolderRoot, int SafetyMarginMb);

    internal sealed record DeleteCall(string Server, string FolderPath, DateTime CutoffUtc);
}

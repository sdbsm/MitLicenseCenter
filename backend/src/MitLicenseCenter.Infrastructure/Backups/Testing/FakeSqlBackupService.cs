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
    private readonly List<EstimateCall> _estimateCalls = [];
    private readonly List<DeleteCall> _deleteCalls = [];
    private readonly List<FilesExistCall> _filesExistCalls = [];

    public SqlBackupResult NextBackupResult { get; set; } = new(
        Succeeded: true,
        Reason: BackupFailureReason.None,
        FilePath: @"D:\Backups\db\db_20260609_120000.bak",
        FileSizeBytes: 1024,
        ErrorMessage: null);

    public SqlDeleteResult NextDeleteResult { get; set; } = new(Succeeded: true, ErrorMessage: null);

    // MLC-183: управляемый результат предпоказа оценки для EstimateAsync. По умолчанию —
    // «места достаточно» (обе цифры заполнены, Sufficient=true).
    public SqlBackupEstimate NextEstimate { get; set; } = new(
        EstimatedSizeBytes: 512L * 1024 * 1024,
        FreeSpaceBytes: 100L * 1024 * 1024 * 1024,
        SafetyMarginBytes: 2048L * 1024 * 1024,
        Sufficient: true,
        Reason: BackupFailureReason.None);

    // Тест может задать «ворота», на которых BackupAsync повиснет до сигнала.
    public TaskCompletionSource? BackupGate { get; set; }

    // MLC-178: управляемый набор «существующих на диске» путей для FilesExistAsync. По
    // умолчанию null = «сервис не смог» (вернёт пустой словарь = «не знаем»). Задав набор,
    // тест получает словарь по запрошенным путям: путь в наборе → true, иначе → false.
    public HashSet<string>? ExistingFilePaths { get; set; }

    public IReadOnlyList<BackupCall> BackupCalls
    {
        get { lock (_gate) { return _backupCalls.ToList(); } }
    }

    public IReadOnlyList<EstimateCall> EstimateCalls
    {
        get { lock (_gate) { return _estimateCalls.ToList(); } }
    }

    public IReadOnlyList<DeleteCall> DeleteCalls
    {
        get { lock (_gate) { return _deleteCalls.ToList(); } }
    }

    public IReadOnlyList<FilesExistCall> FilesExistCalls
    {
        get { lock (_gate) { return _filesExistCalls.ToList(); } }
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

    public Task<SqlBackupEstimate> EstimateAsync(
        string server, string databaseName, string folderRoot, int safetyMarginMb, CancellationToken ct)
    {
        lock (_gate)
        {
            _estimateCalls.Add(new EstimateCall(server, databaseName, folderRoot, safetyMarginMb));
        }

        return Task.FromResult(NextEstimate);
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

    public Task<IReadOnlyDictionary<string, bool>> FilesExistAsync(
        string server, IReadOnlyCollection<string> paths, CancellationToken ct)
    {
        lock (_gate)
        {
            _filesExistCalls.Add(new FilesExistCall(server, paths.ToList()));
        }

        // Пустой запрос — реальный адаптер не ходит в SQL.
        if (paths.Count == 0 || ExistingFilePaths is null)
        {
            // ExistingFilePaths == null имитирует «сервис не смог» → пустой словарь = «не знаем».
            return Task.FromResult<IReadOnlyDictionary<string, bool>>(
                new Dictionary<string, bool>(StringComparer.Ordinal));
        }

        var result = new Dictionary<string, bool>(StringComparer.Ordinal);
        foreach (var path in paths)
        {
            result[path] = ExistingFilePaths.Contains(path);
        }

        return Task.FromResult<IReadOnlyDictionary<string, bool>>(result);
    }

    internal sealed record BackupCall(string Server, string DatabaseName, string FolderRoot, int SafetyMarginMb);

    internal sealed record EstimateCall(string Server, string DatabaseName, string FolderRoot, int SafetyMarginMb);

    internal sealed record DeleteCall(string Server, string FolderPath, DateTime CutoffUtc);

    internal sealed record FilesExistCall(string Server, IReadOnlyList<string> Paths);
}

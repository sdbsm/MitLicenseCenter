using MitLicenseCenter.Domain.TechLog;

namespace MitLicenseCenter.Application.TechLog;

// Сервис жизненного цикла сбора ТЖ режима «Расследование» (MLC-230, ADR-57/58). Зеркаль
// IPerfRecordingService: singleton-реализация держит активное дело + сериализует операции через
// SemaphoreSlim, БД — через IServiceScopeFactory, время — через TimeProvider. В отличие от
// «Записи» быстродействия здесь побочный эффект — файл logcfg.xml в conf платформы: установка
// генерирует целевой конфиг и пишет его (с бэкапом исходного), снятие восстанавливает исходный.
// Идемпотентность (60_SAFETY №6): повторная установка/снятие не ломают состояние. Аудит —
// 806/807/808 (новые frozen-int значения). Окно/авто-стоп/лимит диска/один-активный/orphan-recovery —
// задача MLC-231; здесь только установка, снятие и стартовая сверка файла сторожем.
public interface ITechLogCollectionService
{
    // Ставит целевой ТЖ: проба прав → генерация logcfg → бэкап исходного → запись → дело Active → аудит.
    // AlreadyActive — дело уже идёт (id текущего). NoWriteAccess — нет прав на logcfg.xml (в результате
    // точная команда icacls для оператора). RootNotFound — корень 1С (conf) не найден.
    Task<TechLogStartResult> InstallAsync(
        string startedBy, TechLogScenario scenario, string? infobaseProcessName, CancellationToken ct);

    // Снимает активный сбор: восстановление исходного logcfg → дело Completed (reason) → аудит.
    // NotActive — переданный id не является текущим активным делом.
    Task<TechLogStopOutcome> RemoveAsync(Guid collectionId, InvestigationStopReason reason, CancellationToken ct);

    // Сторож на старте (60_SAFETY №5): сверяет фактический logcfg.xml в conf с ожидаемым. Если в conf
    // лежит НАШ logcfg (по маркеру), но нет активного дела в БД (краш ОС оставил «забытый» конфиг) —
    // принудительно восстанавливает исходный и пишет аудит 808. Best-effort, не бросает.
    Task ReconcileOnStartupAsync(CancellationToken ct);

    // Orphan-recovery на старте (60_SAFETY №5, зеркаль PerfRecording.RecoverInterruptedAsync): все
    // дела Status==Active → Interrupted + StoppedAtUtc. После рестарта процесса in-memory стейт потерян,
    // logcfg при этом снимается стартовой сверкой файла (ReconcileOnStartupAsync). ВЫЗЫВАТЬ ДО
    // ReconcileOnStartupAsync: сначала закрыть осиротевшее дело, потом снять «забытый» конфиг.
    // Best-effort, не бросает.
    Task RecoverInterruptedAsync(CancellationToken ct);

    // Сторож активного сбора (60_SAFETY №3/№4, зеркаль PerfRecording.SampleOnceAsync): при активном деле
    // сверяет прошедшее время с TechLog.MaxDurationMinutes (авто-стоп TimeLimit) и РАЗМЕР каталога сбора
    // с TechLog.DiskLimitMb (авто-стоп DiskLimit). No-op, если активного дела нет. Время — TimeProvider,
    // размер — за seam'ом ILogcfgStore (детерминированные тесты). Best-effort, не бросает.
    Task MonitorActiveAsync(CancellationToken ct);

    // Идёт ли сбор прямо сейчас — дешёвая проверка без БД (как HasActiveRecording).
    bool HasActiveCollection { get; }
}

// Исход установки сбора.
public enum TechLogStartOutcome
{
    Started,
    AlreadyActive,
    NoWriteAccess,
    RootNotFound,

    // Свободного места на диске каталога сбора меньше порога TechLog.MinFreeDiskMb (60_SAFETY №3):
    // не стартуем (полный ТЖ забивает диск за минуты — место критичнее обычного, MLC-229/40_TECHLOG §8).
    InsufficientDiskSpace,

    // У аккаунта агента 1С (TechLog.CollectionAgentAccount) нет прав записи на каталог сбора (MLC-247 A2,
    // 41_LOGCFG_SPEC §6): процессы 1С пишут ТЖ под своим аккаунтом — без полных прав журнал не пишется
    // («пустые дела»). Не стартуем; в результате — точная команда icacls для оператора (зеркаль NoWriteAccess).
    // Возникает ТОЛЬКО при заданном аккаунте; пустой аккаунт установку не блокирует (лишь предупреждение в лог).
    AgentNoCollectionAccess,
}

// AlreadyActive → CollectionId указывает на текущее дело. NoWriteAccess / AgentNoCollectionAccess →
// GrantCommand несёт точную команду icacls для оператора (зеркаль RAS-healing). InsufficientDiskSpace →
// Issue несёт причину (сколько свободно / сколько нужно). Иначе оба null/Empty.
public sealed record TechLogStartResult(
    TechLogStartOutcome Outcome,
    Guid CollectionId,
    string? GrantCommand = null,
    string? Issue = null);

// Исход снятия. NotActive — переданный id не является текущим активным делом.
public enum TechLogStopOutcome
{
    Stopped,
    NotActive,
}

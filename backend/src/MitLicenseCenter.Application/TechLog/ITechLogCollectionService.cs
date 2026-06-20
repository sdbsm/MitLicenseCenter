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

    // Снимает активный сбор: восстановление исходного logcfg → дело Stopped (reason) → аудит.
    // NotActive — переданный id не является текущим активным делом.
    Task<TechLogStopOutcome> RemoveAsync(Guid collectionId, TechLogCollectionStopReason reason, CancellationToken ct);

    // Сторож на старте (60_SAFETY №5): сверяет фактический logcfg.xml в conf с ожидаемым. Если в conf
    // лежит НАШ logcfg (по маркеру), но нет активного дела в БД (краш ОС оставил «забытый» конфиг) —
    // принудительно восстанавливает исходный и пишет аудит 808. Best-effort, не бросает.
    Task ReconcileOnStartupAsync(CancellationToken ct);

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
}

// AlreadyActive → CollectionId указывает на текущее дело. NoWriteAccess → GrantCommand несёт точную
// команду icacls для оператора (зеркаль RAS-healing). Иначе оба null/Empty.
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

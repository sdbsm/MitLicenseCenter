namespace MitLicenseCenter.Application.Performance;

// Сервис записи по требованию раздела «Быстродействие» (MLC-070, ADR-26, Фаза 4). В отличие от
// live-снимков (pull без персиста) Recording — ручное вкл/выкл для расследования: singleton держит
// активную запись + по таймеру сэмплит источники (host + топ-виновники 1С/SQL) в БД-таблицу, пока
// оператор не остановит или не сработает авто-стоп (лимит времени/числа сэмплов из настроек). На
// рестарте процесса активная запись закрывается как Interrupted (best-effort, как частичный бакет
// LicenseUsage). Реализация — PerfRecordingService (Infrastructure, singleton); фоновый драйвер —
// PerfRecordingSamplingService. Операторские действия с записью (старт/стоп/удаление) аудируются
// 700-серией (MLC-179); сами сэмплы — телеметрия, вне AuditLog.
public interface IPerfRecordingService
{
    // Стартует новую запись от имени startedBy (логин оператора). Если запись уже идёт —
    // AlreadyActive с id текущей (одна запись за раз — single-node).
    Task<PerfRecordingStartResult> StartAsync(string startedBy, CancellationToken ct);

    // Останавливает активную запись по id (ручной стоп, причина Manual). NotActive — id не совпадает
    // с текущей активной (уже остановлена / не существует / идёт другая запись).
    Task<PerfRecordingStopOutcome> StopAsync(Guid recordingId, CancellationToken ct);

    // Один тик сэмплинга: если запись активна — снимает источники, пишет сэмпл, проверяет авто-стоп.
    // Детерминированный seam для фонового сэмплера и тестов авто-стопа (через TimeProvider). No-op,
    // когда активной записи нет (как RunCycleOnceAsync у hot-tier при пустом списке).
    Task SampleOnceAsync(CancellationToken ct);

    // Восстановление при старте процесса: все записи в статусе Active в БД (осиротевшие рестартом)
    // помечаются Interrupted. Best-effort — partial-сэмплы остаются как есть (ADR-25/26).
    Task RecoverInterruptedAsync(CancellationToken ct);

    // Идёт ли запись прямо сейчас — дешёвая проверка без БД для фонового сэмплера (пропустить тик
    // без работы, как CurrentHotTenants().Count == 0 у hot-tier).
    bool HasActiveRecording { get; }
}

// Исход старта. AlreadyActive — запись уже идёт (одна за раз), RecordingId указывает на неё.
public enum PerfRecordingStartOutcome
{
    Started,
    AlreadyActive,
}

public sealed record PerfRecordingStartResult(PerfRecordingStartOutcome Outcome, Guid RecordingId);

// Исход ручного стопа. NotActive — переданный id не является текущей активной записью.
public enum PerfRecordingStopOutcome
{
    Stopped,
    NotActive,
}

namespace MitLicenseCenter.Application.Clusters;

// Сеанс, полученный с 1С Cluster REST API. ClusterInfobaseId — внутренний GUID
// инфобазы в кластере (совпадает с Infobase.ClusterInfobaseId в нашей схеме).
public sealed record ClusterSession(
    Guid SessionId,
    Guid ClusterInfobaseId,
    string AppId,
    string UserName,
    string Host,
    bool ConsumesLicense,
    DateTime StartedAtUtc);

// Минимальный дескриптор для идемпотентного kill: тройка (InfobaseId, SessionId,
// StartedAt) позволяет убедиться, что убиваем именно тот сеанс, который видели
// в снапшоте, а не новый с тем же session-ID.
public sealed record SessionDescriptor(
    Guid ClusterInfobaseId,
    Guid SessionId,
    string AppId,
    DateTime StartedAtUtc);

public sealed record KillSessionResult(bool Killed, bool AlreadyGone);

public sealed record ClusterPingResult(bool Ok, string? Error);

// Одна инфобаза, обнаруженная в кластере (rac.exe infobase summary list).
public sealed record ClusterInfobase(Guid Id, string Name, string? Description);

// Результат discovery-запроса инфобаз. Available=false означает, что источник
// (кластер/rac.exe) недоступен или не настроен — фронт показывает ручной ввод.
public sealed record ClusterInfobaseDiscoveryResult(
    IReadOnlyList<ClusterInfobase> Infobases,
    bool Available,
    string? Error);

// Перф-срез активного сеанса 1С для раздела «Быстродействие» — «кто грузит сейчас»
// (MLC-066, Фаза 2, ADR-26). Отдельный DTO от ContextSession/ClusterSession (та — для
// kill-пути, 7 полей): здесь живые perf-поля `rac session list`. Все perf-поля **nullable** —
// на иных версиях/конфигурациях платформы их может не быть, парсер «never throws» (ADR-3.3).
// Тип `MemoryCurrent` — знаковый (`long`, не `uint`): rac отдаёт отрицательную текущую память
// в момент GC (видели −1138560 на разведке MLC-063). Process/Connection = null, когда rac
// отдаёт нулевой UUID (сеанс не привязан к рабочему процессу — клиент idle).
public sealed record OneCSessionLoad(
    Guid SessionId,
    int? SessionNumber,
    Guid ClusterInfobaseId,
    string AppId,
    string UserName,
    string Host,
    Guid? Process,
    Guid? Connection,
    long? CpuTimeCurrent,
    long? DurationCurrent,
    long? DurationCurrentDbms,
    long? MemoryCurrent,
    int? BlockedByDbms,
    int? BlockedByLs,
    DateTime? LastActiveAtUtc);

// Рабочий процесс кластера 1С (`rphost`) из `rac process list` (MLC-066). `available-perfomance`
// (так в rac — с опечаткой) — APDEX-подобный индикатор доступной производительности (↓ = деградация,
// при `capacity` 1000); `avg-call-time` — средняя длительность вызова, **дробная** и парсится
// инвариантно (встречается научная нотация `9.99E-05`, MLC-063). Поля nullable — парсер защитный.
public sealed record OneCProcessLoad(
    Guid Process,
    int? Pid,
    int? AvailablePerformance,
    double? AvgCallTime,
    long? MemorySize);

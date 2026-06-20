namespace MitLicenseCenter.Application.TechLog;

// Результат анализа управляемых блокировок 1С уровня платформы (MLC-233, этап B трека
// «Расследование производительности»). Строится анализатором ILockTreeAnalyzer из событий
// TLOCK / TTIMEOUT / TDEADLOCK (40_TECHLOG §5). Это ТОЛЬКО 1С-уровень (менеджер блокировок
// платформы); блокировки уровня СУБД отдельным тегом <dbmslocks/> с полями lkX и не видны
// здесь (не обещать единое дерево всех блокировок — ложная полнота, 40_TECHLOG §5).
//
// Все поля — строки или нормализованные числа, неизменяемые типы (records).
// «Поля-призраки» (40_TECHLOG §7): любое поле события может отсутствовать — аксессоры
// TechLogEvent толерантны, модель несёт null там, где данных нет.

/// <summary>
/// Ребро дерева ожидания управляемых блокировок 1С (источник — событие TLOCK с непустым
/// WaitConnections). «Ждущая» сессия ожидает ресурс, удерживаемый соединением-держателем
/// (40_TECHLOG §8: поле WaitConnections = соединения, которых ждёт текущее).
/// </summary>
public sealed record LockWaitEdge
{
    // Метка времени события TLOCK (строка из поля ts, как пришла из ТЖ).
    public string? Ts { get; init; }

    // SessionID ждущего соединения (40_TECHLOG §8: может быть «с родителем»).
    public string? WaitingSessionId { get; init; }

    // Пользователь ждущего соединения (Usr ИЛИ UserName — вариант имени по §7).
    public string? WaitingUser { get; init; }

    // AppID приложения ждущего соединения (1CV8C, BackgroundJob и т.п.).
    public string? WaitingAppId { get; init; }

    // Соединение(я)-держатель(и), которых ждёт ждущая сессия (WaitConnections из TLOCK).
    // Значение из ТЖ как есть (может быть несколько через запятую или иной разделитель).
    public string? BlockingConnections { get; init; }

    // Ресурс (пространство блокировки) — поле Regions (40_TECHLOG §8).
    public string? Regions { get; init; }

    // Режим блокировки — поле Locks (Shared / Exclusive, 40_TECHLOG §8).
    public string? LockMode { get; init; }

    // Длительность ожидания в секундах (нормализована из µs TechLogEvent.DurationSeconds,
    // 40_TECHLOG §4: duration в ТЖ = микросекунды, нормализация µs→секунды в TechLogEvent).
    public double? WaitDurationSeconds { get; init; }

    // Имя инфобазы (нормализованное базовое имя из p:processName без суффикса-GUID,
    // 40_TECHLOG §8: у фоновых сессий p:processName = «<имя ИБ>_<GUID>»).
    public string? InfobaseName { get; init; }

    // Сырое значение p:processName (до нормализации, для диагностики).
    public string? RawProcessName { get; init; }

    // Контекст стека 1С (поле Context из TLOCK, 40_TECHLOG §8).
    public string? Context { get; init; }

    // База данных СУБД (поле DataBase, 40_TECHLOG §8).
    public string? Database { get; init; }
}

/// <summary>
/// Запись о таймауте ожидания управляемой блокировки 1С (источник — событие TTIMEOUT,
/// «Lock request timeout», 40_TECHLOG §5). Точная структура полей TTIMEOUT подлежит
/// подтверждению на стенде 8.5 (приёмка владельца) — собрано по семантике 40_TECHLOG §5.
/// </summary>
public sealed record LockTimeoutEntry
{
    // Метка времени события TTIMEOUT (строка из поля ts).
    public string? Ts { get; init; }

    // SessionID сессии, которая ждала (и не дождалась).
    public string? SessionId { get; init; }

    // Пользователь (Usr ИЛИ UserName, вариант имени по §7).
    public string? User { get; init; }

    // Ресурс, который не удалось заблокировать (поле Regions).
    public string? Regions { get; init; }

    // Режим запрошенной блокировки (Locks: Shared / Exclusive).
    public string? LockMode { get; init; }

    // Время ожидания в секундах до таймаута (duration µs→сек).
    public double? WaitDurationSeconds { get; init; }

    // Имя инфобазы (нормализованное из p:processName).
    public string? InfobaseName { get; init; }

    // Сырое значение p:processName.
    public string? RawProcessName { get; init; }

    // Контекст 1С (поле Context).
    public string? Context { get; init; }

    // Соединения, которых ждала сессия (WaitConnections).
    public string? WaitConnections { get; init; }
}

/// <summary>
/// Запись о взаимоблокировке управляемых блокировок 1С (источник — событие TDEADLOCK,
/// 40_TECHLOG §5). Участники и ресурсы взаимоблокировки. Точная структура полей TDEADLOCK
/// подлежит подтверждению на стенде 8.5 (приёмка владельца) — собрано по семантике
/// 40_TECHLOG §5.
/// </summary>
public sealed record LockDeadlockEntry
{
    // Метка времени события TDEADLOCK (строка из поля ts).
    public string? Ts { get; init; }

    // SessionID участника дедлока.
    public string? SessionId { get; init; }

    // Пользователь участника (Usr ИЛИ UserName).
    public string? User { get; init; }

    // Ресурсы, вовлечённые во взаимоблокировку (поле Regions).
    public string? Regions { get; init; }

    // Режим блокировки (Locks).
    public string? LockMode { get; init; }

    // Длительность события в секундах (duration µs→сек).
    public double? DurationSeconds { get; init; }

    // Имя инфобазы (нормализованное из p:processName).
    public string? InfobaseName { get; init; }

    // Сырое значение p:processName.
    public string? RawProcessName { get; init; }

    // Контекст 1С (поле Context).
    public string? Context { get; init; }

    // Соединения-участники взаимоблокировки (WaitConnections).
    public string? WaitConnections { get; init; }
}

/// <summary>
/// Итоговый результат анализа управляемых блокировок 1С (ТОЛЬКО платформенный уровень;
/// СУБД-уровень — отдельная задача MLC-236 через &lt;dbmslocks/&gt;/lkX).
/// Строится ILockTreeAnalyzer из произвольного потока TechLogEvent.
/// </summary>
public sealed class LockAnalysisResult
{
    // Рёбра дерева ожидания: события TLOCK с непустым WaitConnections.
    // «Дерево» 1С-блокировок — кто кого ждёт, ресурс, режим, длительность.
    public IReadOnlyList<LockWaitEdge> WaitEdges { get; init; } = [];

    // Таймауты ожидания управляемых блокировок (события TTIMEOUT).
    public IReadOnlyList<LockTimeoutEntry> Timeouts { get; init; } = [];

    // Взаимоблокировки управляемых блокировок (события TDEADLOCK).
    public IReadOnlyList<LockDeadlockEntry> Deadlocks { get; init; } = [];

    // Сколько событий TLOCK обработано (включая те, что без WaitConnections).
    public int TlockEventsProcessed { get; init; }

    // Сколько событий-блокировок пропущено из-за неожиданной структуры (устойчивость §7).
    public int SkippedEvents { get; init; }
}

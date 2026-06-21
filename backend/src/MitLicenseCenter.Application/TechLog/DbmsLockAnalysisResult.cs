namespace MitLicenseCenter.Application.TechLog;

// Результат анализа СУБД-блокировок (MLC-236, этап B трека «Расследование производительности»).
// Строится анализатором IDbmsLockAnalyzer из событий DBMSSQL с полями lkX.
//
// ГРАНИЦА: ТОЛЬКО СУБД-уровень — отдельный механизм от управляемых блокировок 1С
// (TLOCK/TTIMEOUT/TDEADLOCK → ILockTreeAnalyzer, MLC-233). Не обещать единое дерево;
// СУБД-уровень виден только через тег <dbmslocks/> + поля lkX (40_TECHLOG §5).
//
// ⚠ Структура полей lkX собрана по документации (infostart 1431026, 40_TECHLOG §5).
//   Точная форма в JSON-ТЖ 8.5 подлежит подтверждению на стенде (приёмка владельца).
//
// Все поля — строки или нормализованные числа, неизменяемые типы (records).
// «Поля-призраки» (40_TECHLOG §7): lkX появляются только при блокировке; lka/lkp только
// при =1 — аксессоры TechLogEvent толерантны, модель несёт null там, где данных нет.

/// <summary>
/// Ребро дерева СУБД-блокировок: жертва (поток с lkp=1) → источник (поток с lka=1).
/// Алгоритм связки (40_TECHLOG §5, infostart 1431026): жертва.lksrc → источник.connectID.
/// ⚠ Структура полей lkX за стенд-приёмкой.
/// </summary>
public sealed record DbmsLockWaitEdge
{
    // Метка времени события DBMSSQL-жертвы (строка из поля ts, как пришла из ТЖ).
    public string? VictimTs { get; init; }

    // Соединение жертвы (t:connectID из события DBMSSQL-жертвы).
    public string? VictimConnectId { get; init; }

    // lksrc жертвы — номер соединения источника (у жертвы); по нему матчим источника.
    // Ключ связки «жертва → виновник» (40_TECHLOG §5).
    public string? VictimLksrc { get; init; }

    // lkpto — секунд с момента признания потока жертвой (у жертвы, 40_TECHLOG §5).
    public string? VictimLkpto { get; init; }

    // Текст SQL-запроса жертвы (поле Sql, «поле-призрак» — может отсутствовать, §7).
    public string? VictimSql { get; init; }

    // Контекст стека 1С жертвы (поле Context, «поле-призрак», §7).
    public string? VictimContext { get; init; }

    // Идентификатор запроса жертвы к СУБД (lkpid — номер запроса; кто её заблокировал, §5).
    public string? VictimLkpid { get; init; }

    // Соединение источника блокировки (connectID из события DBMSSQL-источника, если найден в окне).
    // null → источник не найден в текущем окне событий (см. UnmatchedVictimCount).
    public string? SourceConnectId { get; init; }

    // lkato — секунд с момента признания потока источником (у источника, 40_TECHLOG §5).
    // null, если источник не найден в окне.
    public string? SourceLkato { get; init; }

    // Список номеров запросов источника к СУБД (lkaid, у источника, §5).
    public string? SourceLkaid { get; init; }

    // Текст SQL-запроса источника (поле Sql, если источник найден; «поле-призрак», §7).
    public string? SourceSql { get; init; }

    // Контекст стека 1С источника (поле Context, если источник найден; «поле-призрак», §7).
    public string? SourceContext { get; init; }

    // Имя инфобазы (нормализованное базовое имя из p:processName без суффикса-GUID,
    // 40_TECHLOG §8: у фоновых сессий p:processName = «<имя ИБ>_<GUID>»).
    // Берётся из жертвы (источник может быть из другой ИБ).
    public string? InfobaseName { get; init; }

    // Сырое значение p:processName жертвы (до нормализации, для диагностики).
    public string? RawProcessName { get; init; }

    // База данных СУБД (поле DataBase из события жертвы, §7).
    public string? Database { get; init; }

    // true — источник блокировки найден в окне событий по lksrc→connectID.
    // false — источник не найден (ребро частичное: жертва известна, виновник нет).
    public bool SourceMatched { get; init; }
}

/// <summary>
/// Итоговый результат анализа СУБД-блокировок (MLC-236).
/// Строится IDbmsLockAnalyzer из произвольного потока TechLogEvent.
/// ⚠ Структура полей lkX за стенд-приёмкой (infostart 1431026, 40_TECHLOG §5).
/// </summary>
public sealed class DbmsLockAnalysisResult
{
    // Рёбра дерева СУБД-блокировок: жертва.lksrc → источник.connectID.
    // Частичные рёбра (источник не найден в окне) включены с SourceMatched=false.
    public IReadOnlyList<DbmsLockWaitEdge> WaitEdges { get; init; } = [];

    // Сколько событий DBMSSQL с полями lkX (lkp=1 или lka=1) обработано.
    public int LkEventsProcessed { get; init; }

    // Сколько жертв (lkp=1), для которых источник (lksrc→connectID) не найден в окне.
    // Частичные рёбра всё равно включены в WaitEdges (SourceMatched=false).
    public int UnmatchedVictimCount { get; init; }

    // Сколько событий пропущено из-за неожиданной структуры (устойчивость, 40_TECHLOG §7).
    public int SkippedEvents { get; init; }
}

namespace MitLicenseCenter.Application.TechLog;

// Результат анализа долгих запросов к СУБД (MLC-234, этап B трека
// «Расследование производительности»). Строится ISlowQueryAnalyzer из событий DBMSSQL.
//
// Структура полей DBMSSQL (40_TECHLOG §4/§8, снято со стенда 8.5.1.1302):
//   ts, duration, name, depth, level, process, p:processName, OSThread, t:clientID,
//   t:applicationName, t:computerName, t:connectID, DBMS, DataBase, Trans, dbpid, Sql
//   — в ЧАСТИ вызовов: Context, Rows, RowsAffected, SessionID, Usr (набор варьируется, §7).
//
// Все строки — как пришли из ТЖ (значения-строки, 40_TECHLOG §4); нормализованные числа
// и null при отсутствии поля. Неизменяемые типы (records). Комментарии по-русски.

/// <summary>
/// Запись об одном медленном вызове к СУБД (из события DBMSSQL, 40_TECHLOG §5).
/// Каждая запись — отдельное обращение; агрегаты «похожих» — в SlowQueryGroup.
/// </summary>
public sealed record SlowQueryEntry
{
    // Метка времени события DBMSSQL (строка из поля ts, как пришла из ТЖ).
    public string? Ts { get; init; }

    // Длительность запроса в микросекундах (единица ТЖ 8.5, 40_TECHLOG §4: µs).
    // Ненулевое — только запросы, прошедшие порог thresholdMicroseconds.
    public long DurationMicroseconds { get; init; }

    // Длительность в секундах = DurationMicroseconds / 1e6.
    public double DurationSeconds { get; init; }

    // Текст SQL-запроса (поле Sql, 40_TECHLOG §4/§8). null — если поле отсутствует
    // («поле-призрак» §7: Sql у DBMSSQL иногда отсутствует).
    public string? Sql { get; init; }

    // Контекст стека 1С (поле Context, 40_TECHLOG §8). null — часто отсутствует.
    public string? Context { get; init; }

    // Идентификатор соединения к СУБД (поле dbpid, 40_TECHLOG §8). null/пусто — бывает.
    public string? DbPid { get; init; }

    // Число строк в результате (поле Rows, 40_TECHLOG §8). null — поле отсутствует.
    public string? Rows { get; init; }

    // Число затронутых строк (поле RowsAffected, 40_TECHLOG §8). null — поле отсутствует.
    public string? RowsAffected { get; init; }

    // База данных СУБД (поле DataBase, 40_TECHLOG §8: «localhost\infobase01»).
    public string? Database { get; init; }

    // Имя инфобазы — нормализованное базовое имя из p:processName без суффикса-GUID
    // (40_TECHLOG §8: у фоновых сессий p:processName = «<имя ИБ>_<GUID>»).
    // Через TechLogProcessName.Normalize (Application/TechLog/TechLogProcessName.cs).
    public string? InfobaseName { get; init; }

    // Сырое значение p:processName (до нормализации, для диагностики).
    public string? RawProcessName { get; init; }

    // Сессия 1С (поле SessionID, 40_TECHLOG §8). null — поле отсутствует.
    public string? SessionId { get; init; }

    // Пользователь (поле Usr, 40_TECHLOG §8). null — поле отсутствует.
    // Вариант имени «UserName» не встречается в DBMSSQL (в отличие от TLOCK), но
    // при изменении структуры стенда учитывать §7.
    public string? User { get; init; }

    // Текст плана запроса (best-effort: поле плана собирается только при явном теге
    // <plansql/> в logcfg — 40_TECHLOG §5/§6; по умолчанию НЕ собирается).
    // Имя поля плана на стенде 8.5 НЕ снято — читаем кандидат «PlanSQLText»
    // (⚠ подтвердить на стенде: точное имя поля плана может отличаться).
    public string? PlanText { get; init; }
}

/// <summary>
/// Группа похожих SQL-запросов (агрегат по нормализованному тексту, MLC-234).
/// Нормализация: строковые/числовые литералы и параметры @P1/? схлопываются к
/// плейсхолдеру «?», лишние пробелы убираются — простая нормализация, не SQL-парсер.
/// Запросы без поля Sql в группировку не попадают (нечего нормализовать, §7).
/// </summary>
public sealed record SlowQueryGroup
{
    // Нормализованный текст запроса (литералы/параметры схлопнуты в «?»).
    // Используется как ключ группировки.
    public string NormalizedSql { get; init; } = string.Empty;

    // Число вхождений в отобранном потоке (прошедших порог длительности).
    public int Count { get; init; }

    // Суммарная длительность всех вхождений в микросекундах.
    public long TotalDurationMicroseconds { get; init; }

    // Максимальная длительность среди вхождений в микросекундах.
    public long MaxDurationMicroseconds { get; init; }

    // Суммарная длительность в секундах.
    public double TotalDurationSeconds { get; init; }

    // Максимальная длительность в секундах.
    public double MaxDurationSeconds { get; init; }
}

/// <summary>
/// Итоговый результат анализа долгих запросов к СУБД (DBMSSQL).
/// Строится ISlowQueryAnalyzer из произвольного потока TechLogEvent.
/// </summary>
public sealed class SlowQueryAnalysisResult
{
    // Топ медленных запросов, отсортированный по длительности убывающим (топ-N).
    // Только DBMSSQL с длительностью ≥ порога (thresholdMicroseconds).
    // Запись без Sql попадает в топ, если длительность ≥ порога (Sql = null).
    public IReadOnlyList<SlowQueryEntry> TopQueries { get; init; } = [];

    // Группы похожих запросов по нормализованному тексту Sql.
    // Отсортированы по суммарной длительности убывающим.
    // Запросы без поля Sql в группировку не входят (нечего нормализовать, §7).
    public IReadOnlyList<SlowQueryGroup> SimilarGroups { get; init; } = [];

    // Сколько DBMSSQL-событий обнаружено в потоке (до фильтрации по порогу).
    public int TotalDbmssqlEvents { get; init; }

    // Сколько DBMSSQL-событий прошло порог длительности.
    public int EventsAboveThreshold { get; init; }

    // Сколько событий пропущено из-за непредвиденных ошибок разбора (устойчивость §7).
    public int SkippedEvents { get; init; }
}

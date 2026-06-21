namespace MitLicenseCenter.Application.TechLog;

// Результат анализа исключений платформы 1С (MLC-235, этап B трека
// «Расследование производительности»). Строится IExceptionAnalyzer из событий EXCP.
//
// Структура полей EXCP (40_TECHLOG §8, снято со стенда 8.5.1.1302):
//   ts, duration, name, depth, level, process, p:processName, OSThread, t:clientID,
//   Exception (тип исключения), Descr (текст ошибки), Context (стек; иногда отсутствует).
//
// Особенность DataBaseException (40_TECHLOG §7):
//   Дедлок СУБД пишет ДВА события EXCP с Exception=DataBaseException.
//   Точная пара-корреляция — после подтверждения на стенде. В этой версии IsDatabaseException
//   помечает группу; частота DataBaseException может удваивать число инцидентов блокировок.
//
// Неизменяемые типы (records). Комментарии по-русски со ссылками на 40_TECHLOG §5/§7/§8.

/// <summary>
/// Группа однотипных исключений (агрегат по паре: тип Exception + нормализованный Descr).
/// Нормализация Descr: числа/идентификаторы/идентификаторы схлопываются к плейсхолдеру «#»,
/// лишние пробелы убираются. Цель — «timeout 123» и «timeout 456» → одна группа.
/// </summary>
public sealed record ExceptionGroup
{
    // Тип исключения платформы (поле Exception, 40_TECHLOG §8).
    // Примеры: «DataBaseException», «MethodNotFoundException», «SystemException».
    // null — если поле отсутствует в событии («поле-призрак», §7).
    public string? ExceptionType { get; init; }

    // Нормализованный текст описания ошибки (поле Descr, 40_TECHLOG §8).
    // Числа/идентификаторы схлопнуты к «#», пробелы нормализованы.
    // «(без описания)» — если поле Descr отсутствует (толерантность §7).
    public string NormalizedDescr { get; init; } = ExceptionAnalysisResult.NoDescrPlaceholder;

    // Образец сырого текста Descr первого вхождения в группе (до нормализации).
    // Удобен для диагностики: виден конкретный текст ошибки.
    public string? SampleDescr { get; init; }

    // Пример контекста стека 1С (поле Context первого вхождения, 40_TECHLOG §8).
    // null — если поле отсутствует («поле-призрак», §7).
    public string? SampleContext { get; init; }

    // Число вхождений EXCP в группе.
    // Замечание по DataBaseException (40_TECHLOG §7): дедлок СУБД пишет 2 EXCP —
    // Count для DataBaseException может удваивать число реальных инцидентов блокировок.
    // Точная пара-корреляция дедлоков — после подтверждения на стенде.
    public int Count { get; init; }

    // Флаг: группа содержит исключения с типом DataBaseException.
    // DataBaseException — кандидат на блокировки/дедлоки уровня СУБД (40_TECHLOG §7).
    // Дедлок пишет ДВА EXCP: оценочная частота инцидентов = Count / 2.
    public bool IsDatabaseException { get; init; }

    // Имя инфобазы (нормализованное базовое имя из p:processName без суффикса-GUID,
    // 40_TECHLOG §8: у фоновых сессий p:processName = «<имя ИБ>_<GUID>»).
    // Через TechLogProcessName.Normalize (Application/TechLog/TechLogProcessName.cs).
    // null — если поле p:processName отсутствует в событии.
    public string? InfobaseName { get; init; }

    // Сырое значение p:processName первого вхождения в группе (до нормализации).
    public string? RawProcessName { get; init; }

    // Метка времени первого вхождения в группе (поле ts, как пришло из ТЖ).
    public string? FirstTs { get; init; }

    // Метка времени последнего вхождения в группе (поле ts, как пришло из ТЖ).
    public string? LastTs { get; init; }
}

/// <summary>
/// Итоговый результат анализа исключений платформы 1С (EXCP).
/// Строится IExceptionAnalyzer из произвольного потока TechLogEvent.
/// </summary>
public sealed class ExceptionAnalysisResult
{
    // Плейсхолдер для отсутствующего поля Descr (толерантность §7).
    public const string NoDescrPlaceholder = "(без описания)";

    // Топ групп исключений, отсортированный по числу вхождений убывающим (топ-N).
    // Группировка по паре: тип Exception + нормализованный Descr.
    // Ограничен параметром topN метода Analyze.
    public IReadOnlyList<ExceptionGroup> TopExceptions { get; init; } = [];

    // Общее число событий EXCP в потоке (до группировки/фильтрации).
    public int TotalExcpEvents { get; init; }

    // Число событий EXCP с типом DataBaseException.
    // Включает кандидатов на дедлоки/блокировки СУБД (дедлок = 2 EXCP, §7).
    public int DatabaseExceptionEvents { get; init; }

    // Число событий, пропущенных из-за непредвиденных ошибок разбора (устойчивость §7).
    public int SkippedEvents { get; init; }
}

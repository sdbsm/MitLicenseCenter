using System.Text.Json;
using MitLicenseCenter.Domain.TechLog;

namespace MitLicenseCenter.Web.Endpoints;

// Контракты раздела «Расследование производительности» (MLC-239, трек 1.2, этап C). Зеркалят
// контракты «Записи» быстродействия (InvestigationContracts ↔ PerformanceContracts/Recording*):
// Status/Scenario/StopReason на проводе — строкой (JsonStringEnumConverter, Program.cs), пагинация —
// конверт {items,total,page,pageSize}. Раздел читает агрегат Investigation/Finding/CollectionConfig
// (MLC-237/238) — здесь только формы на проводе, наполнение делает сервис/конвейер.

// Тело запроса старта расследования (POST /investigations). Scenario обязателен; InfobaseId
// опционален — задан ⇒ эндпоинт резолвит инфобазу из реестра (имя→p:processName, TenantId) и
// привязывает дело к арендатору (иначе сбор охватывает весь кластер).
// SlowQueryThresholdSeconds (MLC-248) — порог «долгих запросов» В СЕКУНДАХ (оператору понятнее микросекунд),
// релевантен только сценариям SlowQueries/GeneralSlow. Валидация: ≥ 0; отрицательное → 400. null/не задано →
// дефолт 1 c. Явный 0 допустим (все запросы попадут в «топ долгих»). Эндпоинт конвертирует сек→микросек.
public sealed record StartInvestigationRequest(
    InvestigationScenario Scenario,
    Guid? InfobaseId,
    double? SlowQueryThresholdSeconds = null);

// Элемент списка дел + шапка детали. Поля nullable (StoppedAtUtc/StopReason/TenantId/InfobaseId)
// бэкенд ОПУСКАЕТ при null (WhenWritingNull) — на FE это omittable(). FindingsCount — число
// результатов анализаторов (наполняет конвейер MLC-238 при снятии).
public sealed record InvestigationSummary(
    Guid Id,
    InvestigationScenario Scenario,
    InvestigationStatus Status,
    DateTime StartedAtUtc,
    DateTime? StoppedAtUtc,
    string StartedBy,
    InvestigationStopReason? StopReason,
    Guid? TenantId,
    Guid? InfobaseId,
    int FindingsCount);

// Пагинированный список дел (конверт {items,total,page,pageSize}, зеркаль RecordingsPagedResponse).
public sealed record InvestigationsPagedResponse(
    IReadOnlyList<InvestigationSummary> Items,
    int Total,
    int Page,
    int PageSize);

// Снимок включённого сбора (CollectionConfig, owned 1:1). Null для исторических/перенесённых дел
// (механический перенос MLC-237 снимка не наполнял). На проводе nullable-поля опускаются.
public sealed record CollectionConfigDto(
    string LogcfgLocation,
    string Events,
    long? DurationThresholdMicros,
    string? ProcessNameFilter,
    string Format,
    int HistoryHours);

// Один результат анализатора. Result — ДЕсериализованный ResultJson отдаётся вложенным JSON-объектом
// (НЕ строкой): на проводе result = объект анализатора (LockAnalysisResult/SlowQueryAnalysisResult/…).
// Точная пер-Kind типизация результата на FE откладывается на этап D — там схема пермиссивна.
// JsonElement passthrough: System.Text.Json сериализует уже-разобранный JsonElement как есть.
public sealed record FindingDto(
    FindingKind Kind,
    int SchemaVersion,
    JsonElement Result);

// Деталь дела = шапка + снимок сбора + список находок (с разобранным result-объектом).
public sealed record InvestigationDetail(
    InvestigationSummary Summary,
    CollectionConfigDto? CollectionConfig,
    IReadOnlyList<FindingDto> Findings);

// Лёгкий прогресс для поллинга (GET /{id}/progress): статус + старт + прошедшее время + (опц.) размер
// собранного. Дёшево, без тяжёлых JOIN. CollectedBytes — null, если дело не активно (каталог снят) или
// размер не определить (seam ILogcfgStore.GetDirectorySizeBytes за store).
public sealed record InvestigationProgress(
    Guid Id,
    InvestigationStatus Status,
    DateTime StartedAtUtc,
    double ElapsedSeconds,
    long? CollectedBytes);

// ── Отчёт (GET /{id}/report) ─────────────────────────────────────────────────────────────────────
// Вычисляемое представление поверх Finding: ранжированные находки + базовые текстовые рекомендации
// (шаблоны). Глубокого движка рекомендаций НЕТ (ограничение этапа C — best-effort, расширяется в D):
// ранг = эвристика по числу записей в результате анализатора, рекомендации — статические шаблоны
// по Kind. Severity на проводе строкой (фронт локализует/красит). Result не дублируется (он в детали).

public enum ReportSeverity
{
    // Нет находок этого вида (анализатор отработал, но записей нет) — информативная строка.
    None = 0,

    // Есть находки, но в пределах ожидаемого (немного) — обратить внимание.
    Info = 1,

    // Заметный объём находок — вероятная причина проблемы, разобрать.
    Warning = 2,
}

// Одна ранжированная находка отчёта: вид + серьёзность + счётчик (число записей) + заголовок и
// текстовая рекомендация (шаблон по Kind). Headline/Recommendation — готовый русский текст (раздел
// «Отчёт» не строит экранов в этом этапе; формы для D).
public sealed record ReportItem(
    FindingKind Kind,
    ReportSeverity Severity,
    int Count,
    string Headline,
    string Recommendation);

// Отчёт по делу = шапка + ранжированные находки (severity убыванием). Generated — момент вычисления.
public sealed record InvestigationReport(
    InvestigationSummary Summary,
    DateTime GeneratedAtUtc,
    IReadOnlyList<ReportItem> Items);

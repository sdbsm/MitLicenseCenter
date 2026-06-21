namespace MitLicenseCenter.Domain.TechLog;

// Результат анализатора ТЖ под «Дело» (MLC-237, этап C; спека 50_DATA_MODEL §Finding). Один Finding на
// результат анализатора этапа B (LockTreeAnalyzer/SlowQueryAnalyzer/ExceptionAnalyzer/DbmsLockAnalyzer).
//
// РЕШЕНИЕ КУРАТОРА (Q1 в 50_DATA_MODEL): хранить версионированным JSON, НЕ нормализованными таблицами.
// Анализаторы этапа B уже отдают богатые вложенные DTO; проект уже хранит перф-payload'ы JSON-колонками
// (PerfRecordingSample.ProcessGroupsJson/OneCLoadJson/SqlLoadJson). ResultJson несёт сериализованный DTO
// анализатора; SchemaVersion версионирует форму payload'а (миграция формы — без EF-миграции таблицы).
//
// Дочерняя таблица Investigations, каскадное удаление с делом (DELETE дела сносит его Findings).
// Наполнение — оркестрация MLC-238 (здесь сущность + хранилище).
public sealed class Finding
{
    public Guid Id { get; init; }

    // FK на дело-владелец. Каскадное удаление настроено в AppDbContext.
    public Guid InvestigationId { get; init; }

    // Какой анализатор дал результат (frozen-int enum). Различает форму ResultJson.
    public FindingKind Kind { get; init; }

    // Версия схемы payload'а ResultJson. Растёт при изменении формы DTO анализатора — читатель выбирает
    // десериализатор по версии (форвард-совместимость без EF-миграции).
    public int SchemaVersion { get; init; }

    // Сериализованный результат анализатора (nvarchar(max)). Версионируется SchemaVersion/Kind.
    public string ResultJson { get; init; } = string.Empty;
}

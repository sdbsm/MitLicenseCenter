namespace MitLicenseCenter.Domain.TechLog;

// Снимок включённого сбора ТЖ (MLC-237, этап C; спека 50_DATA_MODEL §CollectionConfig). Аудит/
// воспроизводимость: после расследования видно, ЧТО именно собирали, и можно безопасно снять ровно то,
// что ставили. Неизменяемый снимок момента установки.
//
// Реализован как owned-entity Investigation (колонки в таблице Investigations с префиксом Config_, 1:1) —
// проще отдельной таблицы и аудит-снимок неизменяем, отдельный жизненный цикл/ключ не нужны. Все поля
// nullable/optional на уровне owned: на этапе C механический перенос TechLogCollection их НЕ наполняет
// (старая сущность снимка не имела) → owned-ссылка Investigation.CollectionConfig = null для исторических
// дел; наполняет оркестрация MLC-238.
//
// Инвариант изоляции (50_DATA_MODEL, 60_SAFETY №2): если у Investigation задан InfobaseId →
// ProcessNameFilter ОБЯЗАН быть непустым (иначе logcfg пишет ТЖ всех арендаторов). Энфорсится
// Investigation.EnsureProcessFilterInvariant() (вызывает оркестрация после установки снимка).
public sealed class CollectionConfig
{
    // Каталог сбора ТЖ (атрибут location в logcfg).
    public string LogcfgLocation { get; init; } = string.Empty;

    // Список включённых событий (напр. "TLOCK,TTIMEOUT,TDEADLOCK"). Храним строкой CSV — проще JSON-массива,
    // снимок неизменяем и читается целиком (нормализация/парсинг не нужны на этом уровне).
    public string Events { get; init; } = string.Empty;

    // Порог по длительности в МИКРОсекундах (logcfg Dur — мкс, 40_TECHLOG §6). null = без порога (для
    // JSON-ТЖ 8.5 фильтр Dur не работает — MLC-229; объём режется типом события и p:processName).
    public long? DurationThresholdMicros { get; init; }

    // Значение p:processName (имя ИБ) — ОБЯЗАТЕЛЬНО, если у Investigation задан InfobaseId (см. инвариант
    // выше). null = весь кластер.
    public string? ProcessNameFilter { get; init; }

    // Целевой формат ТЖ (всегда "json" для 8.5; ⚠ verify на стенде — 40_TECHLOG).
    public string Format { get; init; } = "json";

    // Лимит ротации (атрибут history в logcfg), в часах.
    public int HistoryHours { get; init; }
}

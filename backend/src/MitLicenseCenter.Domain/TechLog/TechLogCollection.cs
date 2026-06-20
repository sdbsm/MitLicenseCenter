using System.Diagnostics.CodeAnalysis;

namespace MitLicenseCenter.Domain.TechLog;

// Дело сбора технологического журнала «по требованию» (MLC-230, ADR-57/58). Лёгкий прокси
// «активного дела сбора ТЖ» до полной сущности Investigation (MLC-237) — этап C её мигрирует, это
// нормально. Сущность-телеметрия рядом с PerfRecording: одно расследование ТЖ. Активное дело
// (Status=Active) означает, что в conf платформы лежит НАШ logcfg.xml; снятие/сторож возвращают
// исходный. Снимок установленного (Scenario/InfobaseProcessName/CollectionDirectory/ConfigMarker)
// хранится для идемпотентного снятия и сверки сторожем (60_SAFETY №6). Конфиг EF — inline в
// AppDbContext.OnModelCreating (как PerfRecording).
// CA1711: «Collection» здесь — доменный термин («дело сбора ТЖ»), а не суффикс ICollection-типа;
// имя зафиксировано постановкой MLC-230 (прокси будущей Investigation, MLC-237).
[SuppressMessage("Naming", "CA1711:Identifiers should not have incorrect suffix",
    Justification = "TechLogCollection — доменное «дело сбора ТЖ», не коллекция-тип (MLC-230).")]
public sealed class TechLogCollection
{
    public Guid Id { get; init; }

    public TechLogCollectionStatus Status { get; set; }

    public DateTime StartedAtUtc { get; init; }

    public DateTime? StoppedAtUtc { get; set; }

    // Причина остановки: заполнена только для Stopped; null для Active/Interrupted.
    public TechLogCollectionStopReason? StopReason { get; set; }

    // Сценарий сбора — строкой (имя TechLogScenario из Application; Domain не зависит от Application).
    public string Scenario { get; init; } = string.Empty;

    // Имя инфобазы (p:processName) для изоляции арендатора; null = весь кластер (60_SAFETY №2).
    // Известное ограничение: фоновые задания арендатора пишутся как `<имя ИБ>_<GUID>` и проходят
    // мимо точного <eq>, а <like ...%> в JSON НЕ работает (40_TECHLOG §6) → точечная подстраховка
    // на разборе (этап B/C). Обход через <like> НЕ применяется.
    public string? InfobaseProcessName { get; init; }

    // Каталог сбора ТЖ (атрибут location в logcfg) — под контролем панели.
    public string CollectionDirectory { get; init; } = string.Empty;

    // Маркер-комментарий установленного logcfg — по нему сторож на старте отличает «наш» конфиг от
    // чужого и сверяет фактический logcfg.xml в conf с ожидаемым (60_SAFETY №5). Снимок того, что
    // поставили (идемпотентность №6).
    public string ConfigMarker { get; init; } = string.Empty;
}

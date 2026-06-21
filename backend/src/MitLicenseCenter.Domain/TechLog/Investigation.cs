namespace MitLicenseCenter.Domain.TechLog;

// Аггрегат «Дело» расследования производительности (MLC-237, трек 1.2, этап C; спека 50_DATA_MODEL).
// ЗАМЕНЯЕТ TechLogCollection (MLC-230 завёл её как лёгкий прокси «дела» до полной сущности — это её
// миграция, не параллельная сущность): данные TechLogCollections перенесены в Investigations, таблица
// удалена, живой сервис/сторож перенаправлены сюда механически (поведение без изменений — наполнение
// сценарной оркестрацией/Finding/отчётом делает MLC-238).
//
// Объединяет окно времени, сценарий, снимок включённого сбора (CollectionConfig, owned 1:1) и результаты
// анализа (Finding, дочерняя таблица). Активное дело (Status=Collecting) означает, что в conf платформы
// лежит НАШ logcfg.xml; снятие/сторож возвращают исходный. Снимок установленного (Scenario/
// InfobaseProcessName/CollectionDirectory/ConfigMarker) хранится для идемпотентного снятия и сверки
// сторожем (60_SAFETY №6).
//
// RowVersion (optimistic concurrency, MLC-237): у TechLogCollection токена НЕ было; здесь он есть — дело
// получает самостоятельные мутации оркестрацией (MLC-238: Collecting→Analyzing→Completed + наполнение
// Finding), и конкурентный targeted-UPDATE (сторож авто-стопа vs ручное снятие) должен ловиться 409, а
// не молча терять запись. Тест concurrency — на SQLite (InMemory не энфорсит rowversion).
//
// Конфиг EF — inline в AppDbContext.OnModelCreating (как PerfRecording/TechLogCollection). Без FK на
// Tenant — сбор охватывает узел (logcfg действует на весь кластер), изоляция арендатора — фильтр
// p:processName (CollectionConfig.ProcessNameFilter), не FK. Scenario — Domain-enum InvestigationScenario
// (int-совместим с Application.TechLogScenario; Domain не зависит от Application).
public sealed class Investigation
{
    public Guid Id { get; init; }

    // Сценарий сбора (40_TECHLOG §6) — хранится int (HasConversion). Определяет набор событий в logcfg и
    // уходит в аудит/отчёт. InvestigationScenario.* int-совместим с Application.TechLogScenario.*.
    public InvestigationScenario Scenario { get; init; }

    public InvestigationStatus Status { get; set; }

    public DateTime StartedAtUtc { get; init; }

    public DateTime? StoppedAtUtc { get; set; }

    // Причина остановки: заполнена только для Completed; null для Collecting/Analyzing/Interrupted.
    public InvestigationStopReason? StopReason { get; set; }

    // Логин оператора (Admin), запустившего дело. Зеркаль PerfRecording.StartedBy.
    public string StartedBy { get; init; } = string.Empty;

    // Опц. привязка к арендатору/инфобазе (50_DATA_MODEL). Сам по себе НЕ изолирует сбор: logcfg
    // глобален. Инвариант: при заданном InfobaseId в CollectionConfig.ProcessNameFilter ОБЯЗАН попасть
    // фильтр p:processName (иначе пишется ТЖ всех арендаторов) — энфорсится EnsureProcessFilterInvariant.
    public Guid? TenantId { get; init; }
    public Guid? InfobaseId { get; init; }

    // --- Операционные поля сбора (перенесены из TechLogCollection; питают живой сервис/сторож) ---

    // Имя инфобазы (p:processName) для изоляции арендатора; null = весь кластер (60_SAFETY №2).
    // Известное ограничение: фоновые задания арендатора пишутся как `<имя ИБ>_<GUID>` и проходят мимо
    // точного <eq>, а <like ...%> в JSON НЕ работает (40_TECHLOG §6) → точечная подстраховка на разборе
    // (этап B/C). Обход через <like> НЕ применяется.
    public string? InfobaseProcessName { get; init; }

    // Каталог сбора ТЖ (атрибут location в logcfg) — под контролем панели.
    public string CollectionDirectory { get; init; } = string.Empty;

    // Маркер-комментарий установленного logcfg — по нему сторож на старте отличает «наш» конфиг от чужого
    // и сверяет фактический logcfg.xml в conf с ожидаемым (60_SAFETY №5). Снимок того, что поставили
    // (идемпотентность №6).
    public string ConfigMarker { get; init; } = string.Empty;

    // Optimistic-concurrency токен (MLC-237). IsRowVersion() в конфиге → SQL Server `rowversion`,
    // IsConcurrencyToken + ValueGeneratedOnAddOrUpdate. На конкурентном UPDATE с устаревшим OriginalValue
    // EF бросает DbUpdateConcurrencyException. Под InMemory всегда null (провайдер токен не генерирует).
    public byte[]? RowVersion { get; set; }

    // Снимок включённого сбора (аудит/воспроизводимость) — owned 1:1 (колонки в той же таблице).
    // Заполняется оркестрацией (MLC-238); на этапе C механический перенос не наполняет его (поведение
    // TechLogCollection не имело такого снимка). Опционален: null для перенесённых исторических дел.
    public CollectionConfig? CollectionConfig { get; set; }

    // Результаты анализа ТЖ (MLC-238) — дочерняя таблица, каскадное удаление с делом. Один Finding на
    // результат анализатора этапа B (версионированный JSON).
    public ICollection<Finding> Findings { get; } = new List<Finding>();

    // Инвариант изоляции арендатора (50_DATA_MODEL §Investigation/§CollectionConfig, 60_SAFETY №2): если
    // дело привязано к инфобазе (InfobaseId задан), снимок сбора ОБЯЗАН нести фильтр p:processName —
    // иначе logcfg пишет ТЖ всех арендаторов. Вызывается оркестрацией (MLC-238) после установки снимка.
    public void EnsureProcessFilterInvariant()
    {
        if (InfobaseId is not null
            && string.IsNullOrWhiteSpace(CollectionConfig?.ProcessNameFilter))
        {
            throw new InvalidOperationException(
                "Investigation привязано к инфобазе (InfobaseId задан), но CollectionConfig.ProcessNameFilter " +
                "пуст: сбор ТЖ без p:processName пишет журнал всех арендаторов (нарушение изоляции, 60_SAFETY №2).");
        }
    }
}

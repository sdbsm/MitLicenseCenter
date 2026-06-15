namespace MitLicenseCenter.Application.Sessions;

/// <summary>
/// Чистый доменный расчёт потребления лицензий по снапшоту сессий.
/// Единый дом правила: потребление = count(LicenseStatus==Consuming) по TenantId;
/// over-limit = consumed &gt; limit при limit &gt; 0 (членство активных тенантов задаёт
/// вызывающая сторона через словарь лимитов). Pending/NotConsuming (ADR-48) не считаются
/// и не убиваются — факт rac не подтвердил потребление.
/// </summary>
public static class LicenseConsumption
{
    /// <summary>
    /// Потребление лицензий по тенанту: count(LicenseStatus==Consuming) сгруппировано по
    /// TenantId. Тенанты без потребляющих сессий в словарь не попадают.
    /// </summary>
    public static Dictionary<Guid, int> CountByTenant(IEnumerable<SnapshotSessionEntry> entries)
        => entries
            .Where(e => e.LicenseStatus == LicenseStatus.Consuming)
            .GroupBy(e => e.TenantId)
            .ToDictionary(g => g.Key, g => g.Count());

    /// <summary>
    /// Тенанты, превысившие свой положительный лимит. Членство активных тенантов задаёт
    /// <paramref name="activeTenantLimits"/> (тенант не в словаре — пропущен). Порядок
    /// результата = порядку перечисления <paramref name="consumptionByTenant"/> (важно
    /// для cap MaxKillsPerCycle в энфорсере).
    /// </summary>
    public static List<OverLimitTenant> FindOverLimit(
        IReadOnlyDictionary<Guid, int> consumptionByTenant,
        IReadOnlyDictionary<Guid, int> activeTenantLimits)
    {
        var result = new List<OverLimitTenant>();
        foreach (var (tenantId, consumed) in consumptionByTenant)
        {
            if (!activeTenantLimits.TryGetValue(tenantId, out var limit))
                continue;
            if (limit <= 0)
                continue;
            if (consumed <= limit)
                continue;
            result.Add(new OverLimitTenant(tenantId, consumed, limit));
        }
        return result;
    }

    /// <summary>
    /// Сессии тенанта, потребляющие лицензию, отсортированные newest-first — приоритет
    /// на kill. OrderByDescending в LINQ стабилен, поэтому равные StartedAtUtc сохраняют
    /// исходный порядок входной последовательности.
    /// </summary>
    public static List<SnapshotSessionEntry> KillCandidates(
        IEnumerable<SnapshotSessionEntry> entries, Guid tenantId)
        => entries
            .Where(e => e.TenantId == tenantId && e.LicenseStatus == LicenseStatus.Consuming)
            .OrderByDescending(e => e.StartedAtUtc)
            .ToList();
}

public readonly record struct OverLimitTenant(Guid TenantId, int Consumed, int Limit);

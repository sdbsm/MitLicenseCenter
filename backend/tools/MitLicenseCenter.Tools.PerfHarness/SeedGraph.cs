using MitLicenseCenter.Domain.Audit;
using MitLicenseCenter.Domain.Infobases;
using MitLicenseCenter.Domain.Publications;
using MitLicenseCenter.Domain.Tenants;
using MitLicenseCenter.Infrastructure.Audit;

namespace MitLicenseCenter.Tools.PerfHarness;

// Параметры роста. Дефолты = baseline-точка из OPERATIONS.md; CLI переопределяет под
// ростовую точку (×10). Seed фиксирован → воспроизводимый граф (детерминированные GUID).
internal sealed record SeedOptions
{
    public int Tenants { get; init; } = 20;
    public int Infobases { get; init; } = 50;
    public int Audit { get; init; } = 100_000;
    public int Sessions { get; init; } = 500;
    public double OverLimitFraction { get; init; } = 0.30;
    public int Seed { get; init; } = 1039;

    // Горизонт давности для строк аудита (timestamps размазаны по этому окну). Совпадает с
    // дефолтным ретеншеном аудита (Audit.RetentionDays = 365) — сеем ровно столько, сколько
    // программа хранит, чтобы ночная AuditRetentionJob не выкосила засеянное.
    public int AuditDays { get; init; } = 365;

    // Глубина истории использования лицензий в днях (0 = не сеять — сохраняет 1:1 поведение
    // perf-харнесса MLC-039). Совпадает с дефолтным LicenseUsage.RetentionDays = 365.
    public int UsageDays { get; init; }

    // Realistic-режим (--realistic): лимиты тенантов из СМБ-распределения 5..150, снапшоты
    // usage пишут Limit = лимиту тенанта (coupled, как в проде), сессии = текущему потреблению.
    // По умолчанию ВЫКЛ → perf-поведение MLC-039 (1/1e6, round-robin) сохраняется 1:1.
    public bool Realistic { get; init; }
}

// Засеваемый граф (без аудита — он стримится отдельно из-за объёма K) + сценарий для заглушки.
// Profiles непуст только в realistic-режиме (единый источник для снапшотов и сессий).
internal sealed record SeedGraph(
    IReadOnlyList<Tenant> Tenants,
    IReadOnlyList<Infobase> Infobases,
    IReadOnlyList<Publication> Publications,
    PerfScenario Scenario,
    IReadOnlyList<TenantUsageProfile> Profiles);

// Чистая генерация графа — без БД, поэтому инварианты (per-tenant имена, глобальная
// уникальность ClusterInfobaseId, 1:1 публикация, over-limit тенанты) проверяемы unit-тестом.
internal static class SeedDataGenerator
{
    // Over-limit тенант: крошечный лимит → любые ≥2 потребляющих сессии превышают его и
    // промоутят его в hot (percent ≥ 90%). Normal тенант: огромный лимит → никогда не hot.
    private const int OverLimitCap = 1;
    private const int NormalCap = 1_000_000;
    private const int OverLimitSessionQuota = 5; // > OverLimitCap с запасом
    private const string ConsumingAppId = "1CV8"; // ∈ дефолтный whitelist OneCLicenseConsumingAppIds

    public static SeedGraph Build(SeedOptions opts, DateTime nowUtc)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(opts.Tenants);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(opts.Infobases);
        ArgumentOutOfRangeException.ThrowIfNegative(opts.Sessions);

        var rng = new Random(opts.Seed);

        var overLimitCount = Math.Clamp(
            (int)Math.Ceiling(opts.Tenants * opts.OverLimitFraction), 0, opts.Tenants);

        var tenants = new List<Tenant>(opts.Tenants);
        for (var i = 0; i < opts.Tenants; i++)
        {
            // NextGuid вызывается всегда; BuildRealisticLimit — ТОЛЬКО в realistic-ветке
            // тернарника (короткое замыкание), поэтому поток rng perf-пути неизменен и
            // Build_is_deterministic_for_a_fixed_seed остаётся зелёным.
            var id = NextGuid(rng);
            var limit = opts.Realistic
                ? BuildRealisticLimit(rng)
                : (i < overLimitCount ? OverLimitCap : NormalCap);

            tenants.Add(new Tenant
            {
                Id = id,
                Name = opts.Realistic ? BuildRealisticName(i) : $"perf-tenant-{i:D5}",
                MaxConcurrentLicenses = limit,
                IsActive = true,
                CreatedAt = nowUtc,
            });
        }

        // Инфобазы round-robin по тенантам; локальный индекс делает имя уникальным per-tenant.
        var infobases = new List<Infobase>(opts.Infobases);
        var publications = new List<Publication>(opts.Infobases);
        var infobasesByTenant = new List<int>[opts.Tenants];
        for (var t = 0; t < opts.Tenants; t++)
        {
            infobasesByTenant[t] = [];
        }

        for (var j = 0; j < opts.Infobases; j++)
        {
            var tenantIndex = j % opts.Tenants;
            var localIndex = infobasesByTenant[tenantIndex].Count;
            var clusterInfobaseId = NextGuid(rng);

            var infobase = new Infobase
            {
                Id = NextGuid(rng),
                TenantId = tenants[tenantIndex].Id,
                Name = $"ib-{localIndex:D4}",
                ClusterInfobaseId = clusterInfobaseId,
                DatabaseName = $"db_{j:D6}",
                Status = InfobaseStatus.Active,
                CreatedAt = nowUtc,
            };
            infobases.Add(infobase);
            infobasesByTenant[tenantIndex].Add(j);

            publications.Add(new Publication
            {
                Id = NextGuid(rng),
                InfobaseId = infobase.Id,
                SiteName = "Default Web Site",
                VirtualPath = $"perf{j:D6}",
                PlatformVersion = "8.3.24.1234",
                Source = PublicationSource.Unknown,
                LastCheckStatus = PublicationPublishStatus.Unknown,
                CreatedAt = nowUtc,
            });
        }

        // Realistic: профили (единый источник) → сессии из текущего потребления. Perf: прежний
        // round-robin BuildSessions. Профили строятся БЕЗ rng (модель детерминирована от
        // tenantIndex+bucket), поэтому не влияют на поток rng сессий.
        IReadOnlyList<TenantUsageProfile> profiles;
        List<ScenarioSession> sessions;
        if (opts.Realistic)
        {
            profiles = BuildProfiles(overLimitCount, tenants, infobases, infobasesByTenant, nowUtc);
            sessions = BuildRealisticSessions(profiles, rng, nowUtc);
        }
        else
        {
            profiles = [];
            sessions = BuildSessions(opts, overLimitCount, infobases, infobasesByTenant, rng, nowUtc);
        }

        var scenarioInfobases = infobases
            .Select(i => new ScenarioInfobase(i.ClusterInfobaseId, i.Name, null))
            .ToList();

        var scenario = new PerfScenario(NextGuid(rng), sessions, scenarioInfobases);

        return new SeedGraph(tenants, infobases, publications, scenario, profiles);
    }

    // Стримим K строк аудита лениво — материализовать миллион в список незачем (батчевая вставка).
    public static IEnumerable<AuditLog> EnumerateAuditLogs(
        SeedOptions opts, IReadOnlyList<Guid> tenantIds, DateTime nowUtc)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(opts.Audit);

        // Отдельный rng со смещённым seed → аудит независим от графа, но воспроизводим.
        var rng = new Random(opts.Seed ^ 0x5EED);
        var retentionMinutes = Math.Max(1, opts.AuditDays) * 24 * 60;

        for (var k = 0; k < opts.Audit; k++)
        {
            var action = AuditActions[rng.Next(AuditActions.Length)];
            // Половина строк привязана к тенанту, половина — системные (TenantId = null).
            Guid? tenantId = tenantIds.Count > 0 && rng.Next(2) == 0
                ? tenantIds[rng.Next(tenantIds.Count)]
                : null;
            var reason = action == AuditActionType.SessionKilled ? AuditReason.LimitExceeded : (AuditReason?)null;

            yield return new AuditLog
            {
                Id = NextGuid(rng),
                Timestamp = nowUtc.AddMinutes(-rng.Next(retentionMinutes)),
                ActionType = action,
                Reason = reason,
                Initiator = "perf-seed",
                Description = $"perf audit row {k}",
                TenantId = tenantId,
            };
        }
    }

    // Один сэмпл потребления лицензий за 15-мин бакет (вход для LicenseUsageSnapshot).
    internal readonly record struct UsageSample(int ConsumedMin, int ConsumedMax, double ConsumedAvg, int Limit);

    // Потолок лицензий тенанта ДЛЯ ГРАФИКОВ в perf-режиме — намеренно отвязан от enforcement-
    // лимита (1/1e6). Детерминирован от индекса → диапазон 10..55, чтобы perf-график /reports
    // был осмысленным. В realistic-режиме НЕ используется (Limit = реальному лимиту тенанта).
    public static int UsageLimitFor(int tenantIndex) => 10 + (tenantIndex % 10) * 5;

    // Perf-перегрузка (1:1 вызов из UsageSeeder в perf-режиме): decoupled лимит, не over-limit.
    public static UsageSample BuildUsageSample(int tenantIndex, DateTime bucketStartUtc)
        => BuildUsageSample(tenantIndex, UsageLimitFor(tenantIndex), overLimit: false, bucketStartUtc);

    // Чистая модель потребления относительно ЗАДАННОГО лимита: суточный профиль (рабочие
    // часы выше, ночь/выходные ниже) × нагрузка × детерминированный шум. Значение зависит
    // только от (tenantIndex, limit, overLimit, bucket) — без последовательного RNG, поэтому
    // порядок обхода неважен и воспроизводим.
    //   • normal      → нагрузка 0.30..0.85 (с шумом <1.0) → держится НИЖЕ лимита;
    //   • over-limit «постоянный» (чётный индекс)  → 1.15..1.40 → всегда ВЫШЕ лимита (и «сейчас»);
    //   • over-limit «пиковый»     (нечётный)      → 0.6..1.6  → превышает только в рабочие часы.
    public static UsageSample BuildUsageSample(
        int tenantIndex, int limit, bool overLimit, DateTime bucketStartUtc)
    {
        var hour = bucketStartUtc.Hour + (bucketStartUtc.Minute / 60.0);
        var daily = Math.Exp(-Math.Pow((hour - 13.0) / 5.0, 2)); // 0..1, пик в 13:00
        var weekend = bucketStartUtc.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;
        var weekly = weekend ? 0.30 : 1.0;

        const double idleFloor = 0.08; // небольшой ночной фон
        var activity = idleFloor + ((1 - idleFloor) * daily * weekly); // 0.08..1.0

        double load;
        if (!overLimit)
        {
            // Кап подобран так, чтобы даже ConsumedMax (avg + spread, с шумом) не превышал лимит
            // → normal-тенанты НИКОГДА не «красные» (over-limit «сейчас» = ровно помеченные).
            load = 0.25 + (0.45 * activity);                 // 0.27..0.70 — заведомо ниже лимита
        }
        else if (tenantIndex % 2 == 0)
        {
            load = 1.15 + (0.25 * activity);                 // 1.17..1.40 — постоянно выше
        }
        else
        {
            load = 0.60 + (1.00 * activity);                 // 0.68..1.60 — выше только в пик
        }

        var noise = DeterministicNoise(tenantIndex, bucketStartUtc); // -0.15..0.15
        var avg = Math.Max(0.0, limit * load * (1 + noise));
        var spread = Math.Max(1.0, avg * 0.12);
        var max = (int)Math.Ceiling(avg + spread);
        var min = (int)Math.Max(0, Math.Floor(avg - spread));
        return new UsageSample(min, max, avg, limit);
    }

    // СМБ-распределение лимитов лицензий (выбор пользователя): ~60% мелкие 5..20, ~30% средние
    // 25..60, ~10% крупные 75..150. Всё ∈ [0,100000] (= валидация [Range(0,100_000)]).
    private static int BuildRealisticLimit(Random rng)
    {
        var roll = rng.NextDouble();
        if (roll < 0.60)
        {
            return rng.Next(5, 21);
        }

        return roll < 0.90 ? rng.Next(25, 61) : rng.Next(75, 151);
    }

    // Детерминированное правдоподобное имя клиента (форма × основа), уникальное для i: сначала
    // перебираются все комбинации, затем добавляется числовой суффикс. Без rng — на поток не влияет.
    private static string BuildRealisticName(int index)
    {
        var combos = NameForms.Length * NameStems.Length;
        var combo = index % combos;
        var form = NameForms[combo % NameForms.Length];
        var stem = NameStems[combo / NameForms.Length];
        var cycle = index / combos;
        return cycle == 0 ? $"{form} «{stem}»" : $"{form} «{stem} {cycle + 1}»";
    }

    private static readonly string[] NameForms = ["ООО", "АО", "ПАО", "ИП"];

    private static readonly string[] NameStems =
    [
        "Ромашка", "Сибирь", "Альфа-Трейд", "ТехноСервис", "Прогресс", "Вектор", "Гарант",
        "Меридиан", "Стройинвест", "АгроХолдинг", "Логистик", "МедСервис", "ФинГрупп", "Энергия",
        "Уралмаш-Сервис", "Дом Книги", "Зелёный Сад", "Свежий Хлеб", "АвтоМир", "МастерОк",
        "Уют", "Фортуна", "Каскад", "Орбита", "Юпитер", "Атлант", "Бриз", "Лидер", "Капитал",
        "Союз", "Регион-Торг", "ПромСнаб", "ТоргСервис", "Веста", "Кристалл", "Магнат", "Олимп",
        "Пирамида", "Эталон", "Феникс",
    ];

    // 15-минутный бакет (как у боевого LicenseUsageAccumulator) — общий хелпер для профилей и UsageSeeder.
    internal static DateTime FloorToBucket(DateTime utc)
    {
        var minute = utc.Minute - (utc.Minute % 15);
        return new DateTime(utc.Year, utc.Month, utc.Day, utc.Hour, minute, 0, DateTimeKind.Utc);
    }

    // Профиль каждого тенанта: лимит = MaxConcurrentLicenses (coupled), over-limit = первые
    // overLimitCount, current = потребление «сейчас» (floor(now)) — оно задаёт число живых сессий.
    private static List<TenantUsageProfile> BuildProfiles(
        int overLimitCount,
        List<Tenant> tenants,
        List<Infobase> infobases,
        List<int>[] infobasesByTenant,
        DateTime nowUtc)
    {
        var currentBucket = FloorToBucket(nowUtc);
        var profiles = new List<TenantUsageProfile>(tenants.Count);
        for (var t = 0; t < tenants.Count; t++)
        {
            var limit = tenants[t].MaxConcurrentLicenses;
            var overLimit = t < overLimitCount;
            var clusterIds = infobasesByTenant[t].Select(j => infobases[j].ClusterInfobaseId).ToList();
            var current = BuildUsageSample(t, limit, overLimit, currentBucket).ConsumedMax;
            profiles.Add(new TenantUsageProfile(tenants[t].Id, t, limit, overLimit, current, clusterIds));
        }

        return profiles;
    }

    // Realistic-сессии: ровно CurrentConsumed живых сессий на тенанта, round-robin по его
    // инфобазам → правый край /reports совпадает с live-снимком дашборда; over-limit тенанты
    // дают сессий больше лимита (kill-демо).
    private static List<ScenarioSession> BuildRealisticSessions(
        IReadOnlyList<TenantUsageProfile> profiles, Random rng, DateTime nowUtc)
    {
        var sessions = new List<ScenarioSession>();
        var n = 0;
        foreach (var p in profiles)
        {
            if (p.InfobaseClusterIds.Count == 0)
            {
                continue;
            }

            for (var k = 0; k < p.CurrentConsumed; k++)
            {
                var cluster = p.InfobaseClusterIds[k % p.InfobaseClusterIds.Count];
                sessions.Add(NewSession(cluster, n, rng, nowUtc));
                n++;
            }
        }

        return sessions;
    }

    // Дешёвый детерминированный шум из (tenantIndex, тики бакета) → [-0.15, 0.15].
    private static double DeterministicNoise(int tenantIndex, DateTime ts)
    {
        unchecked
        {
            var h = (uint)tenantIndex * 2654435761u;
            h ^= (uint)(ts.Ticks ^ (ts.Ticks >> 32)) * 2246822519u;
            h ^= h >> 13;
            var unit = (h & 0xFFFF) / 65535.0; // 0..1
            return (unit - 0.5) * 0.30;
        }
    }

    private static readonly AuditActionType[] AuditActions =
    [
        AuditActionType.TenantCreated,
        AuditActionType.TenantUpdated,
        AuditActionType.InfobaseCreated,
        AuditActionType.InfobaseUpdated,
        AuditActionType.PublicationCreated,
        AuditActionType.SessionKilled,
        AuditActionType.LimitChanged,
        AuditActionType.SettingChanged,
        AuditActionType.PublicationDriftDetected,
        AuditActionType.PublicationReconciled,
    ];

    private static List<ScenarioSession> BuildSessions(
        SeedOptions opts,
        int overLimitCount,
        List<Infobase> infobases,
        List<int>[] infobasesByTenant,
        Random rng,
        DateTime nowUtc)
    {
        var sessions = new List<ScenarioSession>(opts.Sessions);
        var produced = 0;

        // Фаза 1: гарантируем, что каждый over-limit тенант с ≥1 инфобазой реально превышает
        // лимит — иначе enforcement/kill-путь не сработает.
        for (var t = 0; t < overLimitCount && produced < opts.Sessions; t++)
        {
            var ibIndices = infobasesByTenant[t];
            if (ibIndices.Count == 0)
            {
                continue;
            }

            for (var q = 0; q < OverLimitSessionQuota && produced < opts.Sessions; q++)
            {
                var ib = infobases[ibIndices[q % ibIndices.Count]];
                sessions.Add(NewSession(ib.ClusterInfobaseId, produced, rng, nowUtc));
                produced++;
            }
        }

        // Фаза 2: остаток — round-robin по всем инфобазам (равномерная фоновая нагрузка).
        var cursor = 0;
        while (produced < opts.Sessions && infobases.Count > 0)
        {
            var ib = infobases[cursor % infobases.Count];
            sessions.Add(NewSession(ib.ClusterInfobaseId, produced, rng, nowUtc));
            produced++;
            cursor++;
        }

        return sessions;
    }

    private static ScenarioSession NewSession(Guid clusterInfobaseId, int n, Random rng, DateTime nowUtc)
        => new(
            SessionId: NextGuid(rng),
            ClusterInfobaseId: clusterInfobaseId,
            AppId: ConsumingAppId,
            UserName: $"user{n:D5}",
            Host: $"host{n % 50:D2}",
            StartedAtUtc: nowUtc.AddMinutes(-rng.Next(600)));

    private static Guid NextGuid(Random rng)
    {
        var bytes = new byte[16];
        rng.NextBytes(bytes);
        return new Guid(bytes);
    }
}

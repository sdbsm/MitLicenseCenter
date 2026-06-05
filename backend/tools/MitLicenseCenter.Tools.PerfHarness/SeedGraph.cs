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
}

// Засеваемый граф (без аудита — он стримится отдельно из-за объёма K) + сценарий для заглушки.
internal sealed record SeedGraph(
    IReadOnlyList<Tenant> Tenants,
    IReadOnlyList<Infobase> Infobases,
    IReadOnlyList<Publication> Publications,
    PerfScenario Scenario);

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
            tenants.Add(new Tenant
            {
                Id = NextGuid(rng),
                Name = $"perf-tenant-{i:D5}",
                MaxConcurrentLicenses = i < overLimitCount ? OverLimitCap : NormalCap,
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
                DatabaseServer = "perf-sql",
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

        var sessions = BuildSessions(opts, overLimitCount, infobases, infobasesByTenant, rng, nowUtc);

        var scenarioInfobases = infobases
            .Select(i => new ScenarioInfobase(i.ClusterInfobaseId, i.Name, null))
            .ToList();

        var scenario = new PerfScenario(NextGuid(rng), sessions, scenarioInfobases);

        return new SeedGraph(tenants, infobases, publications, scenario);
    }

    // Стримим K строк аудита лениво — материализовать миллион в список незачем (батчевая вставка).
    public static IEnumerable<AuditLog> EnumerateAuditLogs(
        SeedOptions opts, IReadOnlyList<Guid> tenantIds, DateTime nowUtc)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(opts.Audit);

        // Отдельный rng со смещённым seed → аудит независим от графа, но воспроизводим.
        var rng = new Random(opts.Seed ^ 0x5EED);
        const int retentionDays = 365;
        var retentionMinutes = retentionDays * 24 * 60;

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

namespace MitLicenseCenter.Application.Clusters;

public interface IClusterClient
{
    Task<IReadOnlyList<ClusterSession>> ListActiveSessionsAsync(CancellationToken ct);

    // ADR-48 (MLC-166): множество session-GUID, реально потребляющих клиентскую лицензию,
    // по факту `rac session list --licenses`. Нелицензионные сеансы в вывод rac не
    // попадают, поэтому отсутствие id в множестве = «не потребляет». Возврат null =
    // «факт недоступен» (exit≠0 / таймаут / нет прав) — холодный тир ставит всем сеансам
    // Pending и приостанавливает enforcement (не рубит вслепую). Отдельная проекция rac
    // **без поля infobase** — поэтому сшивается с ListActiveSessionsAsync по SessionId.
    Task<IReadOnlySet<Guid>?> ListLicensedSessionIdsAsync(CancellationToken ct);

    Task<KillSessionResult> KillSessionAsync(SessionDescriptor descriptor, CancellationToken ct);
    Task<ClusterPingResult> PingAsync(CancellationToken ct);

    // Discovery: перечень инфобаз, зарегистрированных в кластере 1С.
    // Используется формами вместо ручного ввода UUID инфобазы. Работает с теми
    // же cluster-admin кредами, что и остальные команды (BuildArgsWithAuth).
    Task<ClusterInfobaseDiscoveryResult> ListInfobasesAsync(CancellationToken ct);

    // Раздел «Быстродействие» (MLC-066, Фаза 2). Live-срез нагрузки сеансов с perf-полями
    // (`rac session list`, тот же спавн что и ListActiveSessionsAsync, но богаче маппинг).
    // Пустой список = rac не настроен/недоступен (best-effort, как ListActiveSessionsAsync).
    Task<IReadOnlyList<OneCSessionLoad>> ListSessionLoadsAsync(CancellationToken ct);

    // Рабочие процессы кластера (`rac process list`) для атрибуции «кто грузит» (MLC-066).
    // **+1 спавн rac.exe на poll** сверх session list (учтено в spawn-бюджете ADR-3.3) —
    // зовётся только live-pull по требованию (вкладка «Быстродействие» открыта), не в фоне.
    // UUID кластера переиспользуется из IClusterUuidCache (без лишнего cluster list).
    Task<IReadOnlyList<OneCProcessLoad>> ListProcessesAsync(CancellationToken ct);
}

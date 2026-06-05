namespace MitLicenseCenter.Application.Publishing;

// Управление жизненным циклом IIS (MLC-047, ADR-24). Server-scope операции, не
// привязанные к конкретной публикации: пул приложений (recycle/start/stop), сайт
// (start/stop/restart) и полный перезапуск IIS (iisreset). Цели берутся из живого
// IIS (discovery), а не из строк публикаций.
//
// Отдельный интерфейс от IIisPublishingService (тот — read-only статус публикации +
// webinst/web.config): другая ось ответственности (хостинг vs публикация) и иной
// scope (сервер vs одна публикация); ADR-4 «публикация read-only» не размывается.
//
// Анти-коррупционная граница (ADR-20): реализация (ServerManager + спавн iisreset.exe)
// живёт в Infrastructure; Web ходит сюда только через этот интерфейс.
public interface IIisLifecycleService
{
    // Discovery: список пулов приложений с текущим состоянием. Может бросить (нет
    // доступа к Metabase / не Windows) — вызывающий эндпоинт ловит и помечает
    // результат недоступным (Available:false), как discovery-сайтов.
    Task<IReadOnlyList<IisAppPoolInfo>> ListApplicationPoolsAsync(CancellationToken ct);

    // Discovery: список сайтов с текущим состоянием. (Отдельно от
    // IIisPublishingService.ListSitesAsync — тому состояние не нужно.)
    Task<IReadOnlyList<IisSiteStateInfo>> ListSitesAsync(CancellationToken ct);

    // Состояние IIS в целом — статус Windows-службы W3SVC (а не ServerManager:
    // после остановки IIS чтение состояния сайтов/пулов через ServerManager падает,
    // а служба надёжно сообщает Started/Stopped). Бросает при недоступности службы.
    Task<IisObjectState> GetServerStateAsync(CancellationToken ct);

    // Пул приложений. Возвращают состояние пула сразу после операции (UI обновит
    // бейдж; точное состояние доедет фоновым refetch'ем). poolName не найден →
    // KeyNotFoundException (эндпоинт → 404). IIS-сбой → COM/Unauthorized (→ 409).
    Task<IisObjectState> RecycleApplicationPoolAsync(string poolName, CancellationToken ct);
    Task<IisObjectState> StartApplicationPoolAsync(string poolName, CancellationToken ct);
    Task<IisObjectState> StopApplicationPoolAsync(string poolName, CancellationToken ct);

    // Сайт. RestartSite = Stop()+Start() атомарно за границей (Web не оркестрирует
    // две IIS-операции). siteName не найден → KeyNotFoundException.
    Task<IisObjectState> StartSiteAsync(string siteName, CancellationToken ct);
    Task<IisObjectState> StopSiteAsync(string siteName, CancellationToken ct);
    Task<IisObjectState> RestartSiteAsync(string siteName, CancellationToken ct);

    // Полный перезапуск/остановка/запуск IIS через iisreset.exe (W3SVC/WAS —
    // затрагивает ВСЕ сайты сервера). Бросает при ненулевом exit/таймауте (→ 409).
    Task RestartIisAsync(CancellationToken ct);   // iisreset (без аргументов)
    Task StopIisAsync(CancellationToken ct);      // iisreset /stop
    Task StartIisAsync(CancellationToken ct);     // iisreset /start
}

// Состояние объекта IIS (зеркалит Microsoft.Web.Administration.ObjectState без утечки
// инфраструктурного типа в Application/Web). Сериализуется именем (JsonStringEnumConverter).
public enum IisObjectState
{
    Unknown = 0,
    Starting,
    Started,
    Stopping,
    Stopped,
}

public sealed record IisAppPoolInfo(string Name, IisObjectState State);

public sealed record IisSiteStateInfo(string SiteName, IisObjectState State);

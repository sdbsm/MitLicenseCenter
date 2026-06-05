using System.Runtime.Versioning;
using Microsoft.Extensions.Logging;
using Microsoft.Web.Administration;
using MitLicenseCenter.Application.Publishing;
using MitLicenseCenter.Application.Settings;
using MitLicenseCenter.Domain.Publications;
using MitLicenseCenter.Domain.Settings;

namespace MitLicenseCenter.Infrastructure.Publishing;

// Реальный IIS-адаптер (MLC-045). Делает две вещи:
//   1) ReadActualStateAsync — read-only: читает факт публикации (сайт/vdir/web.config,
//      версия платформы из пути к wsisapi.dll). Ничего не меняет.
//   2) ChangePlatformAsync — правит ТОЛЬКО version-сегмент пути к wsisapi.dll в
//      web.config (fallback — default.vrd). Содержательно default.vrd не трогается.
// Создание/перезапись публикаций — НЕ здесь, а через webinst (OneCWebinstPublisher).
//
// Windows-only: ServerManager доступен только на Windows. Тесты, не зависящие от
// IIS, идут через WsisapiVersionRewriter/PublicationStatusEvaluator (pure helpers).
[SupportedOSPlatform("windows")]
internal sealed partial class OneCIisPublishingService : IIisPublishingService
{
    private readonly ISettingsSnapshot _settings;
    private readonly ILogger<OneCIisPublishingService> _logger;

    public OneCIisPublishingService(ISettingsSnapshot settings, ILogger<OneCIisPublishingService> logger)
    {
        _settings = settings;
        _logger = logger;
    }

    public Task<PublicationActualState> ReadActualStateAsync(Publication publication, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(publication);
        return Task.FromResult(ReadActualState(publication));
    }

    public Task ChangePlatformAsync(Publication publication, string newVersion, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(publication);
        ArgumentException.ThrowIfNullOrWhiteSpace(newVersion);
        ChangePlatform(publication, newVersion);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<IisSiteInfo>> ListSitesAsync(CancellationToken ct)
    {
        // Исключения (нет доступа к Metabase, COM) пробрасываются — discovery-эндпоинт
        // их ловит и помечает результат недоступным.
        using var sm = new ServerManager();
        var sites = sm.Sites
            .Select(site => new IisSiteInfo(site.Name))
            .ToList();
        return Task.FromResult<IReadOnlyList<IisSiteInfo>>(sites);
    }

    private PublicationActualState ReadActualState(Publication publication)
    {
        try
        {
            using var sm = new ServerManager();
            var site = sm.Sites[publication.SiteName];
            if (site is null)
            {
                return new PublicationActualState(
                    SiteExists: false,
                    VirtualPathExists: false,
                    WebConfigExists: false,
                    PlatformVersion: null,
                    Error: null);
            }

            var virtualPathExists = SiteHasVirtualPath(site, publication.VirtualPath);

            var webConfigPath = ResolveWebConfigPath(publication);
            var vrdPath = ResolveVrdPath(publication);
            var webConfigExists = File.Exists(webConfigPath);

            // Версию платформы ищем сначала в web.config (современные сборки держат
            // ISAPI-handler там), затем — в default.vrd (старые сборки).
            string? platformVersion = null;
            if (webConfigExists)
            {
                platformVersion = WsisapiVersionRewriter.TryReadVersion(File.ReadAllText(webConfigPath));
            }
            if (platformVersion is null && File.Exists(vrdPath))
            {
                platformVersion = WsisapiVersionRewriter.TryReadVersion(File.ReadAllText(vrdPath));
            }

            return new PublicationActualState(
                SiteExists: true,
                VirtualPathExists: virtualPathExists,
                WebConfigExists: webConfigExists,
                PlatformVersion: platformVersion,
                Error: null);
        }
        catch (UnauthorizedAccessException ex)
        {
            LogIisAccessDenied(_logger, ex);
            return ErrorState(ex.Message);
        }
        catch (System.Runtime.InteropServices.COMException ex)
        {
            LogIisMetabaseFailure(_logger, ex);
            return ErrorState(ex.Message);
        }
        catch (IOException ex)
        {
            LogVrdReadFailure(_logger, ex);
            return ErrorState(ex.Message);
        }
    }

    private void ChangePlatform(Publication publication, string newVersion)
    {
        var webConfigPath = ResolveWebConfigPath(publication);
        var vrdPath = ResolveVrdPath(publication);

        if (!File.Exists(webConfigPath) && !File.Exists(vrdPath))
        {
            throw new FileNotFoundException(
                $"Файлы публикации «{publication.SiteName}{publication.VirtualPath}» не найдены " +
                $"(web.config / default.vrd). Сначала опубликуйте инфобазу.",
                webConfigPath);
        }

        // Современные сборки держат путь к wsisapi.dll в web.config; старые — в
        // default.vrd. Патчим оба, что нашлись и содержат путь. Хотя бы один должен
        // содержать version-сегмент, иначе менять нечего.
        var patchedAny = PatchWsisapiVersion(webConfigPath, newVersion)
                         | PatchWsisapiVersion(vrdPath, newVersion);
        if (!patchedAny)
        {
            throw new InvalidOperationException(
                "В web.config/default.vrd не найден путь к wsisapi.dll — версию платформы изменить не удалось.");
        }
    }

    // Перезаписывает version-сегмент пути к wsisapi.dll в файле, если он есть.
    // Возвращает true, если файл существует и содержит такой путь (даже если версия
    // уже совпадала — атомарно перезаписываем только при реальном изменении).
    private bool PatchWsisapiVersion(string path, string newVersion)
    {
        if (!File.Exists(path))
            return false;

        var original = File.ReadAllText(path);
        if (WsisapiVersionRewriter.TryReadVersion(original) is null)
            return false;

        var patched = WsisapiVersionRewriter.Rewrite(original, newVersion);
        if (string.Equals(original, patched, StringComparison.Ordinal))
        {
            LogPlatformUpToDate(_logger, path);
            return true;
        }

        var tmp = path + ".mlc.tmp";
        File.WriteAllText(tmp, patched);
        File.Replace(tmp, path, destinationBackupFileName: null);
        LogPlatformPatched(_logger, path, newVersion);
        return true;
    }

    private string ResolveVrdPath(Publication publication) =>
        VrdPathResolver.Resolve(
            publication.PhysicalPathOverride,
            DefaultVrdRoot(),
            publication.SiteName,
            publication.VirtualPath);

    private string ResolveWebConfigPath(Publication publication) =>
        VrdPathResolver.ResolveWebConfig(
            publication.PhysicalPathOverride,
            DefaultVrdRoot(),
            publication.SiteName,
            publication.VirtualPath);

    private string DefaultVrdRoot() =>
        _settings.GetString(SettingKey.IisDefaultVrdRoot) ?? @"C:\inetpub\wwwroot";

    private static bool SiteHasVirtualPath(Site site, string virtualPath)
    {
        var normalized = virtualPath.StartsWith('/') ? virtualPath : "/" + virtualPath;

        foreach (var app in site.Applications)
        {
            if (string.Equals(app.Path, normalized, StringComparison.OrdinalIgnoreCase))
                return true;
            foreach (var vdir in app.VirtualDirectories)
            {
                if (string.Equals(vdir.Path, normalized, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
        }
        return false;
    }

    private static PublicationActualState ErrorState(string error) => new(
        SiteExists: false,
        VirtualPathExists: false,
        WebConfigExists: false,
        PlatformVersion: null,
        Error: error);

    [LoggerMessage(Level = LogLevel.Warning, Message = "IIS: нет доступа к Metabase/файлу публикации")]
    private static partial void LogIisAccessDenied(ILogger logger, Exception ex);

    [LoggerMessage(Level = LogLevel.Warning, Message = "IIS: ошибка ServerManager (COM)")]
    private static partial void LogIisMetabaseFailure(ILogger logger, Exception ex);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Файл публикации: не удалось прочитать")]
    private static partial void LogVrdReadFailure(ILogger logger, Exception ex);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Путь к wsisapi.dll уже актуален: {Path}")]
    private static partial void LogPlatformUpToDate(ILogger logger, string path);

    [LoggerMessage(Level = LogLevel.Information, Message = "Версия платформы в {Path} изменена на {Version}")]
    private static partial void LogPlatformPatched(ILogger logger, string path, string version);
}

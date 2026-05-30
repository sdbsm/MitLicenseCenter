using System.Runtime.Versioning;
using Microsoft.Extensions.Logging;
using Microsoft.Web.Administration;
using MitLicenseCenter.Application.Publishing;
using MitLicenseCenter.Application.Settings;
using MitLicenseCenter.Domain.Publications;
using MitLicenseCenter.Domain.Settings;

namespace MitLicenseCenter.Infrastructure.Publishing;

// Реальный IIS-адаптер (PR 3.5): читает desired-state из Publication,
// сравнивает с фактическим состоянием IIS-сайта + default.vrd, применяет
// surgical XML-patch. Никогда не запускает webinst, никогда не overwrite'ит
// файл целиком — см. memory/infrastructure_integration.md и ADR-4.1.
//
// VRD-path layout (ADR-4.1 + PR 4.1): если у Publication задан PhysicalPathOverride —
// используется он (папка IIS-приложения). Иначе convention-fallback:
// {IIS.DefaultVrdRoot}/{siteName}/{trimmedVirtualPath}/default.vrd.
// Resolver вынесен в VrdPathResolver (pure static, unit-testable без IIS).
//
// Windows-only: ServerManager доступен только на Windows. Тесты, не зависящие
// от IIS, идут через VrdPatcher/PublicationDriftDetector напрямую (pure helpers).
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

    public Task ApplyDesiredStateAsync(Publication publication, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(publication);
        ApplyDesiredState(publication);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<IisSiteInfo>> ListSitesAsync(CancellationToken ct)
    {
        // Исключения (нет доступа к Metabase, COM) пробрасываются — discovery-эндпоинт
        // их ловит и помечает результат недоступным. Здесь не swallow'им, чтобы
        // отличить «нет сайтов» от «не смогли прочитать».
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
                    PlatformVersion: null,
                    EnableOData: false,
                    EnableHttpServices: false,
                    VrdContent: null,
                    Error: null);
            }

            var virtualPathExists = SiteHasVirtualPath(site, publication.VirtualPath);
            var vrdPath = ResolveVrdPath(publication);
            string? vrdContent = null;
            string? platformVersion = null;
            var enableOData = false;
            var enableHttpServices = false;

            if (File.Exists(vrdPath))
            {
                vrdContent = File.ReadAllText(vrdPath);
                platformVersion = VrdPatcher.TryReadPlatformVersion(vrdContent);
                VrdPatcher.TryReadODataEnabled(vrdContent, out enableOData);
                VrdPatcher.TryReadHttpServicesEnabled(vrdContent, out enableHttpServices);
            }

            return new PublicationActualState(
                SiteExists: true,
                VirtualPathExists: virtualPathExists,
                PlatformVersion: platformVersion,
                EnableOData: enableOData,
                EnableHttpServices: enableHttpServices,
                VrdContent: vrdContent,
                Error: null);
        }
        catch (UnauthorizedAccessException ex)
        {
            LogIisAccessDenied(_logger, ex);
            return ErrorState(ex.Message);
        }
        catch (System.Runtime.InteropServices.COMException ex)
        {
            // IIS Metabase / ServerManager сообщает COM-исключение, если у нас
            // нет прав или host недоступен. Не считаем это «дрейфом» — это Error.
            LogIisMetabaseFailure(_logger, ex);
            return ErrorState(ex.Message);
        }
        catch (IOException ex)
        {
            LogVrdReadFailure(_logger, ex);
            return ErrorState(ex.Message);
        }
    }

    private void ApplyDesiredState(Publication publication)
    {
        var vrdPath = ResolveVrdPath(publication);
        if (!File.Exists(vrdPath))
        {
            throw new FileNotFoundException(
                $"default.vrd не найден по пути «{vrdPath}». Создание новых публикаций через webinst запрещено — публикация должна быть создана вручную.",
                vrdPath);
        }

        var original = File.ReadAllText(vrdPath);
        var patched = VrdPatcher.Patch(original, publication);
        if (string.Equals(original, patched, StringComparison.Ordinal))
        {
            // Idempotent: ничего не изменилось — не трогаем файл, чтобы не
            // обновлять mtime и не сбивать оператору диагностику.
            LogVrdUpToDate(_logger, vrdPath);
            return;
        }

        // Защитное сохранение: пишем во временный файл рядом и атомарно
        // подменяем — даже при сбое после write мы не оставим VRD пустым.
        var tmp = vrdPath + ".mlc.tmp";
        File.WriteAllText(tmp, patched);
        File.Replace(tmp, vrdPath, destinationBackupFileName: null);
        LogVrdPatched(_logger, vrdPath);
    }

    private string ResolveVrdPath(Publication publication) =>
        VrdPathResolver.Resolve(
            publication.PhysicalPathOverride,
            _settings.GetString(SettingKey.IisDefaultVrdRoot) ?? @"C:\inetpub\1c-publications",
            publication.SiteName,
            publication.VirtualPath);

    private static bool SiteHasVirtualPath(Site site, string virtualPath)
    {
        // IIS-приложение определяется path'ом вида "/MyPub". Если оператор
        // зашёл в default web site и публикация — это просто виртуальный
        // каталог под root application'ом, проверяем через `VirtualDirectories`.
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
        PlatformVersion: null,
        EnableOData: false,
        EnableHttpServices: false,
        VrdContent: null,
        Error: error);

    [LoggerMessage(Level = LogLevel.Warning, Message = "IIS: нет доступа к Metabase/файлу default.vrd")]
    private static partial void LogIisAccessDenied(ILogger logger, Exception ex);

    [LoggerMessage(Level = LogLevel.Warning, Message = "IIS: ошибка ServerManager (COM)")]
    private static partial void LogIisMetabaseFailure(ILogger logger, Exception ex);

    [LoggerMessage(Level = LogLevel.Warning, Message = "default.vrd: не удалось прочитать файл")]
    private static partial void LogVrdReadFailure(ILogger logger, Exception ex);

    [LoggerMessage(Level = LogLevel.Debug, Message = "default.vrd up-to-date: {Path}")]
    private static partial void LogVrdUpToDate(ILogger logger, string path);

    [LoggerMessage(Level = LogLevel.Information, Message = "default.vrd согласован: {Path}")]
    private static partial void LogVrdPatched(ILogger logger, string path);
}

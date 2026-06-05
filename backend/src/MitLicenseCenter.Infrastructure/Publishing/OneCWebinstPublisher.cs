using System.Diagnostics;
using System.Text;
using Microsoft.Extensions.Logging;
using MitLicenseCenter.Application.Publishing;
using MitLicenseCenter.Application.Settings;
using MitLicenseCenter.Domain.Infobases;
using MitLicenseCenter.Domain.Publications;
using MitLicenseCenter.Domain.Settings;

namespace MitLicenseCenter.Infrastructure.Publishing;

// Реальный адаптер публикации через webinst.exe (MLC-045, ADR-20). Запускает
// webinst той версии платформы, что указана в публикации, с -publish -iis.
// webinst перезаписывает default.vrd + web.config целиком — поэтому повторная
// публикация безопасна только для Source=Webinst (гейт в эндпоинте).
//
// Кодировка вывода: webinst пишет UTF-16LE (в отличие от rac.exe с OEM/CP866 —
// проверено вручную). Декодируем как Encoding.Unicode, иначе русский текст ошибки
// в лог уходит mojibake'д.
internal sealed partial class OneCWebinstPublisher : IWebinstPublisher
{
    // webinst (создание IIS-приложения + запись vrd/web.config) укладывается в секунды;
    // 60s — щедрый потолок на медленный диск/IIS-метабазу.
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(60);

    private readonly ISettingsSnapshot _settings;
    private readonly ILogger<OneCWebinstPublisher> _logger;

    public OneCWebinstPublisher(ISettingsSnapshot settings, ILogger<OneCWebinstPublisher> logger)
    {
        _settings = settings;
        _logger = logger;
    }

    public async Task<WebinstResult> PublishAsync(Publication publication, Infobase infobase, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(publication);
        ArgumentNullException.ThrowIfNull(infobase);

        var exePath = WebinstExeResolver.TryResolve(publication.PlatformVersion);
        if (exePath is null)
        {
            return WebinstResult.Failed(
                $"webinst.exe для версии платформы {publication.PlatformVersion} не найден. " +
                "Убедитесь, что эта версия 1С установлена на сервере.");
        }

        string connStr;
        try
        {
            var clusterServer = WebinstArgs.ResolveClusterServer(
                _settings.GetString(SettingKey.OneCClusterServer),
                _settings.GetString(SettingKey.OneCRasEndpoint));
            connStr = WebinstArgs.BuildConnStr(clusterServer, infobase.Name);
        }
        catch (InvalidOperationException ex)
        {
            return WebinstResult.Failed(ex.Message);
        }

        var physicalDir = VrdPathResolver.ResolveDirectory(
            publication.PhysicalPathOverride,
            _settings.GetString(SettingKey.IisDefaultVrdRoot) ?? @"C:\inetpub\wwwroot",
            publication.SiteName,
            publication.VirtualPath);

        var args = WebinstArgs.BuildPublish(publication, physicalDir, connStr);

        var (exitCode, stdout, stderr) = await RunAsync(exePath, args, ct).ConfigureAwait(false);
        if (exitCode == 0)
        {
            return WebinstResult.Ok();
        }

        // Полный вывод webinst — в лог сервера (пути, имена ИБ). Наружу — общий
        // санитизированный detail (MLC-009 / ADR-4.1 sanitization pattern).
        LogWebinstFailed(_logger, publication.Id, exitCode, $"{stdout}\n{stderr}".Trim());
        return WebinstResult.Failed(
            "Не удалось опубликовать инфобазу через webinst. Подробности — в журнале сервера.");
    }

    private static async Task<(int ExitCode, string Stdout, string Stderr)> RunAsync(
        string exePath,
        IReadOnlyList<string> arguments,
        CancellationToken ct)
    {
        var psi = new ProcessStartInfo
        {
            FileName = exePath,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };
        foreach (var arg in arguments)
            psi.ArgumentList.Add(arg);

        using var process = new Process { StartInfo = psi };
        process.Start();

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(Timeout);

        await using var registration = timeoutCts.Token.Register(static state =>
        {
            try
            {
                var p = (Process)state!;
                if (!p.HasExited)
                    p.Kill(entireProcessTree: true);
            }
            catch
            {
                // Процесс уже завершился — нормальная гонка WaitForExit/Kill.
            }
        }, process);

        using var stdoutBuf = new MemoryStream();
        using var stderrBuf = new MemoryStream();
        var stdoutTask = process.StandardOutput.BaseStream.CopyToAsync(stdoutBuf, timeoutCts.Token);
        var stderrTask = process.StandardError.BaseStream.CopyToAsync(stderrBuf, timeoutCts.Token);

        try
        {
            await Task.WhenAll(stdoutTask, stderrTask, process.WaitForExitAsync(timeoutCts.Token))
                .ConfigureAwait(false);
            return (
                process.ExitCode,
                Encoding.Unicode.GetString(stdoutBuf.ToArray()),
                Encoding.Unicode.GetString(stderrBuf.ToArray()));
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            // Локальный таймаут — синтетический non-zero exit, как у rac-раннера.
            return (-1, Encoding.Unicode.GetString(stdoutBuf.ToArray()),
                $"webinst.exe не уложился в таймаут {Timeout.TotalSeconds:0}s.");
        }
    }

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "webinst publish {PublicationId}: exit={ExitCode}. Вывод: {Output}")]
    private static partial void LogWebinstFailed(ILogger logger, Guid publicationId, int exitCode, string output);
}

using System.Diagnostics;
using System.Text;
using MitLicenseCenter.Application.Ras;
using MitLicenseCenter.Infrastructure.Clusters;

namespace MitLicenseCenter.Infrastructure.Ras;

// Единственная production-реализация IScProcessRunner. Тонкая обёртка над sc.exe из
// System32. Вывод sc.exe читается сырым потоком и декодируется по активной OEM-кодовой
// странице процесса (CP866 на RU Windows), как у SystemProcessRacRunner/iisreset — иначе
// кириллица в STATE/локализованных подписях приходит mojibake. На «машинные» поля
// (SERVICE_NAME/BINARY_PATH_NAME) это не влияет (латиница), но декод корректен для всего
// вывода. Фиксированный 30s deadline — sc локальный и быстрый.
internal sealed class ScProcessRunner : IScProcessRunner
{
    private static readonly Encoding OemEncoding = SystemProcessRacRunner.ResolveOemEncoding();
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);

    // sc.exe берём по абсолютному пути из System32, а не из PATH (защита от подмены
    // одноимённым исполняемым в рабочем каталоге — sc у нас запускается под SYSTEM).
    private static readonly string ScExePath =
        Path.Combine(Environment.SystemDirectory, "sc.exe");

    public async Task<ScResult> RunAsync(string arguments, CancellationToken ct)
    {
        // Arguments (raw-строка), НЕ ArgumentList: sc.exe-парсер «ключ= значение»
        // (binPath=/start=) несовместим с поэлементным квотированием ArgumentList —
        // .NET обернул бы значение binPath= в кавычки, и sc его отбрасывал (MLC-162).
        // Строку готовит RasServiceCommandBuilder — как установщик в [Code] (ADR-47).
        var psi = new ProcessStartInfo
        {
            FileName = ScExePath,
            Arguments = arguments,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };

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
                {
                    p.Kill(entireProcessTree: true);
                }
            }
            catch
            {
                // Процесс уже завершился — нормально (гонка WaitForExit/Kill).
            }
        }, process);

        // Читаем raw bytes и декодируем явно по OEM (см. SystemProcessRacRunner): прямое
        // StandardOutputEncoding=UTF8 на не-английской OEM не работает.
        using var stdoutBuf = new MemoryStream();
        using var stderrBuf = new MemoryStream();
        var stdoutTask = process.StandardOutput.BaseStream.CopyToAsync(stdoutBuf, timeoutCts.Token);
        var stderrTask = process.StandardError.BaseStream.CopyToAsync(stderrBuf, timeoutCts.Token);

        try
        {
            await Task.WhenAll(stdoutTask, stderrTask, process.WaitForExitAsync(timeoutCts.Token))
                .ConfigureAwait(false);

            return new ScResult(
                ExitCode: process.ExitCode,
                Stdout: OemEncoding.GetString(stdoutBuf.ToArray()),
                Stderr: OemEncoding.GetString(stderrBuf.ToArray()));
        }
        catch (OperationCanceledException)
        {
            if (ct.IsCancellationRequested)
            {
                throw;
            }
            // Локальный таймаут — синтетический non-zero exit (вызывающий пройдёт error-ветку).
            return new ScResult(
                ExitCode: -1,
                Stdout: OemEncoding.GetString(stdoutBuf.ToArray()),
                Stderr: $"sc.exe не уложился в таймаут {Timeout.TotalSeconds:0}с.");
        }
    }
}

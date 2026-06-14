using System.Diagnostics;
using System.Globalization;
using System.Text;
using MitLicenseCenter.Application.Clusters;
using MitLicenseCenter.Infrastructure.Diagnostics;

namespace MitLicenseCenter.Infrastructure.Clusters;

// Единственная production-реализация IRacProcessRunner. Тонкая обёртка над
// System.Diagnostics.Process: stdout/stderr читаются сырым потоком и декодируются по
// активной OEM-кодовой странице процесса (CP866 на RU Windows) с откатом на UTF-8 —
// согласно ADR-3.3 (детали декода — в комментарии к OemEncoding ниже), entireProcessTree:true
// на отмену (rac.exe может спавнить child для gRPC-диалога с RAS, иначе остался бы orphan),
// фиксированный 30s deadline (rac→ras→ragent дольше REST hop'а).
internal sealed class SystemProcessRacRunner : IRacProcessRunner
{
    // rac.exe пишет в активную OEM-кодовую страницу process'а, а не в фиксированный
    // UTF-8 — это поведение унаследовано от консольных приложений Windows.
    // На русскоязычной Windows OEM=CP866, и без явного декода Кириллица превращается
    // в mojibake. Декодируем согласно текущей OEM, чтобы идемпотентный маркер
    // «Сеанс с указанным идентификатором не найден» матчился корректно.
    // (Раньше пробовали ProcessStartInfo.StandardErrorEncoding=UTF8 — не работает,
    // т.к. дочерний процесс не меняет свой output на UTF-8, только StreamReader
    // в parent'е пытается декодить байты как UTF-8 и получает U+FFFD.)
    private static readonly Encoding OemEncoding = ResolveOemEncoding();

    private readonly RacMetrics _metrics;

    public SystemProcessRacRunner(RacMetrics metrics)
    {
        _metrics = metrics;
    }

    public async Task<RacInvocation> RunAsync(
        string exePath,
        IReadOnlyList<string> arguments,
        TimeSpan timeout,
        CancellationToken ct)
    {
        // MLC-037 (PERF-01): засекаем длительность спавна. GetTimestamp() — наносекундный
        // QPC-read, всегда-on без аллокаций. Сама запись метрики — под гардом _metrics.Enabled
        // в конце метода (тег команды не вычисляется, если слушателя нет).
        var startTs = Stopwatch.GetTimestamp();

        var psi = new ProcessStartInfo
        {
            FileName = exePath,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };

        foreach (var arg in arguments)
        {
            psi.ArgumentList.Add(arg);
        }

        using var process = new Process { StartInfo = psi };
        process.Start();

        // Читаем raw bytes из StandardOutput.BaseStream / StandardError.BaseStream
        // и явно декодим UTF-8. Так обходим случай, когда `StandardErrorEncoding`
        // в ProcessStartInfo игнорируется (наблюдается на Windows с не-английской
        // OEM code page: StreamReader использует CP866 вместо UTF-8 — Russian
        // text приходит mojibake'д, и идемпотентный маркер «Сеанс… не найден»
        // не матчится). Выходы rac.exe всегда мелкие (≤ единицы KB), CopyToAsync
        // в MemoryStream без deadlock'а pipe-buffer'а.
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(timeout);

        // entireProcessTree:true — обязательно: rac.exe держит child для RAS-диалога,
        // killing parent only оставит orphan, висящий до своего собственного таймаута.
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
                // Process уже завершился — нормально, гонка между WaitForExit и Kill.
            }
        }, process);

        using var stdoutBuf = new MemoryStream();
        using var stderrBuf = new MemoryStream();
        var stdoutTask = process.StandardOutput.BaseStream.CopyToAsync(stdoutBuf, timeoutCts.Token);
        var stderrTask = process.StandardError.BaseStream.CopyToAsync(stderrBuf, timeoutCts.Token);

        RacInvocation result;
        string outcome;
        try
        {
            await Task.WhenAll(stdoutTask, stderrTask, process.WaitForExitAsync(timeoutCts.Token))
                .ConfigureAwait(false);

            result = new RacInvocation(
                ExitCode: process.ExitCode,
                Stdout: OemEncoding.GetString(stdoutBuf.ToArray()),
                Stderr: OemEncoding.GetString(stderrBuf.ToArray()));
            outcome = process.ExitCode == 0 ? RacMetrics.OutcomeOk : RacMetrics.OutcomeFailed;
        }
        catch (OperationCanceledException)
        {
            if (ct.IsCancellationRequested)
            {
                // Внешняя отмена — пробрасываем дальше (метрику не пишем).
                throw;
            }
            // Локальный таймаут (timeoutCts сработал, но не родительский ct) —
            // возвращаем синтетический "non-zero exit" чтобы вызывающий код прошёл
            // ту же error-ветку, что и при failure от rac (empty list / circuit-open).
            result = new RacInvocation(
                ExitCode: -1,
                Stdout: OemEncoding.GetString(stdoutBuf.ToArray()),
                Stderr: $"rac.exe не уложился в таймаут {timeout.TotalSeconds:0}s.");
            outcome = RacMetrics.OutcomeTimeout;
        }

        // MLC-037 (PERF-01): единственная точка записи спавн-метрики. Тег команды и
        // запись считаются только при активном слушателе → near-zero overhead в проде.
        if (_metrics.Enabled)
        {
            _metrics.Record(
                RacCommandTag.For(arguments),
                Stopwatch.GetElapsedTime(startTs).TotalMilliseconds,
                outcome);
        }

        return result;
    }

    // internal (а не private) только ради юнит-теста CP866-декода (BE-12, MLC-120):
    // тест round-trip'ит идемпотентный маркер «Сеанс… не найден» через эту кодировку.
    // InternalsVisibleTo на MitLicenseCenter.Tests.Unit уже объявлен в .csproj. Поведение
    // декода НЕ меняется.
    internal static Encoding ResolveOemEncoding()
    {
        // .NET Core+ требует регистрации code-page providers для CP866/CP1251/etc.
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        try
        {
            var oemCp = CultureInfo.CurrentCulture.TextInfo.OEMCodePage;
            if (oemCp > 0)
            {
                return Encoding.GetEncoding(oemCp);
            }
        }
        catch (ArgumentException)
        {
            // Неизвестная кодовая страница → UTF-8 как разумный дефолт.
        }
        return Encoding.UTF8;
    }
}

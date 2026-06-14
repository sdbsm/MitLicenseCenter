namespace MitLicenseCenter.Application.Ras;

// Абстракция над спавном sc.exe для testability ScRasServiceManager — тот же приём,
// что IRacProcessRunner для rac.exe. Production-реализация (ScProcessRunner,
// Infrastructure) декодирует вывод по активной OEM-кодовой странице (CP866 на RU
// Windows), как SystemProcessRacRunner. Unit-тесты подсовывают fake, возвращающий
// заранее заготовленный ScResult по имени sub-команды (query/qc/create/...).
public interface IScProcessRunner
{
    Task<ScResult> RunAsync(IReadOnlyList<string> arguments, CancellationToken ct);
}

public sealed record ScResult(int ExitCode, string Stdout, string Stderr);

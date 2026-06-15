namespace MitLicenseCenter.Application.Ras;

// Абстракция над спавном sc.exe для testability ScRasServiceManager — тот же приём,
// что IRacProcessRunner для rac.exe. Production-реализация (ScProcessRunner,
// Infrastructure) декодирует вывод по активной OEM-кодовой странице (CP866 на RU
// Windows), как SystemProcessRacRunner. Unit-тесты подсовывают fake, возвращающий
// заранее заготовленный ScResult по sub-команде (create/config/start/stop).
//
// Аргументы передаются ОДНОЙ raw-строкой в ProcessStartInfo.Arguments (НЕ ArgumentList):
// у sc.exe нестандартный парсер опций «ключ= значение» (binPath=/start=), и .NET-овское
// поэлементное квотирование ArgumentList ломало значение binPath= (MLC-162). Строку
// собирает RasServiceCommandBuilder — ровно как установщик в [Code] (ADR-47).
public interface IScProcessRunner
{
    Task<ScResult> RunAsync(string arguments, CancellationToken ct);
}

public sealed record ScResult(int ExitCode, string Stdout, string Stderr);

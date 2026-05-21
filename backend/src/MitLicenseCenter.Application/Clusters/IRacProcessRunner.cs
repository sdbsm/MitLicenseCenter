namespace MitLicenseCenter.Application.Clusters;

// Абстракция над System.Diagnostics.Process для testability адаптера PR 3.8.
// Production-реализация — SystemProcessRacRunner в Infrastructure/Clusters/.
// Unit-тесты подсовывают fake, возвращающий заранее заготовленный RacInvocation.
public interface IRacProcessRunner
{
    Task<RacInvocation> RunAsync(
        string exePath,
        IReadOnlyList<string> arguments,
        TimeSpan timeout,
        CancellationToken ct);
}

public sealed record RacInvocation(int ExitCode, string Stdout, string Stderr);

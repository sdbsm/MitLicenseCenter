namespace MitLicenseCenter.Infrastructure.Server;

// Таймауты верификации состояния службы для WindowsServiceController (ADR-55).
// VerificationTimeout — общий бюджет ожидания целевого состояния (дефолт 30с, как
// «sc 30с» в ADR-55); PollInterval — пауза между опросами ServiceController. Вынесено
// в record, чтобы тест таймаута/полинга подменял мгновенными значениями через ctor.
internal sealed record WindowsServiceControllerOptions
{
    public TimeSpan PollInterval { get; init; } = TimeSpan.FromMilliseconds(500);

    public TimeSpan VerificationTimeout { get; init; } = TimeSpan.FromSeconds(30);
}

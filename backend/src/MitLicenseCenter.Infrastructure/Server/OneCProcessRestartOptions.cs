namespace MitLicenseCenter.Infrastructure.Server;

// Таймауты верификации исчезновения Pid для OneCProcessRestartService (MLC-220, ADR-56,
// дух ADR-55). VerificationTimeout — общий бюджет ожидания, пока кластер 1С заменит
// завершённый rphost (старый Pid исчезнет из rac process list); дефолт ~30с (как «sc 30с»
// в ADR-55). PollInterval — пауза между опросами rac process list. Вынесено в record,
// чтобы тесты подменяли мгновенными значениями через ctor (FakeTimeProvider не подключён —
// тесты управляют TimeProvider вручную / мизерными опциями, как WindowsServiceController).
internal sealed record OneCProcessRestartOptions
{
    public TimeSpan PollInterval { get; init; } = TimeSpan.FromSeconds(1);

    public TimeSpan VerificationTimeout { get; init; } = TimeSpan.FromSeconds(30);
}

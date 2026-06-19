namespace MitLicenseCenter.Infrastructure.Server;

// Таймауты верификации состояния службы для WindowsServiceController (ADR-55).
// VerificationTimeout — общий бюджет ожидания ЦЕЛЕВОГО СОСТОЯНИЯ службы (не путать с
// таймаутом самой команды sc.exe в ScProcessRunner: `sc start/stop` возвращается мгновенно).
// Дефолт 300с (MLC-224): холодный старт/стоп КЛАСТЕРА 1С (ragent + rmngr + rphost,
// освобождение портов 1540/1541, слив сеансов) на нагруженном сервере идёт МИНУТАМИ — при
// 30с панель ложно рапортовала «не успела запуститься/остановиться», хотя служба поднималась.
// Полл идёт каждые PollInterval и возвращается СРАЗУ при достижении состояния, поэтому высокий
// потолок не замедляет нормальный случай — он лишь отодвигает момент объявления реального отказа.
// PollInterval — пауза между опросами ServiceController. Record — чтобы тест таймаута/полинга
// подменял мгновенными значениями через ctor.
internal sealed record WindowsServiceControllerOptions
{
    public TimeSpan PollInterval { get; init; } = TimeSpan.FromMilliseconds(500);

    public TimeSpan VerificationTimeout { get; init; } = TimeSpan.FromSeconds(300);
}

using Hangfire;

namespace MitLicenseCenter.Application.Jobs;

// Hangfire-job (MLC-218, ADR-55): ночной профилактический рестарт сервера 1С. Cron строится
// из настройки OneC.AutoRestart.Time (ежедневно в HH:mm по местному поясу хоста, ADR-52);
// регистрация/снятие — RecurringJob.AddOrUpdate / RemoveIfExists на старте и при изменении
// настройки через эндпоинт /server/auto-restart (НЕ тик-каждые-5-минут).
public interface IOneCAutoRestartJob
{
    // Тело джобы: проверяет OneC.AutoRestart.Enabled (выкл → no-op, защита от рассинхрона
    // расписания), находит запущенные службы ragent через discovery и рестартит каждую через
    // IWindowsServiceController, пишет аудит срабатывания (initiator "system") и обновляет
    // OneC.AutoRestart.LastRunUtc.
    //
    // [AutomaticRetry(Attempts = 0)]: ретраи Hangfire тут вредны — рестарт сервера 1С
    // разрушителен (прерывает все базы узла), повторять при сбое не нужно; самоисцеление —
    // следующий суточный прогон. Атрибут на методе интерфейса (job зарегистрирован через
    // интерфейс — RecurringJob.AddOrUpdate<IOneCAutoRestartJob>, Hangfire берёт фильтры с него).
    [AutomaticRetry(Attempts = 0)]
    Task RunAsync(CancellationToken ct);
}

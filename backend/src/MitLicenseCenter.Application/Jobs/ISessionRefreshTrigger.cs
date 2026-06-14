namespace MitLicenseCenter.Application.Jobs;

// MLC-156: порт для кнопки «Обновить сейчас» на /sessions. Эндпоинт POST /sessions/refresh
// (Web) форсит немедленный cold-обход 1С и ждёт его завершения, после чего фронт перечитывает
// свежий снимок. Реализация — ColdTierPollingService (Infrastructure): прерывает ожидание
// таймера cold-петли, прогоняет цикл сейчас. Через Application-интерфейс Web не ломает
// границу слоёв (ADR-5/16/20): не ссылается на Infrastructure.Jobs напрямую.
public interface ISessionRefreshTrigger
{
    // Запросить немедленный cold-прогон и дождаться его завершения. Single-flight:
    // несколько одновременных вызовов коалесцируются в один ближайший прогон —
    // все ждущие завершаются вместе по его окончании.
    Task RunNowAsync(CancellationToken ct);
}

namespace MitLicenseCenter.Infrastructure.Ras;

// Состояние службы Windows по её имени: запущена ли (для маппинга «Остановлена») и
// отображаемое имя. Вынесено за интерфейс, чтобы ScRasServiceManager тестировался
// без обращения к реальному менеджеру служб. Production-реализация — тонкая обёртка
// над System.ServiceProcess.ServiceController (ADR-47, Update MLC-162).
internal interface IServiceStateReader
{
    // null, если службы с таким именем нет (исчезла между чтением реестра и
    // проверкой состояния) или состояние недоступно.
    ServiceState? ReadState(string serviceName);
}

// Снимок состояния службы: IsRunning (Status==Running) + IsStopped (Status==Stopped) +
// DisplayName (best-effort). ВАЖНО (MLC-225): IsRunning и IsStopped — НЕ инверсии друг друга:
// переходные состояния StartPending/StopPending дают оба false. Различать их обязательно для
// рестарта — фазу остановки нельзя считать завершённой на StopPending (иначе `sc start` уйдёт
// в ещё останавливающуюся службу и не подействует).
internal readonly record struct ServiceState(bool IsRunning, bool IsStopped, string? DisplayName);

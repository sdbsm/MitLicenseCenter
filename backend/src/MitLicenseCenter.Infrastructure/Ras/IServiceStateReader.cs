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

// Снимок состояния службы: running + DisplayName (best-effort).
internal readonly record struct ServiceState(bool IsRunning, string? DisplayName);

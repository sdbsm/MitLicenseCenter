using System.Runtime.Versioning;
using System.ServiceProcess;

namespace MitLicenseCenter.Infrastructure.Ras;

// Production-реализация IServiceStateReader поверх System.ServiceProcess.ServiceController
// (тот же приём, что OneCIisLifecycleService для W3SVC). Running = ServiceControllerStatus
// .Running. DisplayName берём из контроллера (best-effort). Windows-only — guard CA1416.
internal sealed class ServiceControllerStateReader : IServiceStateReader
{
    public ServiceState? ReadState(string serviceName)
    {
        if (!OperatingSystem.IsWindows())
        {
            return null;
        }

        return ReadStateWindows(serviceName);
    }

    [SupportedOSPlatform("windows")]
    private static ServiceState? ReadStateWindows(string serviceName)
    {
        try
        {
            using var controller = new ServiceController(serviceName);
            // Обращение к Status/DisplayName бросает InvalidOperationException, если
            // службы с таким именем нет (исчезла между чтением реестра и проверкой).
            // Свежий контроллер на каждый вызов → Status читается «вживую» (без Refresh).
            // IsRunning/IsStopped различают переходные StartPending/StopPending (оба false) —
            // критично для верификации рестарта (MLC-225).
            var status = controller.Status;
            var isRunning = status == ServiceControllerStatus.Running;
            var isStopped = status == ServiceControllerStatus.Stopped;
            var displayName = controller.DisplayName;
            return new ServiceState(isRunning, isStopped, string.IsNullOrWhiteSpace(displayName) ? null : displayName);
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }
}

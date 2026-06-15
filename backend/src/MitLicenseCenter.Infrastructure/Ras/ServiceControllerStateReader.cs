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
            var isRunning = controller.Status == ServiceControllerStatus.Running;
            var displayName = controller.DisplayName;
            return new ServiceState(isRunning, string.IsNullOrWhiteSpace(displayName) ? null : displayName);
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }
}

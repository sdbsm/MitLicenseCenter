using MitLicenseCenter.Application.Publishing;
using MitLicenseCenter.Domain.Publications;

namespace MitLicenseCenter.Infrastructure.Publishing;

// Pure-function helper: вся логика «что считать дрейфом» вынесена сюда,
// чтобы unit-тесты могли её покрыть без IIS/файловой системы. Реальный
// I/O (ReadActualStateAsync) живёт в OneCIisPublishingService и
// возвращает PublicationActualState, который этот компаратор и сравнивает
// с Publication (desired).
internal static class PublicationDriftDetector
{
    // Максимальный размер details, который мы готовы хранить в LastDriftDetails.
    // Audit-description truncate'ится ещё агрессивнее в DriftCheckJob.
    public const int MaxDetailsLength = 1024;

    public static (PublicationDriftStatus Status, string Details) Compare(
        Publication desired,
        PublicationActualState actual)
    {
        ArgumentNullException.ThrowIfNull(desired);
        ArgumentNullException.ThrowIfNull(actual);

        // Error побеждает над всем — адаптер не смог прочитать состояние,
        // значит сравнивать нечего.
        if (!string.IsNullOrWhiteSpace(actual.Error))
        {
            return (PublicationDriftStatus.Error, Truncate($"Ошибка чтения IIS/VRD: {actual.Error}"));
        }

        // Missing: физически отсутствует Site, VirtualPath, или сам default.vrd.
        if (!actual.SiteExists)
        {
            return (PublicationDriftStatus.Missing, Truncate($"IIS-сайт «{desired.SiteName}» не найден."));
        }
        if (!actual.VirtualPathExists)
        {
            return (PublicationDriftStatus.Missing,
                Truncate($"Виртуальный путь «{desired.VirtualPath}» отсутствует в сайте «{desired.SiteName}»."));
        }
        if (actual.VrdContent is null)
        {
            return (PublicationDriftStatus.Missing,
                Truncate($"Файл default.vrd для публикации «{desired.SiteName}{desired.VirtualPath}» не найден."));
        }

        // Сравнение полей — собираем все расхождения в один details-блок,
        // чтобы оператор видел полную картину, а не первую найденную проблему.
        var diffs = new List<string>(3);

        if (!string.IsNullOrWhiteSpace(actual.PlatformVersion)
            && !string.Equals(actual.PlatformVersion, desired.PlatformVersion, StringComparison.Ordinal))
        {
            diffs.Add($"PlatformVersion: ожидается {desired.PlatformVersion}, в VRD {actual.PlatformVersion}.");
        }

        if (actual.EnableOData != desired.EnableOData)
        {
            diffs.Add(desired.EnableOData
                ? "OData включён в desired, но выключен в VRD."
                : "OData выключен в desired, но включён в VRD.");
        }

        if (actual.EnableHttpServices != desired.EnableHttpServices)
        {
            diffs.Add(desired.EnableHttpServices
                ? "HTTP-сервисы включены в desired, но выключены в VRD."
                : "HTTP-сервисы выключены в desired, но включены в VRD.");
        }

        if (diffs.Count == 0)
        {
            return (PublicationDriftStatus.InSync, string.Empty);
        }

        return (PublicationDriftStatus.Drift, Truncate(string.Join(" ", diffs)));
    }

    private static string Truncate(string s) =>
        s.Length <= MaxDetailsLength ? s : string.Concat(s.AsSpan(0, MaxDetailsLength - 1), "…");
}

using MitLicenseCenter.Domain.Infobases;
using MitLicenseCenter.Domain.Publications;

namespace MitLicenseCenter.Infrastructure.Publishing;

// Pure-function helper (MLC-045): сборка аргументов командной строки webinst и
// строки соединения. Вынесено из адаптера, чтобы покрыть unit-тестами без запуска
// процесса. Аргументы передаются через ProcessStartInfo.ArgumentList (без shell),
// поэтому здесь — «сырые» значения без ручного экранирования кавычек.
internal static class WebinstArgs
{
    // Имя виртуального каталога для -wsdir: VirtualPath без ведущего «/».
    public static string VirtualDirName(string virtualPath) =>
        (virtualPath ?? string.Empty).Trim().TrimStart('/').TrimEnd('/');

    // Адрес кластера для строки соединения: явная настройка OneC.Cluster.Server,
    // иначе host из OneC.RAS.Endpoint (host:port → host). Бросает, если ни того,
    // ни другого нет — публиковать без адреса кластера нельзя.
    public static string ResolveClusterServer(string? clusterServerSetting, string? rasEndpoint)
    {
        var explicitServer = (clusterServerSetting ?? string.Empty).Trim();
        if (explicitServer.Length > 0)
            return explicitServer;

        var endpoint = (rasEndpoint ?? string.Empty).Trim();
        if (endpoint.Length > 0)
        {
            // host:port → host. Если порт RAS (обычно 1545) указан — для строки
            // соединения с кластером он не подходит, поэтому отсекаем порт и
            // используем только host (кластер слушает свой порт по умолчанию).
            var colon = endpoint.IndexOf(':');
            return colon > 0 ? endpoint[..colon] : endpoint;
        }

        throw new InvalidOperationException(
            "Не задан адрес 1С-кластера: укажите OneC.Cluster.Server или OneC.RAS.Endpoint в «Параметрах».");
    }

    // Строка соединения 1С для серверной ИБ: Srvr=<кластер>;Ref=<имя ИБ>;
    public static string BuildConnStr(string clusterServer, string infobaseName) =>
        $"Srvr={clusterServer};Ref={infobaseName};";

    // Аргументы публикации: webinst -publish -iis -wsdir <vdir> -dir <физ.путь> -connstr <connstr>
    public static IReadOnlyList<string> BuildPublish(Publication publication, string physicalDir, string connStr)
    {
        ArgumentNullException.ThrowIfNull(publication);
        ArgumentException.ThrowIfNullOrWhiteSpace(physicalDir);
        ArgumentException.ThrowIfNullOrWhiteSpace(connStr);

        return new[]
        {
            "-publish",
            "-iis",
            "-wsdir", VirtualDirName(publication.VirtualPath),
            "-dir", physicalDir,
            "-connstr", connStr,
        };
    }
}

using System.Text;
using MitLicenseCenter.Application.Ras;

namespace MitLicenseCenter.Infrastructure.Ras;

// Сборка строк запуска ras.exe-службы и аргументов sc.exe (ADR-47). Точный синтаксис
// сверен с документацией 1С:
//
//   ras.exe cluster --service --port=<RASport> <agent-host>:<agent-ctrlport>
//
// где <RASport> — порт самой службы RAS (OneC.RAS.Endpoint, дефолт 1545), а
// <agent-host>:<agent-ctrlport> — адрес локального агента кластера ragent (single-host
// → localhost:1540, 1540 — стандартный порт агента кластера). Флаг --service переводит
// ras.exe в режим Windows-службы. Этот синтаксис стабилен для 8.3.x и 8.5.x.
//
// Регистрация через sc create:
//   sc create <name> binPath= "\"<...>\ras.exe\" cluster --service --port=1545 localhost:1540" start= auto
//
// Гочи квотирования (sc binPath= = одна строка, разбираемая SCM):
//   * binPath включает И путь к ras.exe (в кавычках — может содержать пробелы), И
//     аргументы ras.exe. Внешние кавычки самого значения binPath экранируются для sc
//     как \" — иначе sc обрежет значение на первом пробеле.
//   * sc требует ПРОБЕЛ после '=' в binPath=/start=/DisplayName= (значимый синтаксис sc).
//   * obj=/password= НЕ задаём: служба слушает loopback (ADR-28/47), работает под
//     LocalSystem — секрет в команду не попадает (требование аудита ADR-47).
internal static class RasServiceCommandBuilder
{
    // Имя создаваемой нами службы (используется только на register; обнаружение идёт по
    // binPath, не по имени, поэтому имя — внутренняя деталь, не контракт с оператором).
    public const string DefaultServiceName = "MitLicenseRas";

    public const string DefaultDisplayName = "1C:Enterprise Remote Administration Server (MitLicense)";

    // Командная строка ras.exe-службы: «cluster --service --port=<port> <agent>».
    // Возвращается БЕЗ пути к ras.exe (только аргументы) — для предпросмотра и для
    // встраивания в binPath. Аргументы по отдельности — для тестируемой проверки.
    public static IReadOnlyList<string> BuildRasArguments(string port, string agentAddress)
        => new[]
        {
            "cluster",
            "--service",
            $"--port={port}",
            agentAddress,
        };

    // Полная строка запуска ras.exe (путь + аргументы), как она ляжет внутрь binPath.
    // Путь к ras.exe всегда в кавычках (может содержать пробелы — «C:\Program Files\…»).
    public static string BuildRasCommandLine(string rasExePath, string port, string agentAddress)
    {
        var sb = new StringBuilder();
        sb.Append('"').Append(rasExePath).Append('"');
        foreach (var arg in BuildRasArguments(port, agentAddress))
        {
            sb.Append(' ').Append(arg);
        }
        return sb.ToString();
    }

    // Значение для sc binPath= : полная строка запуска ras.exe с экранированными
    // кавычками (\"…\"). sc разбирает binPath= как одну лексему до конца значения;
    // внутренние кавычки вокруг пути экранируются обратным слешем.
    public static string BuildBinPathValue(string rasExePath, string port, string agentAddress)
        => BuildRasCommandLine(rasExePath, port, agentAddress).Replace("\"", "\\\"");

    // Аргументы sc create (чистая регистрация). Каждый элемент — отдельный аргумент
    // процесса; ScProcessRunner кладёт их в ArgumentList (Windows сам соберёт корректную
    // командную строку и не порвёт значения с пробелами/кавычками).
    public static IReadOnlyList<string> BuildScCreateArguments(
        string serviceName, string rasExePath, string port, string agentAddress)
        => new[]
        {
            "create",
            serviceName,
            "binPath=",
            BuildRasCommandLine(rasExePath, port, agentAddress),
            "start=",
            "auto",
            "DisplayName=",
            DefaultDisplayName,
        };

    // Аргументы sc config (перерегистрация существующей службы под новый binPath/порт).
    public static IReadOnlyList<string> BuildScConfigArguments(
        string serviceName, string rasExePath, string port, string agentAddress)
        => new[]
        {
            "config",
            serviceName,
            "binPath=",
            BuildRasCommandLine(rasExePath, port, agentAddress),
            "start=",
            "auto",
        };

    public static IReadOnlyList<string> BuildScStartArguments(string serviceName)
        => new[] { "start", serviceName };

    public static IReadOnlyList<string> BuildScStopArguments(string serviceName)
        => new[] { "stop", serviceName };

    // Человекочитаемая команда sc для предпросмотра в UI (одна строка, как оператор
    // ввёл бы её в консоли). Кавычки binPath= показываем экранированными — ровно так,
    // как их нужно набрать в cmd. Секреты в превью отсутствуют (obj/password не задаём).
    public static string BuildScCreatePreview(
        string serviceName, string rasExePath, string port, string agentAddress)
        => $"sc create {serviceName} binPath= \"{BuildBinPathValue(rasExePath, port, agentAddress)}\" "
           + $"start= auto DisplayName= \"{DefaultDisplayName}\"";

    public static string BuildScConfigPreview(
        string serviceName, string rasExePath, string port, string agentAddress)
        => $"sc stop {serviceName}  &&  "
           + $"sc config {serviceName} binPath= \"{BuildBinPathValue(rasExePath, port, agentAddress)}\" start= auto"
           + $"  &&  sc start {serviceName}";

    public static string BuildScStartPreview(string serviceName)
        => $"sc start {serviceName}";
}

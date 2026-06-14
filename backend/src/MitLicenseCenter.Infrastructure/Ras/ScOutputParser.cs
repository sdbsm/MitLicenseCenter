using System.Text.RegularExpressions;

namespace MitLicenseCenter.Infrastructure.Ras;

// Парсеры вывода sc.exe. Вывод sc локализован (RU Windows → CP866), но имена полей в
// «машинной» форме (SERVICE_NAME, STATE, BINARY_PATH_NAME) sc печатает латиницей и в
// фиксированном формате независимо от языка ОС — на них и опираемся, не на переведённые
// подписи. Декодирование байтов в строку выполняет ScProcessRunner (OEM-кодовая
// страница), сюда приходит уже строка.
internal static partial class ScOutputParser
{
    // SERVICE_NAME: <имя> в выводе `sc query state= all` / `sc queryex`.
    [GeneratedRegex(@"^\s*SERVICE_NAME:\s*(?<name>.+?)\s*$", RegexOptions.Multiline)]
    private static partial Regex ServiceNameRegex();

    // STATE              : 4  RUNNING  — нас интересует числовой код состояния (4 = RUNNING).
    [GeneratedRegex(@"^\s*STATE\s*:\s*(?<code>\d+)\b", RegexOptions.Multiline)]
    private static partial Regex StateRegex();

    // BINARY_PATH_NAME   : <строка запуска службы> (путь + аргументы) из `sc qc <name>`.
    [GeneratedRegex(@"^\s*BINARY_PATH_NAME\s*:\s*(?<path>.+?)\s*$", RegexOptions.Multiline)]
    private static partial Regex BinaryPathRegex();

    // Версия платформы 1С из binPath: ...\1cv8\<N.N.N.N>\bin\ras.exe.
    [GeneratedRegex(@"[\\/]1cv8[\\/](?<version>\d+\.\d+\.\d+\.\d+)[\\/]", RegexOptions.IgnoreCase)]
    private static partial Regex PlatformFromPathRegex();

    // Порт RAS из аргументов ras.exe: «--port=1545» или «--port 1545».
    [GeneratedRegex(@"--port[=\s]+(?<port>\d+)", RegexOptions.IgnoreCase)]
    private static partial Regex PortRegex();

    // Имена всех служб из вывода `sc query state= all`.
    public static IReadOnlyList<string> ParseServiceNames(string queryOutput)
    {
        if (string.IsNullOrEmpty(queryOutput))
        {
            return Array.Empty<string>();
        }

        var names = new List<string>();
        foreach (Match m in ServiceNameRegex().Matches(queryOutput))
        {
            var name = m.Groups["name"].Value.Trim();
            if (name.Length > 0)
            {
                names.Add(name);
            }
        }
        return names;
    }

    // Состояние службы (running) из вывода `sc query <name>`. STATE-код 4 = RUNNING.
    // Любой иной код (1 STOPPED, 2 START_PENDING, 3 STOP_PENDING, …) → не running.
    public static bool ParseIsRunning(string queryOutput)
    {
        var m = StateRegex().Match(queryOutput ?? string.Empty);
        return m.Success && m.Groups["code"].Value == "4";
    }

    // BINARY_PATH_NAME из вывода `sc qc <name>`. null, если поле не найдено.
    public static string? ParseBinaryPath(string qcOutput)
    {
        var m = BinaryPathRegex().Match(qcOutput ?? string.Empty);
        return m.Success ? m.Groups["path"].Value.Trim() : null;
    }

    // Содержит ли binPath ссылку на ras.exe (обнаружение службы по binPath, ADR-47).
    // Имя ras.exe может быть в кавычках/без, с любым регистром.
    public static bool BinPathReferencesRas(string? binPath)
        => !string.IsNullOrEmpty(binPath)
           && binPath.Contains("ras.exe", StringComparison.OrdinalIgnoreCase);

    // Версия платформы из binPath (по сегменту ...\1cv8\<версия>\...). null, если путь
    // нестандартный.
    public static string? ParsePlatformVersion(string? binPath)
    {
        if (string.IsNullOrEmpty(binPath))
        {
            return null;
        }
        var m = PlatformFromPathRegex().Match(binPath);
        return m.Success ? m.Groups["version"].Value : null;
    }

    // Порт RAS из аргументов в binPath. null, если флаг --port отсутствует (тогда служба
    // слушает дефолтный 1545 — это решает уровнем выше при сравнении с endpoint).
    public static string? ParsePort(string? binPath)
    {
        if (string.IsNullOrEmpty(binPath))
        {
            return null;
        }
        var m = PortRegex().Match(binPath);
        return m.Success ? m.Groups["port"].Value : null;
    }
}

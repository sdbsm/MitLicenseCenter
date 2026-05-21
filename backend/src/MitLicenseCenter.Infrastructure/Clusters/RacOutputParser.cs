using System.Text.RegularExpressions;

namespace MitLicenseCenter.Infrastructure.Clusters;

// Pure-static defensive parser для вертикального key-value формата rac.exe.
// Контракт зафиксирован в docs/DECISIONS.md ADR-3.3.
// Формат:
//   field-name                                : value
//   field-name                                : "quoted value"
//   ⏎    (blank line ⇒ запись закончена)
//   field-name                                : <следующая запись>
// Никогда не throws — malformed строки пропускаются с возможным debug-логом.
internal static partial class RacOutputParser
{
    [GeneratedRegex(@"^\s*(?<key>[a-z0-9-]+)\s*:\s?(?<value>.*?)\s*$", RegexOptions.Compiled)]
    private static partial Regex LineRegex();

    // Парсит весь stdout в список словарей (по одному на запись).
    // Пустой вход / только whitespace / только пустые строки → пустой список.
    public static IReadOnlyList<IReadOnlyDictionary<string, string>> Parse(string? stdout)
    {
        if (string.IsNullOrWhiteSpace(stdout))
        {
            return Array.Empty<IReadOnlyDictionary<string, string>>();
        }

        var records = new List<IReadOnlyDictionary<string, string>>();
        var current = new Dictionary<string, string>(StringComparer.Ordinal);

        // Разбиение на строки совместимо с CRLF / LF / mixed.
        var lines = stdout.Split('\n');

        foreach (var rawLine in lines)
        {
            var line = rawLine.TrimEnd('\r');

            // Пустая или whitespace-only строка = разделитель записей.
            if (string.IsNullOrWhiteSpace(line))
            {
                if (current.Count > 0)
                {
                    records.Add(current);
                    current = new Dictionary<string, string>(StringComparer.Ordinal);
                }
                continue;
            }

            var match = LineRegex().Match(line);
            if (!match.Success)
            {
                // Malformed → пропустить (defensive по ADR-3.3).
                continue;
            }

            var key = match.Groups["key"].Value;
            var value = UnquoteIfNeeded(match.Groups["value"].Value);
            current[key] = value;
        }

        // Финальная запись без trailing blank line — типичный случай для последнего блока.
        if (current.Count > 0)
        {
            records.Add(current);
        }

        return records;
    }

    private static string UnquoteIfNeeded(string raw)
    {
        if (raw.Length >= 2 && raw[0] == '"' && raw[^1] == '"')
        {
            return raw.Substring(1, raw.Length - 2);
        }
        return raw;
    }
}

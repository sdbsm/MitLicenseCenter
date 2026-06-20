using System.Text;
using System.Text.Json;
using MitLicenseCenter.Application.TechLog;

namespace MitLicenseCenter.Infrastructure.TechLog;

// Парсер NDJSON-ТЖ 1С 8.5 (ядро MLC-232, этап B) — чистый C# без файловой системы, зеркаль стиля
// LogcfgBuilder (internal sealed). Реализует ЗАКОН по фактам стенда MLC-229 (40_TECHLOG §4/§7):
//   • вход — NDJSON (объект на строку), читаем построчно; BOM в начале строки/файла снимаем;
//   • событие — плоский объект, все значения строки;
//   • ДУБЛИ КЛЮЧЕЙ сохраняем целиком — поэтому разбираем потоковым Utf8JsonReader и складываем ВСЕ
//     пары ключ→значение в список (Dictionary взял бы последнее → потеря данных, §7);
//   • никогда не бросаем: пустая строка → null; невалидный JSON/не объект → null + счётчик пропусков.
internal sealed class TechLogParser : ITechLogParser
{
    // Символ BOM после декодирования UTF-8 в .NET — U+FEFF (escape, чтобы не зависеть от кодировки .cs).
    private const char Bom = '\uFEFF';

    public TechLogEvent? ParseLine(string? line)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            return null;
        }

        // Снять BOM в начале (файл ТЖ 8.5 начинается с \xEF\xBB\xBF; в .NET после декодирования UTF-8
        // это символ U+FEFF). Может стоять только в первой строке файла, но проверяем дёшево всегда.
        var text = line[0] == Bom ? line.Substring(1) : line;
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        try
        {
            var bytes = Encoding.UTF8.GetBytes(text);
            var reader = new Utf8JsonReader(bytes, new JsonReaderOptions
            {
                // Толерантность к «грязным» строкам ТЖ: допускаем хвостовые запятые и комментарии.
                AllowTrailingCommas = true,
                CommentHandling = JsonCommentHandling.Skip,
            });

            return ReadObject(ref reader);
        }
        catch (JsonException)
        {
            // Битая/неполная JSON-строка: пропускаем (never throws, 40_TECHLOG §7).
            return null;
        }
    }

    public TechLogParseResult ParseLines(IEnumerable<string> lines)
    {
        ArgumentNullException.ThrowIfNull(lines);

        var counter = new TechLogParseCounter();
        return new TechLogParseResult(Iterate(lines, counter), counter);
    }

    public TechLogParseResult ParseReader(TextReader reader)
    {
        ArgumentNullException.ThrowIfNull(reader);

        var counter = new TechLogParseCounter();
        return new TechLogParseResult(Iterate(ReadLines(reader), counter), counter);
    }

    // Ленивый разбор: каждая строка → событие либо пропуск со счётчиком (память не растёт).
    private IEnumerable<TechLogEvent> Iterate(IEnumerable<string> lines, TechLogParseCounter counter)
    {
        foreach (var line in lines)
        {
            var ev = ParseLine(line);
            if (ev is null)
            {
                counter.Skipped++;
                continue;
            }

            counter.Processed++;
            yield return ev;
        }
    }

    private static IEnumerable<string> ReadLines(TextReader reader)
    {
        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            yield return line;
        }
    }

    // Потоковый разбор одного плоского JSON-объекта в список ВСЕХ пар ключ→значение (с дублями).
    // Возвращает null, если корень — не объект (например, массив/скаляр): для NDJSON это «не событие».
    private static TechLogEvent? ReadObject(ref Utf8JsonReader reader)
    {
        if (!reader.Read() || reader.TokenType != JsonTokenType.StartObject)
        {
            return null;
        }

        var pairs = new List<KeyValuePair<string, string>>();

        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndObject)
            {
                return new TechLogEvent(pairs);
            }

            if (reader.TokenType != JsonTokenType.PropertyName)
            {
                // Защита от неожиданной структуры — не должно случаться для плоского объекта.
                return null;
            }

            var key = reader.GetString() ?? string.Empty;

            if (!reader.Read())
            {
                return null;
            }

            // Все значения ТЖ 8.5 — строки (40_TECHLOG §4). Толерантно читаем и не-строковые токены
            // (на случай нестандартного источника): нормализуем в строку, ничего не теряя.
            var value = ReadValueAsString(ref reader);
            pairs.Add(new KeyValuePair<string, string>(key, value));
        }

        // Объект не закрылся (обрезанная строка) — толерантно вернуть то, что успели прочитать.
        return new TechLogEvent(pairs);
    }

    private static string ReadValueAsString(ref Utf8JsonReader reader)
    {
        switch (reader.TokenType)
        {
            case JsonTokenType.String:
                return reader.GetString() ?? string.Empty;
            case JsonTokenType.Number:
                // ТЖ 8.5 пишет числа строками (§4); но если источник дал «голое» число — берём его
                // сырое UTF-8 представление (без потери точности), а не парсим в double.
                return RawScalar(ref reader);
            case JsonTokenType.True:
                return "true";
            case JsonTokenType.False:
                return "false";
            case JsonTokenType.Null:
                return string.Empty;
            case JsonTokenType.StartObject:
            case JsonTokenType.StartArray:
                // Вложенный объект/массив у плоского ТЖ-события не ожидается (§4): пропускаем контейнер
                // целиком (чтобы цикл не сбился), значение трактуем как пустое.
                reader.Skip();
                return string.Empty;
            default:
                return string.Empty;
        }
    }

    // Сырое скалярное значение токена как строка (из UTF-8 байт исходного буфера/последовательности).
    private static string RawScalar(ref Utf8JsonReader reader)
    {
        var bytes = reader.HasValueSequence
            ? System.Buffers.BuffersExtensions.ToArray(reader.ValueSequence)
            : reader.ValueSpan.ToArray();
        return Encoding.UTF8.GetString(bytes);
    }
}

using System.Globalization;

namespace MitLicenseCenter.Application.TechLog;

// Распарсенное событие технологического журнала 1С 8.5 (JSON-режим). Модель плоского события по
// фактам стенда MLC-229 (40_TECHLOG §4/§7), снятым с живой 8.5.1.1302:
//   • событие ТЖ — ПЛОСКИЙ JSON-объект: базовые поля (ts/duration/name/depth/level/process) и далее
//     свойства события как обычные ключи верхнего уровня (p:processName, OSThread, t:clientID, Sql,
//     Context, Descr, Exception, …);
//   • ВСЕ значения — строки (даже числа: "duration":"1", "Rows":"1") — храним как строки;
//   • ключи МОГУТ ДУБЛИРОВАТЬСЯ (40_TECHLOG §7): t:clientID повторяется; у SDBL-завершения транзакции
//     Func встречается ДВАЖДЫ; p:processName при динамическом обновлении пишется дважды. Поэтому пары
//     ключ→значение хранятся СПИСКОМ (порядок исходный), а НЕ Dictionary вслепую — иначе теряем дубли;
//   • любое поле может отсутствовать («поля-призраки», §7) — аксессоры возвращают null/пусто, не бросают.
// Модель неизменяема и не зависит от файловой системы — её строит ITechLogParser из NDJSON-строки.
public sealed class TechLogEvent
{
    // Все пары ключ→значение события в исходном порядке, включая дубли ключей (40_TECHLOG §4/§7).
    // Значения — всегда строки (как на проводе). Это «сырое» представление; типизированные аксессоры
    // и нормализованная длительность вычисляются поверх него.
    public IReadOnlyList<KeyValuePair<string, string>> Properties { get; }

    public TechLogEvent(IReadOnlyList<KeyValuePair<string, string>> properties)
    {
        Properties = properties ?? throw new ArgumentNullException(nameof(properties));
        RawDuration = First("duration");
        (DurationMicroseconds, DurationSeconds) = NormalizeDuration(RawDuration);
    }

    // Базовые поля события (40_TECHLOG §4). Хранятся строками, как пришли. null = поля нет в строке.
    public string? Ts => First("ts");
    public string? Name => First("name");
    public string? Depth => First("depth");
    public string? Level => First("level");
    public string? Process => First("process");

    // Сырое строковое значение длительности (микросекунды, как на проводе: "60005971"). null/пусто —
    // длительности нет. Нормализованные поля ниже — вычислены из него.
    public string? RawDuration { get; }

    // Длительность в МИКРОСЕКУНДАХ (единица ТЖ 8.5 — факт стенда MLC-229, 40_TECHLOG §4). null, если
    // duration отсутствует/пуст/нечисловой (толерантность — не бросаем).
    public long? DurationMicroseconds { get; }

    // Длительность в секундах = микросекунды / 1e6 (60005971 µs → 60.006 с). null при той же
    // толерантности, что и DurationMicroseconds.
    public double? DurationSeconds { get; }

    // Первое значение по ключу (или null, если ключа нет). Аксессор «поля-призрака»: не бросает.
    public string? First(string key)
    {
        foreach (var pair in Properties)
        {
            if (string.Equals(pair.Key, key, StringComparison.Ordinal))
            {
                return pair.Value;
            }
        }

        return null;
    }

    // Последнее значение по ключу (или null). Нужно для дублей, где значимо именно последнее вхождение
    // (например, второе значение Func у завершения транзакции SDBL, 40_TECHLOG §7).
    public string? Last(string key)
    {
        string? value = null;
        foreach (var pair in Properties)
        {
            if (string.Equals(pair.Key, key, StringComparison.Ordinal))
            {
                value = pair.Value;
            }
        }

        return value;
    }

    // ВСЕ значения по ключу в исходном порядке (пусто, если ключа нет). Сохраняет дубли целиком —
    // ничего не теряется (40_TECHLOG §7: t:clientID дважды, Func дважды, p:processName дважды).
    public IReadOnlyList<string> All(string key)
    {
        List<string>? values = null;
        foreach (var pair in Properties)
        {
            if (string.Equals(pair.Key, key, StringComparison.Ordinal))
            {
                (values ??= new List<string>()).Add(pair.Value);
            }
        }

        return (IReadOnlyList<string>?)values ?? Array.Empty<string>();
    }

    // Есть ли хотя бы одно вхождение ключа.
    public bool Has(string key) => First(key) is not null;

    // Нормализация duration: строка µs → (микросекунды, секунды). Толерантность (40_TECHLOG §4):
    // отсутствует/пусто/нечисловое → (null, null), не бросаем.
    private static (long?, double?) NormalizeDuration(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)
            || !long.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var micros))
        {
            return (null, null);
        }

        return (micros, micros / 1_000_000d);
    }
}

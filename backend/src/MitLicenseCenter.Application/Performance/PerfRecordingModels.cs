using System.Text.Json;

namespace MitLicenseCenter.Application.Performance;

// Жизненный цикл записи (MLC-070, ADR-26). На проводе — строкой (JsonStringEnumConverter, Program.cs);
// в БД хранится int'ом (HasConversion<int>). Это первичное определение — int-значения не
// переиспользовать (та же дисциплина, что у замороженных enum'ов аудита).
public enum PerfRecordingStatus
{
    Active,
    Stopped,
    Interrupted,
}

// Причина остановки записи. Заполнена только для Stopped: Manual — оператор нажал «остановить»;
// TimeLimit/SampleLimit — авто-стоп по настройкам. Для Active — null; для Interrupted — тоже null
// (запись не «остановлена по причине», а оборвана рестартом процесса, это несёт сам Status).
public enum PerfRecordingStopReason
{
    Manual,
    TimeLimit,
    SampleLimit,
}

// Единые опции (де)сериализации JSON-колонок сэмпла (семьи процессов + сериализованные топ-виновники
// 1С/SQL). Сервис (Infrastructure) сериализует при записи сэмпла, эндпоинт просмотра (Web)
// десериализует при чтении — один источник опций, чтобы формат в БД и на проводе не разъезжался.
// Web-дефолты = camelCase + регистронезависимое чтение.
public static class PerfSampleJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);
}

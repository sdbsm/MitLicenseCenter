using System.Text.Json;

namespace MitLicenseCenter.Application.Performance;

// Жизненный цикл записи (MLC-070, ADR-26). На проводе — строкой (JsonStringEnumConverter, Program.cs);
// в БД хранится int'ом (HasConversion<int>). ЗАМОРОЖЕНЫ — контракт с БД HasConversion<int>:
// int-значения не переиспользовать и не переназначать (та же дисциплина, что у AuditActionType
// и BackupStatus). Новые члены добавляются только в конец с явным числом.
public enum PerfRecordingStatus
{
    Active = 0,
    Stopped = 1,
    Interrupted = 2,
}

// Причина остановки записи. Заполнена только для Stopped: Manual — оператор нажал «остановить»;
// TimeLimit/SampleLimit — авто-стоп по настройкам. Для Active — null; для Interrupted — тоже null
// (запись не «остановлена по причине», а оборвана рестартом процесса, это несёт сам Status).
// ЗАМОРОЖЕНЫ — контракт с БД HasConversion<int>: int-значения не переиспользовать и не переназначать.
// Новые члены добавляются только в конец с явным числом.
public enum PerfRecordingStopReason
{
    Manual = 0,
    TimeLimit = 1,
    SampleLimit = 2,
}

// Единые опции (де)сериализации JSON-колонок сэмпла (семьи процессов + сериализованные топ-виновники
// 1С/SQL). Сервис (Infrastructure) сериализует при записи сэмпла, эндпоинт просмотра (Web)
// десериализует при чтении — один источник опций, чтобы формат в БД и на проводе не разъезжался.
// Web-дефолты = camelCase + регистронезависимое чтение.
public static class PerfSampleJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);
}

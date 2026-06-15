using Hangfire;

namespace MitLicenseCenter.Application.Jobs;

// Hangfire-job (MLC-169): ежедневная очистка записей «Быстродействия» (dbo.PerfRecordings
// + каскадные dbo.PerfRecordingSamples) старше фиксированного срога хранения. В отличие от
// license-usage/audit/backup-retention окно НЕ настраивается оператором — это отладочные
// артефакты «по требованию», а не телеметрия с операционным смыслом срока; срок зашит
// константой в реализации (прецедент JobRetentionStateFilter — knob в коде, не Setting, без
// раздувания каталога настроек и миграций). Запускается в 03:45 UTC (смещён от audit 03:00 /
// backup 03:15 / license-usage 03:30); CRON фиксирован в Program.cs. Без аудит-записи — это
// housekeeping (хватит server-лога; запись в аудит жгла бы новый замороженный enum-номер).
public interface IPerfRecordingRetentionJob
{
    // Идемпотентная ночная housekeeping-джоба (как license-usage/backup-retention): 3 попытки
    // на транзиентные блипы, затем Fail; следующий суточный запуск самоисцеляется. Атрибут на
    // методе интерфейса (RecurringJob.AddOrUpdate<IPerfRecordingRetentionJob>).
    [AutomaticRetry(Attempts = 3, OnAttemptsExceeded = AttemptsExceededAction.Fail)]
    Task RunAsync(CancellationToken ct);
}

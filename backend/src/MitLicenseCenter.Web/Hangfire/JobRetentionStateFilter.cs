using Hangfire.States;
using Hangfire.Storage;

namespace MitLicenseCenter.Web.Hangfire;

// Hangfire-история — единственная схема в БД, растущая от рекуррентных джоб
// (самая частая — publication-status-refresh каждые 5 мин → ~288 завершённых джоб/сутки).
// MLC-154: cold-snapshot больше не Hangfire-recurring (ушёл в ColdTierPollingService). Hangfire
// истекает завершённые джобы по ExpireAt, но дефолт (1 день) задаётся неявно в
// библиотеке. Делаем срок детерминированным и документированным: succeeded/deleted
// джобы живут 2 дня (пара дней истории в дэшборде, схема ограничена ~3k строк).
// Failed-состоянию (MLC-123, REL-22) даём ОТДЕЛЬНОЕ, более ДОЛГОЕ окно — 30 дней:
// упавшие джобы должны оставаться видимыми для разбора (раньше они не истекали вовсе),
// но не копиться вечно — после 30 дней самоочищаются. Это внутренний knob, оператору
// не tuneable (как и фиксированный CRON у audit-retention), поэтому константа в коде,
// а не Setting — без раздувания каталога настроек и миграций.
public sealed class JobRetentionStateFilter : IApplyStateFilter
{
    private static readonly TimeSpan FinishedJobRetention = TimeSpan.FromDays(2);
    private static readonly TimeSpan FailedJobRetention = TimeSpan.FromDays(30);

    public void OnStateApplied(ApplyStateContext context, IWriteOnlyTransaction transaction)
    {
        if (context.NewState is SucceededState or DeletedState)
        {
            context.JobExpirationTimeout = FinishedJobRetention;
        }
        else if (context.NewState is FailedState)
        {
            // Раньше Failed не истекал (видимость для разбора), но без потолка схема
            // копила бы упавшие джобы бесконечно. 30 дней — видимость + самоочистка.
            context.JobExpirationTimeout = FailedJobRetention;
        }
    }

    public void OnStateUnapplied(ApplyStateContext context, IWriteOnlyTransaction transaction)
    {
        // Откат состояния не меняет политику хранения — её задаёт только применённое
        // финальное состояние в OnStateApplied.
    }
}

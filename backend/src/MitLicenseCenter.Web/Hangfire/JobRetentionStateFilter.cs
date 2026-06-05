using Hangfire.States;
using Hangfire.Storage;

namespace MitLicenseCenter.Web.Hangfire;

// Hangfire-история — единственная схема в БД, растущая от высокочастотной джобы
// (cold-snapshot тикает раз в минуту → ~1440 завершённых джоб/сутки). Hangfire
// истекает завершённые джобы по ExpireAt, но дефолт (1 день) задаётся неявно в
// библиотеке. Делаем срок детерминированным и документированным: succeeded/deleted
// джобы живут 2 дня (пара дней истории в дэшборде, схема ограничена ~3k строк).
// Failed-состояние НЕ трогаем — оно по дефолту не истекает, чтобы упавшие джобы
// оставались видимыми для разбора. Это внутренний knob, оператору не tuneable
// (как и фиксированный CRON у audit-retention), поэтому константа в коде, а не
// Setting — без раздувания каталога настроек и миграций.
public sealed class JobRetentionStateFilter : IApplyStateFilter
{
    private static readonly TimeSpan FinishedJobRetention = TimeSpan.FromDays(2);

    public void OnStateApplied(ApplyStateContext context, IWriteOnlyTransaction transaction)
    {
        if (context.NewState is SucceededState or DeletedState)
        {
            context.JobExpirationTimeout = FinishedJobRetention;
        }
    }

    public void OnStateUnapplied(ApplyStateContext context, IWriteOnlyTransaction transaction)
    {
        // Откат состояния не меняет политику хранения — её задаёт только применённое
        // финальное состояние в OnStateApplied.
    }
}

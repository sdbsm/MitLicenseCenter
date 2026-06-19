using System.Globalization;
using Hangfire;
using MitLicenseCenter.Application.Jobs;

namespace MitLicenseCenter.Web.Hangfire;

// MLC-218 (ADR-55): регистрация/снятие рекуррентной джобы авто-рестарта сервера 1С из
// настройки расписания. В отличие от шести фиксированных ночных джоб (NightlyJobSchedule),
// у этой cron собирается из OneC.AutoRestart.Time (ежедневно в HH:mm) и пересобирается при
// каждом изменении настройки — поэтому регистрация вынесена в переиспользуемый хелпер,
// вызываемый и на старте (Program.cs, по текущей настройке), и из эндпоинта set-расписания.
//
// Включено → RecurringJob.AddOrUpdate с TimeZone = TimeZoneInfo.Local (как ночные джобы,
// ADR-52: «04:00» — по часам хоста, не UTC). Выключено → RecurringJob.RemoveIfExists
// (задание снимается, тик не идёт — НЕ тик-каждые-5-минут). Идемпотентно.
public static class OneCAutoRestartScheduler
{
    public const string RecurringJobId = "onec-auto-restart";

    // Применить расписание: enabled → зарегистрировать ежедневный cron из time (HH:mm) в
    // местном поясе; иначе снять задание. time валидируется вызывающим (эндпоинт);
    // BuildCron на всякий случай отбивает мусор → снятие (безопасный no-op вместо падения).
    public static void Apply(bool enabled, string? time)
    {
        if (enabled && TryBuildCron(time, out var cron))
        {
            RecurringJob.AddOrUpdate<IOneCAutoRestartJob>(
                RecurringJobId,
                j => j.RunAsync(CancellationToken.None),
                cron,
                NightlyJobSchedule.LocalTimeZoneOptions);
        }
        else
        {
            RecurringJob.RemoveIfExists(RecurringJobId);
        }
    }

    // "HH:mm" → дневной cron "m H * * *". true + cron при валидном времени; false при null/мусоре.
    public static bool TryBuildCron(string? time, out string cron)
    {
        cron = string.Empty;
        if (string.IsNullOrWhiteSpace(time))
        {
            return false;
        }

        var parts = time.Split(':');
        if (parts.Length != 2
            || !int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out var hour)
            || !int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var minute)
            || hour is < 0 or > 23
            || minute is < 0 or > 59)
        {
            return false;
        }

        cron = string.Format(CultureInfo.InvariantCulture, "{0} {1} * * *", minute, hour);
        return true;
    }
}

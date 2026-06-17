using Hangfire;

namespace MitLicenseCenter.Web.Hangfire;

// MLC-191 (ADR-52): ночные рекуррентные housekeeping-джобы планируются по МЕСТНОМУ
// часовому поясу хоста, а не по UTC (дефолт Hangfire). Cron-выражения у этих джоб
// («ночные» 02:00…04:00) задумывались как ночное окно по часам сервера; без явного
// TimeZone Hangfire считал их в UTC, и на хосте UTC+3 они уезжали в утро (05:00…07:00 МСК).
// Single-host (ADR-28) → TimeZoneInfo.Local = пояс оператора; совпадает с поведением
// прочих планировщиков на том же Windows-сервере (Task Scheduler, SQL Agent).
//
// ВАЖНО: это влияет ТОЛЬКО на момент срабатывания планировщика. Хранение/транспорт
// времени остаются UTC (ADR-23 в силе) — TimeZone здесь не трогает метки данных.
//
// Единый общий объект на все шесть суточных джоб (без дубля new RecurringJobOptions
// на каждой регистрации). publication-status-refresh (каждые 5 мин) к поясу
// нечувствителен и эти опции не использует.
public static class NightlyJobSchedule
{
    // Опции для всех суточных ночных джоб: cron считается в местном поясе хоста.
    public static readonly RecurringJobOptions LocalTimeZoneOptions = new()
    {
        TimeZone = TimeZoneInfo.Local,
    };
}

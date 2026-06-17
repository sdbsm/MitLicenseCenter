using System;
using FluentAssertions;
using MitLicenseCenter.Web.Hangfire;
using Xunit;

namespace MitLicenseCenter.Tests.Unit.Jobs;

// MLC-191 (ADR-52): инвариант — ночные суточные housekeeping-джобы планируются по МЕСТНОМУ
// поясу хоста, а не по UTC (дефолт Hangfire). Сама регистрация Hangfire идёт против реального
// storage и отключена в env "Test" (см. Program.cs, !IsEnvironment("Test")), поэтому
// напрямую её юнит-тестировать нельзя. Выбор пояса вынесен в общий объект
// NightlyJobSchedule.LocalTimeZoneOptions, которым накрыты все шесть суточных джоб; здесь
// закрепляем, что этот объект задаёт именно Local, а не Utc.
//
// Guard, а не тавтология: если кто-то вернёт регистрации к дефолтному UTC (уберёт TimeZone
// или поставит TimeZoneInfo.Utc), «ночные» джобы снова уедут в утро по местному времени на
// хосте UTC+3 — этот тест покраснеет и не даст молча откатить решение ADR-52.
public sealed class NightlyJobScheduleTests
{
    [Fact]
    public void Nightly_jobs_are_scheduled_in_host_local_time_zone()
    {
        NightlyJobSchedule.LocalTimeZoneOptions.TimeZone
            .Should().Be(TimeZoneInfo.Local,
                "ночные housekeeping-джобы должны срабатывать ночью по часам хоста (ADR-52, single-host)");
    }

    [Fact]
    public void Nightly_jobs_do_not_silently_fall_back_to_hangfire_utc_default()
    {
        // Негативный guard против дефолта Hangfire (RecurringJobOptions.TimeZone == Utc).
        // Пропускаем там, где пояс хоста САМ равен UTC (тогда Local == Utc легитимно и
        // отличить «осознанный Local» от «забытого дефолта» нельзя) — на таком хосте
        // ночное окно и так совпадает с UTC, расхождения нет. На хостах со смещением
        // (стенд UTC+3) этот guard ловит откат к UTC.
        if (TimeZoneInfo.Local.BaseUtcOffset == TimeSpan.Zero)
        {
            return;
        }

        NightlyJobSchedule.LocalTimeZoneOptions.TimeZone
            .Should().NotBe(TimeZoneInfo.Utc,
                "UTC — дефолт Hangfire, от которого осознанно ушли в MLC-191; на хосте со смещением это уводит ночные джобы в утро");
    }
}

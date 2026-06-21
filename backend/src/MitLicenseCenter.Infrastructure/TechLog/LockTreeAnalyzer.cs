using System.Text.RegularExpressions;
using MitLicenseCenter.Application.TechLog;

namespace MitLicenseCenter.Infrastructure.TechLog;

// Анализатор управляемых блокировок 1С уровня платформы (MLC-233, этап B).
// Реализует ILockTreeAnalyzer: потребляет IEnumerable<TechLogEvent>, строит LockAnalysisResult.
// internal sealed — зеркаль TechLogParser (тот же слой и стиль).
//
// ГРАНИЦА (40_TECHLOG §5): ТОЛЬКО события TLOCK/TTIMEOUT/TDEADLOCK (менеджер блокировок
// платформы 1С). Блокировки уровня СУБД (<dbmslocks/>/lkX) — НЕ здесь (MLC-236).
//
// Устойчивость (40_TECHLOG §7, принцип never-throws):
//   • «поля-призраки»: WaitConnections пусто/нет, escalating у TLOCK только при =true и т.д.;
//   • варианты имён пользователя: Usr ИЛИ UserName (40_TECHLOG §7);
//   • p:processName нормализуется к базовому имени ИБ (отсекаем суффикс-GUID фоновых сессий);
//   • дубли ключей берём через First()/Last() TechLogEvent — не падаем.
//   • любое нераспознанное событие — пропускаем, не бросаем.
internal sealed partial class LockTreeAnalyzer : ILockTreeAnalyzer
{
    // Суффикс-GUID фоновых/динамических сессий: «<имя ИБ>_<8-4-4-4-12>».
    // Снято со стенда 8.5 (40_TECHLOG §8): у фоновых заданий p:processName = «ut11_saratov_296ee6d8-…».
    [GeneratedRegex(@"_[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$",
        RegexOptions.CultureInvariant)]
    private static partial Regex GuidSuffixRegex();

    public LockAnalysisResult Analyze(IEnumerable<TechLogEvent> events)
    {
        ArgumentNullException.ThrowIfNull(events);

        var waitEdges = new List<LockWaitEdge>();
        var timeouts = new List<LockTimeoutEntry>();
        var deadlocks = new List<LockDeadlockEntry>();
        var tlockProcessed = 0;
        var skipped = 0;

        foreach (var ev in events)
        {
            // Не-блокировочные события молча игнорируем (DBMSSQL, SDBL, EXCP и др. — не наши).
            var name = ev.Name;
            if (name is null)
            {
                skipped++;
                continue;
            }

            try
            {
                switch (name)
                {
                    case "TLOCK":
                        ProcessTlock(ev, waitEdges, ref tlockProcessed);
                        break;

                    case "TTIMEOUT":
                        ProcessTtimeout(ev, timeouts);
                        break;

                    case "TDEADLOCK":
                        ProcessTdeadlock(ev, deadlocks);
                        break;

                    // Все остальные события (DBMSSQL, SDBL, EXCP, CALL…) — не наши, игнорируем.
                    default:
                        break;
                }
            }
            catch
            {
                // never-throws: любое непредвиденное исключение при разборе одного события
                // не ломает весь анализ — просто пропускаем (40_TECHLOG §7).
                skipped++;
            }
        }

        return new LockAnalysisResult
        {
            WaitEdges = waitEdges,
            Timeouts = timeouts,
            Deadlocks = deadlocks,
            TlockEventsProcessed = tlockProcessed,
            SkippedEvents = skipped,
        };
    }

    // Обработка TLOCK: событие установки управляемой блокировки.
    // Ребро ожидания строится ТОЛЬКО если WaitConnections непустой (есть кого ждать).
    // Поля: SessionID, AppID, Usr/UserName, DBMS, DataBase, Regions, Locks, WaitConnections,
    //        Context, p:processName, duration (40_TECHLOG §8).
    private static void ProcessTlock(
        TechLogEvent ev,
        List<LockWaitEdge> waitEdges,
        ref int tlockProcessed)
    {
        tlockProcessed++;

        // WaitConnections: пусто = блокировка взята без ожидания → ребро не строим (нет «кого ждёт»).
        var waitConnections = ev.First("WaitConnections");
        if (string.IsNullOrEmpty(waitConnections))
        {
            return;
        }

        var rawProcessName = ev.First("p:processName");
        waitEdges.Add(new LockWaitEdge
        {
            Ts = ev.Ts,
            WaitingSessionId = ev.First("SessionID"),
            WaitingUser = ev.First("Usr") ?? ev.First("UserName"),   // вариант имени §7
            WaitingAppId = ev.First("AppID"),
            BlockingConnections = waitConnections,
            Regions = ev.First("Regions"),
            LockMode = ev.First("Locks"),
            WaitDurationSeconds = ev.DurationSeconds,
            InfobaseName = NormalizeProcessName(rawProcessName),
            RawProcessName = rawProcessName,
            Context = ev.First("Context"),
            Database = ev.First("DataBase"),
        });
    }

    // Обработка TTIMEOUT: таймаут ожидания управляемой блокировки.
    // «Lock request timeout» (40_TECHLOG §5). Точная структура полей подлежит подтверждению
    // на стенде 8.5 — собрано по семантике §5 и общей структуре событий ТЖ.
    private static void ProcessTtimeout(TechLogEvent ev, List<LockTimeoutEntry> timeouts)
    {
        var rawProcessName = ev.First("p:processName");
        timeouts.Add(new LockTimeoutEntry
        {
            Ts = ev.Ts,
            SessionId = ev.First("SessionID"),
            User = ev.First("Usr") ?? ev.First("UserName"),
            Regions = ev.First("Regions"),
            LockMode = ev.First("Locks"),
            WaitDurationSeconds = ev.DurationSeconds,
            InfobaseName = NormalizeProcessName(rawProcessName),
            RawProcessName = rawProcessName,
            Context = ev.First("Context"),
            WaitConnections = ev.First("WaitConnections"),
        });
    }

    // Обработка TDEADLOCK: взаимоблокировка управляемых блокировок.
    // Участники + ресурсы (40_TECHLOG §5). Точная структура полей подлежит подтверждению
    // на стенде 8.5 — собрано по семантике §5 и общей структуре событий ТЖ.
    private static void ProcessTdeadlock(TechLogEvent ev, List<LockDeadlockEntry> deadlocks)
    {
        var rawProcessName = ev.First("p:processName");
        deadlocks.Add(new LockDeadlockEntry
        {
            Ts = ev.Ts,
            SessionId = ev.First("SessionID"),
            User = ev.First("Usr") ?? ev.First("UserName"),
            Regions = ev.First("Regions"),
            LockMode = ev.First("Locks"),
            DurationSeconds = ev.DurationSeconds,
            InfobaseName = NormalizeProcessName(rawProcessName),
            RawProcessName = rawProcessName,
            Context = ev.First("Context"),
            WaitConnections = ev.First("WaitConnections"),
        });
    }

    // Нормализация p:processName к базовому имени ИБ: отсекаем суффикс-GUID фоновых сессий.
    // 40_TECHLOG §8: у фоновых/динамических сессий p:processName = «<имя ИБ>_<GUID>».
    // Основная работа — голое имя («mitpro», «ut11_saratov»); фоновые задания добавляют GUID-суффикс.
    // Сырое значение сохраняется в RawProcessName для диагностики.
    internal static string? NormalizeProcessName(string? raw)
    {
        if (string.IsNullOrEmpty(raw))
        {
            return raw;
        }

        var m = GuidSuffixRegex().Match(raw);
        return m.Success ? raw[..m.Index] : raw;
    }
}

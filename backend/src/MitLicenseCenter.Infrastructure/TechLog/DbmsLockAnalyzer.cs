using MitLicenseCenter.Application.TechLog;

namespace MitLicenseCenter.Infrastructure.TechLog;

// Анализатор СУБД-блокировок (MLC-236, этап B трека «Расследование производительности»).
// Реализует IDbmsLockAnalyzer: потребляет IEnumerable<TechLogEvent>, строит DbmsLockAnalysisResult.
// internal sealed — зеркаль LockTreeAnalyzer/ExceptionAnalyzer (тот же слой и стиль).
//
// ГРАНИЦА (40_TECHLOG §5): ТОЛЬКО события DBMSSQL с полями lkX (СУБД-уровень).
// Управляемые блокировки (TLOCK/TTIMEOUT/TDEADLOCK) — ILockTreeAnalyzer (MLC-233).
// Не путать механизмы, не обещать единое дерево всех блокировок (ложная полнота).
//
// Алгоритм дерева (40_TECHLOG §5, infostart 1431026):
//   1. Проход по всем событиям: индексировать источники (lka=1) по connectID;
//      жертвы (lkp=1) накапливать в список.
//   2. По каждой жертве: жертва.lksrc → источник.connectID → ребро.
//   3. Источник не найден в окне → ребро с SourceMatched=false (частичное).
//
// ⚠ Структура полей lkX собрана по документации (infostart 1431026, 40_TECHLOG §5).
//   Точная форма в JSON-ТЖ 8.5 подлежит подтверждению на стенде (приёмка владельца).
//
// Устойчивость (40_TECHLOG §7, принцип never-throws):
//   • «поля-призраки»: lkX появляются только при блокировке; lka/lkp только при =1 —
//     без них событие игнорируется (не наше);
//   • варианты имён connectID: «t:connectID» (факт стенда 40_TECHLOG §4) и «connectID»
//     как альтернатива — берём первое непустое;
//   • дубли ключей — через First() TechLogEvent, не падаем;
//   • любое нераспознанное/неполное событие — пропускаем, не бросаем.
internal sealed class DbmsLockAnalyzer : IDbmsLockAnalyzer
{
    public DbmsLockAnalysisResult Analyze(IEnumerable<TechLogEvent> events)
    {
        ArgumentNullException.ThrowIfNull(events);

        // Первый проход: собрать все события, разложить по lkX-ролям.
        // Источники индексируем по connectID → быстрый поиск при построении рёбер.
        // Ключ: нормализованный connectID (trimmed, т.к. вариации пробелов возможны).
        var sources = new Dictionary<string, TechLogEvent>(StringComparer.Ordinal);
        var victims = new List<TechLogEvent>();
        var lkEventsProcessed = 0;
        var skipped = 0;

        foreach (var ev in events)
        {
            // Только DBMSSQL — всё остальное игнорируем (не наш анализатор).
            if (!string.Equals(ev.Name, "DBMSSQL", StringComparison.Ordinal))
            {
                continue;
            }

            try
            {
                var lka = ev.First("lka");
                var lkp = ev.First("lkp");

                // DBMSSQL без lkX — обычный запрос к СУБД, не блокировка; молча игнорируем.
                if (string.IsNullOrEmpty(lka) && string.IsNullOrEmpty(lkp))
                {
                    continue;
                }

                lkEventsProcessed++;

                // Источник блокировки (lka=1): индексируем по connectID для поиска жертвами.
                // Вариант имени (40_TECHLOG §4): «t:connectID» на стенде 8.5; «connectID» —
                // альтернатива (§7 «варианты имён»). Берём первое непустое.
                if (string.Equals(lka, "1", StringComparison.Ordinal))
                {
                    var connectId = ResolveConnectId(ev);
                    if (!string.IsNullOrEmpty(connectId))
                    {
                        // При дублях connectID — последний выигрывает (более поздний источник
                        // в окне актуальнее старого). Устойчивость: не бросаем.
                        sources[connectId] = ev;
                    }
                }

                // Жертва блокировки (lkp=1): накапливаем для построения рёбер.
                if (string.Equals(lkp, "1", StringComparison.Ordinal))
                {
                    victims.Add(ev);
                }
            }
            catch
            {
                // never-throws: любое непредвиденное исключение при разборе одного события
                // не ломает весь анализ — просто пропускаем (40_TECHLOG §7).
                skipped++;
            }
        }

        // Второй проход: построение рёбер «жертва → источник».
        var waitEdges = new List<DbmsLockWaitEdge>(victims.Count);
        var unmatchedVictimCount = 0;

        foreach (var victim in victims)
        {
            try
            {
                var lksrc = victim.First("lksrc");
                var rawProcessName = victim.First("p:processName");

                // Поиск источника по lksrc → connectID источника (40_TECHLOG §5).
                TechLogEvent? source = null;
                var sourceMatched = false;
                if (!string.IsNullOrEmpty(lksrc)
                    && sources.TryGetValue(lksrc, out source))
                {
                    sourceMatched = true;
                }
                else
                {
                    unmatchedVictimCount++;
                }

                waitEdges.Add(new DbmsLockWaitEdge
                {
                    VictimTs = victim.Ts,
                    VictimConnectId = ResolveConnectId(victim),
                    VictimLksrc = lksrc,
                    VictimLkpto = victim.First("lkpto"),
                    VictimSql = victim.First("Sql"),
                    VictimContext = victim.First("Context"),
                    VictimLkpid = victim.First("lkpid"),
                    SourceConnectId = source is not null ? ResolveConnectId(source) : null,
                    SourceLkato = source?.First("lkato"),
                    SourceLkaid = source?.First("lkaid"),
                    SourceSql = source?.First("Sql"),
                    SourceContext = source?.First("Context"),
                    InfobaseName = TechLogProcessName.Normalize(rawProcessName),
                    RawProcessName = rawProcessName,
                    Database = victim.First("DataBase"),
                    SourceMatched = sourceMatched,
                });
            }
            catch
            {
                // never-throws: пропускаем, не бросаем (40_TECHLOG §7).
                skipped++;
            }
        }

        return new DbmsLockAnalysisResult
        {
            WaitEdges = waitEdges,
            LkEventsProcessed = lkEventsProcessed,
            UnmatchedVictimCount = unmatchedVictimCount,
            SkippedEvents = skipped,
        };
    }

    // Нормализует connectID из события ТЖ: берём «t:connectID» (факт стенда 8.5, 40_TECHLOG §4),
    // fallback — «connectID» (вариант имени по §7 «варианты имён», устойчивость).
    // Trim: защита от случайных пробелов (40_TECHLOG §7).
    private static string? ResolveConnectId(TechLogEvent ev)
    {
        var v = ev.First("t:connectID") ?? ev.First("connectID");
        return string.IsNullOrWhiteSpace(v) ? null : v.Trim();
    }
}

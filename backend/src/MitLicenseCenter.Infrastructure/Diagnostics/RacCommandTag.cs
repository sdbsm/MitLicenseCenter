namespace MitLicenseCenter.Infrastructure.Diagnostics;

// MLC-037 (PERF-01): дешёвый детерминированный тег команды rac.exe, выведенный из её
// аргументов, для группировки спавн-метрик. Возвращает интернированные константы — на
// горячем пути ни одной аллокации. Структура args: [endpoint?] <verb> <subverb> [--options].
// Endpoint-токен (host:port) и опции (--cluster=…) не входят в множество глаголов и
// пропускаются естественно. Сканируется список ≤6 элементов — стоимость пренебрежима, а
// вызывается только под гардом RacMetrics.Enabled (при активном слушателе).
internal static class RacCommandTag
{
    public const string ClusterList = "cluster.list";
    public const string SessionList = "session.list";
    public const string SessionTerminate = "session.terminate";
    public const string InfobaseSummaryList = "infobase.summary.list";
    public const string Other = "other";

    public static string For(IReadOnlyList<string> args)
    {
        for (var i = 0; i < args.Count; i++)
        {
            switch (args[i])
            {
                case "cluster":
                    return ClusterList;
                case "session":
                    return i + 1 < args.Count && args[i + 1] == "terminate"
                        ? SessionTerminate
                        : SessionList;
                case "infobase":
                    return InfobaseSummaryList;
            }
        }

        return Other;
    }
}

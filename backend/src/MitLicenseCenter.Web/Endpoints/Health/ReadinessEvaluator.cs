namespace MitLicenseCenter.Web.Endpoints;

// MLC-040 (PERF-04): чистая агрегация суб-статусов проб в overall-статус + HTTP-код.
// Вынесено из хендлера, чтобы вся матрица решений покрывалась юнит-тестами без реально
// лежащих зависимостей. Контракт (решения по задаче):
//   • БД — единственная зависимость, гейтящая not_ready/503 (CanConnect=false).
//   • RAS-«Сбой» (degraded) и Hangfire-down — мягкие сигналы: overall=degraded, но HTTP 200
//     (single-node: снимать узел из-за RAS бессмысленно — это уронит и сам Dashboard).
//   • RAS-«unknown» (первые 30с, ещё не пробовали) не понижает overall до degraded.
internal static class ReadinessEvaluator
{
    public static (string Overall, int HttpStatus) Evaluate(string database, string ras, string hangfire)
    {
        if (database == ReadinessStatus.Down)
        {
            return (ReadinessStatus.NotReady, StatusCodes.Status503ServiceUnavailable);
        }

        var degraded = ras == ReadinessStatus.Degraded || hangfire == ReadinessStatus.Down;
        return degraded
            ? (ReadinessStatus.Degraded, StatusCodes.Status200OK)
            : (ReadinessStatus.Ready, StatusCodes.Status200OK);
    }
}

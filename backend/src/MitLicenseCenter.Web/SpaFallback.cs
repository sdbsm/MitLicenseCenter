namespace MitLicenseCenter.Web;

// MLC-098 (ADR-30) — чистое решение «этот путь SPA-fallback НЕ перехватывает».
// Вынесено из Program.cs, чтобы таблицу путей проверить юнит-тестом без загрузки хоста
// (полный boot тянет SQL — зеркало TransportSecurity.cs).
internal static class SpaFallback
{
    // /api (REST + /api/docs Swagger) и /hangfire — отдают свой 404/контракт, НЕ index.html,
    // иначе SPA замаскировал бы реальный API. StartsWithSegments сегмент-aware:
    // «/api» матчит /api и /api/v1/x, но НЕ /applications.
    public static bool IsReservedPath(PathString path)
        => path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase)
        || path.StartsWithSegments("/hangfire", StringComparison.OrdinalIgnoreCase);
}

namespace MitLicenseCenter.Web.Endpoints;

// MLC-040 (PERF-04): readiness-контракт. Тело анонимного ответа санитизировано —
// только грубые суб-статусы, без путей/имён серверов/текстов исключений
// (ADR-4.1 / MLC-009; полные детали сбоя уходят в журнал сервера).
public sealed record ReadinessResponse(string Status, DateTime UtcNow, ReadinessChecks Checks);

public sealed record ReadinessChecks(string Database, string Ras, string Hangfire);

// Грубые строковые статусы (лоуэркейс, как существующий liveness status:"ok").
internal static class ReadinessStatus
{
    // overall
    public const string Ready = "ready";
    public const string Degraded = "degraded";
    public const string NotReady = "not_ready";

    // суб-статусы проб
    public const string Ok = "ok";
    public const string Down = "down";
    public const string Unknown = "unknown"; // RAS: первые 30с после старта («Проверка…»)
}

namespace MitLicenseCenter.Application.Performance;

// Live-снимок DMV-метрик MSSQL для раздела «Быстродействие» — «1С грузит SQL?» (MLC-068,
// ADR-26, Фаза 3). Адаптер читает динамические представления (DMV) по требованию (pull) и
// НИЧЕГО не персистит — live-модель ADR-26. Реализация — SqlPerformanceProbe (Infrastructure,
// ADO.NET через строку панели); в тестах — StubSqlPerformanceProbe.
public interface ISqlPerformanceProbe
{
    // Снимает активные запросы + цепочки блокировок + IO-stall по базам + дельту wait-stats.
    // Никогда не бросает ради инфраструктурного сбоя: нет права VIEW SERVER STATE → снимок со
    // Status=PermissionDenied (честный degraded-сигнал, как баннер MLC-064a), SQL недоступен →
    // Status=Unavailable. Пустые списки при degraded, а не исключение.
    Task<SqlPerformanceSnapshot> CaptureAsync(CancellationToken ct);
}

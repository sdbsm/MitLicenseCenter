namespace MitLicenseCenter.Application.Reporting;

// Singleton-аккумулятор сбора time-series потребления лицензий (MLC-048, ADR-25).
// Cold ReconciliationJob (scoped, per-call) подаёт мгновенные семплы каждые ≈25с;
// аккумулятор копит running min/max/sum/count на тенанта в текущем 15-мин бакете и
// при пересечении границы бакета возвращает готовые строки прошлого бакета. Состояние
// живёт между инвокациями джобы → регистрируется singleton (как ColdThrottleState).
// Реализация thread-safe. Частичный бакет при рестарте процесса теряется (best-effort
// телеметрия — на graceful shutdown не флашим).
public interface ILicenseUsageAccumulator
{
    // Складывает семпл момента sampleUtc по набору тенантов в текущий бакет. Если
    // sampleUtc попал в новый 15-мин бакет — возвращает агрегаты закрытого прошлого
    // бакета (по строке на тенанта) и начинает новый; иначе возвращает пустой список.
    IReadOnlyList<LicenseUsageBucket> RecordSample(
        DateTime sampleUtc,
        IReadOnlyCollection<LicenseUsageSample> samples);
}

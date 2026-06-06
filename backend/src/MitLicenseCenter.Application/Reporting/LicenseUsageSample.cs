namespace MitLicenseCenter.Application.Reporting;

// Мгновенный замер потребления лицензий одним тенантом в момент cold-цикла
// (MLC-048, ADR-25). Consumed — concurrent-сессии, потребляющие лицензию; Limit —
// MaxConcurrentLicenses тенанта на момент замера. Нейтральный вход аккумулятора:
// Application не зависит от Infrastructure-сущности LicenseUsageSnapshot (NetArchTest).
public sealed record LicenseUsageSample(Guid TenantId, int Consumed, int Limit);

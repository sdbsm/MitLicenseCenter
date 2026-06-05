namespace MitLicenseCenter.Domain.Publications;

// Целочисленные значения зафиксированы — контракт с БД (HasConversion<int>)
// и UI (JsonStringEnumConverter сериализует именем). Webinst = публикация
// сделана/перезаписана панелью через webinst.exe (MLC-045). Configurator =
// помечена как ручная (конфигуратор/внешний инструмент) — повторная
// webinst-публикация требует явного подтверждения. Unknown = происхождение
// неизвестно (дефолт для строк, существовавших до MLC-045).
public enum PublicationSource
{
    Unknown = 0,
    Webinst = 1,
    Configurator = 2,
}

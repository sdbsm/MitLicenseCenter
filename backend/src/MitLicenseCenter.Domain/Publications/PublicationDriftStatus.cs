namespace MitLicenseCenter.Domain.Publications;

// Целочисленные значения зафиксированы — контракт с БД (HasConversion<int>)
// и UI (JsonStringEnumConverter сериализует именем). Drift = поля
// default.vrd / IIS-приложения расходятся с desired-state. Missing = ресурс
// (site / default.vrd) физически отсутствует. Error = адаптер не смог прочитать.
public enum PublicationDriftStatus
{
    InSync = 0,
    Drift = 1,
    Missing = 2,
    Error = 3,
}

namespace MitLicenseCenter.Domain.Infobases;

// Целочисленные значения зафиксированы — это контракт с БД (HasConversion<int>).
// На wire значения уходят строкой через JsonStringEnumConverter (см. Program.cs).
public enum InfobaseStatus
{
    Active = 0,
    Maintenance = 1,
    Suspended = 2,
}

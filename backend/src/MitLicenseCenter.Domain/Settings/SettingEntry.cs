namespace MitLicenseCenter.Domain.Settings;

// Одна строка таблицы dbo.Settings: либо plain (ValueText заполнен, Value null),
// либо secret (Value содержит DPAPI-зашифрованные UTF-8 байты, ValueText null).
// IsSecret фиксирует, какой столбец считается актуальным на write-side; читатели
// смотрят на оба, но никогда не должны видеть оба заполненными одновременно.
public sealed class SettingEntry
{
    public string Key { get; set; } = string.Empty;
    public string? ValueText { get; set; }
    public byte[]? Value { get; set; }
    public bool IsSecret { get; set; }
    public string? Description { get; set; }
    public DateTime UpdatedAt { get; set; }
    public string UpdatedBy { get; set; } = string.Empty;
}

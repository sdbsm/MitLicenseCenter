namespace MitLicenseCenter.Web.Endpoints;

// Wire-DTO. SettingDescriptor (Application) → SettingDescriptorResponse: имя
// поля ValueText → Value на стороне API. Для секретов Value всегда null
// (маскировка в store, дублируется ниже в endpoint'е на всякий случай).
public sealed record SettingDescriptorResponse(
    string Key,
    bool IsSecret,
    bool IsSet,
    string? Value,
    string? Description,
    DateTime UpdatedAt,
    string UpdatedBy);

// `Value=null` → очистка значения (для секретов = «убрать пароль»). Whitespace
// тоже трактуется как очистка.
public sealed record UpdateSettingRequest(string? Value);

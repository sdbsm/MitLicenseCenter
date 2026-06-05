namespace MitLicenseCenter.Application.Clusters;

// Презентация имени пользователя сеанса. rac.exe отдаёт пустой `user-name` для
// сеансов без явного пользователя (базовая/однопользовательская ИБ) — показываем
// читаемую метку вместо пустоты. Парность: тот же литерал на фронте —
// frontend/src/i18n/ru.json `sessions.noUser`.
public static class SessionDisplay
{
    public const string NoUserLabel = "без пользователя";

    public static string UserNameOrFallback(string? userName) =>
        string.IsNullOrWhiteSpace(userName) ? NoUserLabel : userName;
}

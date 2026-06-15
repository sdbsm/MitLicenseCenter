namespace MitLicenseCenter.Domain.Updates;

// MLC-176 — единственное правило «доступно ли обновление»: latest строго больше
// current. Любая сторона не парсится (мусорный тег / отсутствует версия) → false:
// без надёжной сверки баннер «доступна версия» не показываем (не пугаем оператора
// ложным сигналом).
public static class UpdateComparison
{
    public static bool IsUpdateAvailable(string? currentRaw, string? latestTag)
    {
        if (!AppVersion.TryParse(currentRaw, out var current)
            || !AppVersion.TryParse(latestTag, out var latest))
        {
            return false;
        }

        return latest.CompareTo(current) > 0;
    }
}

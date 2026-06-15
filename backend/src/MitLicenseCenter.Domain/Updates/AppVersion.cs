using System.Globalization;

namespace MitLicenseCenter.Domain.Updates;

// MLC-176 — чистый semver-компаратор для сверки текущей версии панели с тегом
// последнего GitHub-релиза. Намеренно минимален: канал релизов — `major.minor.patch`
// с опциональным prerelease-суффиксом (`v0.7.0-beta`). Сам суффикс НЕ сравниваем —
// важен лишь факт «это предрелиз» (release > prerelease при равной тройке), чтобы
// финальный 0.7.0 обходил свой же 0.7.0-beta, но никакая «beta1 < beta2» магия не
// нужна (релизный поток линеен, single-host ADR-28).
public readonly record struct AppVersion(int Major, int Minor, int Patch, bool IsPrerelease)
{
    // Парсит `[v]major.minor.patch[-suffix]`. Тег релиза (`v0.7.0-beta`) и
    // informational-версия сборки (`0.7.0-beta`) дают один и тот же результат —
    // ведущий `v`/`V` срезается. Непарсимое/null → false (вызывающий трактует как
    // «сравнить нельзя» → обновление недоступно).
    public static bool TryParse(string? raw, out AppVersion value)
    {
        value = default;
        if (string.IsNullOrWhiteSpace(raw))
        {
            return false;
        }

        var span = raw.Trim();
        if (span.Length > 0 && (span[0] == 'v' || span[0] == 'V'))
        {
            span = span[1..];
        }

        var isPrerelease = false;
        var dash = span.IndexOf('-');
        if (dash >= 0)
        {
            // Любой суффикс после '-' → предрелиз. Сам суффикс не сохраняем.
            isPrerelease = true;
            span = span[..dash];
        }

        var parts = span.Split('.');
        if (parts.Length != 3)
        {
            return false;
        }

        if (!TryParseComponent(parts[0], out var major)
            || !TryParseComponent(parts[1], out var minor)
            || !TryParseComponent(parts[2], out var patch))
        {
            return false;
        }

        value = new AppVersion(major, minor, patch, isPrerelease);
        return true;
    }

    // Сначала по тройке Major/Minor/Patch; при равной тройке release (без prerelease)
    // строго больше prerelease; два prerelease при равной тройке → равны.
    public int CompareTo(AppVersion other)
    {
        var byMajor = Major.CompareTo(other.Major);
        if (byMajor != 0)
        {
            return byMajor;
        }

        var byMinor = Minor.CompareTo(other.Minor);
        if (byMinor != 0)
        {
            return byMinor;
        }

        var byPatch = Patch.CompareTo(other.Patch);
        if (byPatch != 0)
        {
            return byPatch;
        }

        // Тройка равна: release > prerelease. IsPrerelease=false «больше».
        return other.IsPrerelease.CompareTo(IsPrerelease);
    }

    private static bool TryParseComponent(string component, out int value) =>
        int.TryParse(component, NumberStyles.None, CultureInfo.InvariantCulture, out value);
}

using MitLicenseCenter.Application.Publishing;
using MitLicenseCenter.Domain.Publications;

namespace MitLicenseCenter.Infrastructure.Publishing;

// Pure-function helper (MLC-045): превращает прочитанный из IIS факт
// (PublicationActualState) в read-only статус + человекочитаемые детали для
// оператора. НЕ сравнивает с эталоном — это не drift-детектор (тот удалён вместе
// с enforcement). Покрыт unit-тестами без IIS/файловой системы.
internal static class PublicationStatusEvaluator
{
    public const int MaxDetailsLength = 1024;

    public static (PublicationPublishStatus Status, string Details) Evaluate(
        Publication publication,
        PublicationActualState actual)
    {
        ArgumentNullException.ThrowIfNull(publication);
        ArgumentNullException.ThrowIfNull(actual);

        // Error побеждает над всем — адаптер не смог прочитать состояние.
        if (!string.IsNullOrWhiteSpace(actual.Error))
        {
            return (PublicationPublishStatus.Error, Truncate($"Не удалось проверить публикацию: {actual.Error}"));
        }

        if (!actual.SiteExists)
        {
            return (PublicationPublishStatus.NotPublished,
                Truncate($"IIS-сайт «{publication.SiteName}» не найден."));
        }
        if (!actual.VirtualPathExists)
        {
            return (PublicationPublishStatus.NotPublished,
                Truncate($"Виртуальный путь «{publication.VirtualPath}» отсутствует в сайте «{publication.SiteName}»."));
        }
        if (!actual.WebConfigExists)
        {
            return (PublicationPublishStatus.NotPublished,
                Truncate($"Файл web.config для публикации «{publication.SiteName}{publication.VirtualPath}» не найден."));
        }

        var version = string.IsNullOrWhiteSpace(actual.PlatformVersion)
            ? "версия платформы не определена"
            : $"web.config → платформа {actual.PlatformVersion}";
        return (PublicationPublishStatus.Published,
            Truncate($"Публикация на месте: сайт «{publication.SiteName}», путь «{publication.VirtualPath}», {version}."));
    }

    private static string Truncate(string s) =>
        s.Length <= MaxDetailsLength ? s : string.Concat(s.AsSpan(0, MaxDetailsLength - 1), "…");
}

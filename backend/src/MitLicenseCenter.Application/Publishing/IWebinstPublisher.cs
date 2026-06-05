using MitLicenseCenter.Domain.Infobases;
using MitLicenseCenter.Domain.Publications;

namespace MitLicenseCenter.Application.Publishing;

// Адаптер публикации 1С-инфобазы на IIS через webinst.exe (MLC-045, ADR-20).
// Запускает webinst той версии платформы, что указана в публикации
// (…\1cv8\<версия>\bin\webinst.exe), с -publish -iis. webinst перезаписывает
// default.vrd и web.config целиком — поэтому повторная публикация безопасна
// только для Source=Webinst (гейт в эндпоинте).
public interface IWebinstPublisher
{
    Task<WebinstResult> PublishAsync(Publication publication, Infobase infobase, CancellationToken ct);
}

// Результат запуска webinst. Success — exit 0. При неуспехе ErrorDetail несёт
// санитизированную русскую формулировку для 409 (сырой вывод webinst — в лог сервера).
public sealed record WebinstResult(bool Success, string? ErrorDetail)
{
    public static WebinstResult Ok() => new(true, null);
    public static WebinstResult Failed(string detail) => new(false, detail);
}

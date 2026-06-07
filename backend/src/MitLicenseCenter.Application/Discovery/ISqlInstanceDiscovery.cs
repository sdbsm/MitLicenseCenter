namespace MitLicenseCenter.Application.Discovery;

// Discovery локальных инстансов MSSQL из реестра Windows. Используется формой
// инфобазы и страницей Settings вместо ручного ввода сервера БД. Топология
// single-node — SQL всегда на localhost, единственное неизвестное это имя инстанса.
public interface ISqlInstanceDiscovery
{
    // Возвращает серверные строки подключения (localhost, localhost\SQLEXPRESS).
    // Может бросить при недоступности реестра — ловит эндпоинт (Available:false).
    IReadOnlyList<string> FindLocalInstances();
}

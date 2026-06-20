namespace MitLicenseCenter.Application.TechLog;

// Адаптер файловых операций над logcfg.xml в conf платформы (MLC-230). Всё за интерфейсом —
// юнит-тесты мокают его, build.ps1 зелёный без живой 1С и без прав на Program Files. Путь —
// <корень 1С>\conf\logcfg.xml (OneCInstallRoots; стенд: C:\Program Files\1cv8\conf\logcfg.xml).
// Сервисный аккаунт NT SERVICE\MitLicenseCenter по умолчанию прав записи в conf НЕ имеет
// (60_SAFETY ACL): ProbeWriteAccess отдаёт структурную диагностику с точной командой icacls
// (зеркаль RAS-healing, где оператору отдаётся точная sc-команда). Грант (M) на стенде проверен
// рабочим (MLC-229). НЕ правит установщик.
public interface ILogcfgStore
{
    // Путь к conf\logcfg.xml (через OneCInstallRoots). null — корень 1С (…\1cv8\conf) не найден.
    string? ResolveLogcfgPath();

    // Проба прав записи в logcfg.xml. Никогда не бросает — возвращает структурный результат
    // (зеркаль RasServiceDiagnosis): можно ли писать + точная команда icacls для оператора.
    LogcfgWriteProbe ProbeWriteAccess();

    // Читает текущий logcfg.xml. null — файла нет (conf без настроенного ТЖ — допустимо).
    string? ReadLogcfg();

    // Записывает целевой logcfg.xml в conf. Перед первой записью сохраняет резервную копию
    // исходного (если бэкап ещё не сделан) — для точного восстановления при снятии.
    void WriteLogcfg(string content);

    // Восстанавливает исходный logcfg.xml из резервной копии. Если бэкапа нет (исходного файла
    // не было) — удаляет наш logcfg.xml, возвращая conf к «нет настроенного ТЖ».
    void RestoreOriginal();

    // Есть ли резервная копия исходного logcfg (сделан ли бэкап при установке) — для идемпотентности.
    bool HasBackup();
}

// Результат пробы прав записи (структурная диагностика, never-throws). CanWrite — можно ли писать
// logcfg.xml под текущим аккаунтом. GrantCommand — точная команда icacls для оператора, если нет
// прав (зеркаль RasServiceDiagnosis.CommandPreview). Issue — человекочитаемая причина.
public sealed record LogcfgWriteProbe(
    bool CanWrite,
    string? LogcfgPath,
    string? GrantCommand,
    string? Issue);

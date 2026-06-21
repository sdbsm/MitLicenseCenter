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

    // Свободное место (байты) на диске, где лежит каталог сбора ТЖ. Seam для сторожа места
    // (60_SAFETY №3): тест симулирует нехватку без реального диска. null — том не определить
    // (несуществующий путь/ошибка) → трактуем как «проверка невозможна», не блокируем старт.
    long? GetAvailableFreeSpaceBytes(string directory);

    // Суммарный размер каталога сбора ТЖ (байты) — для авто-стопа по лимиту диска (60_SAFETY №3).
    // Seam: тест симулирует превышение без реальных файлов. Каталога нет/ошибка чтения → 0.
    long GetDirectorySizeBytes(string directory);

    // Проба прав аккаунта агента 1С на каталог сбора ТЖ (MLC-247 A2, 41_LOGCFG_SPEC §6). Процессы 1С
    // пишут ТЖ под СВОИМ аккаунтом и должны иметь полные права на каталог сбора (и \dumps); панель лишь
    // создаёт каталог. Никогда не бросает — структурный результат (зеркаль ProbeWriteAccess): есть ли
    // у agentAccount право Modify/FullControl + точная команда icacls для оператора, если нет.
    // Seam для детерминированных тестов через FakeLogcfgStore. best-effort: групповые/эффективные права
    // полностью не разворачиваем (см. DirectoryAclProbeResult).
    DirectoryAclProbeResult ProbeAgentDirectoryAccess(string directory, string agentAccount);
}

// Результат пробы прав аккаунта агента 1С на каталог сбора (MLC-247 A2, структурная диагностика,
// never-throws; зеркаль LogcfgWriteProbe). HasAccess — обнаружено ли Allow-правило Modify/FullControl/
// Write для agentAccount. Determined — удалось ли вообще проверить (на не-Windows/ошибке доступа =
// false: «проверка невозможна», толерантно — не блокируем). GrantCommand — точная команда icacls для
// оператора, если права не обнаружены. Issue — человекочитаемая причина/предупреждение.
//
// ⚠ best-effort: проверяется ТОЛЬКО прямое Allow-правило на самом каталоге для указанного
// NTAccount-имени. Членство в группах (агент входит в группу с правами) и эффективные права полностью
// НЕ разворачиваются — возможны ложные «нет доступа», когда права даны через группу. Поэтому при
// HasAccess=false установка с пустым аккаунтом НЕ блокируется, а с заданным — отдаёт команду как
// рекомендацию оператору (детект → команда, без авто-гранта).
public sealed record DirectoryAclProbeResult(
    bool HasAccess,
    bool Determined,
    string? GrantCommand,
    string? Issue);

// Результат пробы прав записи (структурная диагностика, never-throws). CanWrite — можно ли писать
// logcfg.xml под текущим аккаунтом. GrantCommand — точная команда icacls для оператора, если нет
// прав (зеркаль RasServiceDiagnosis.CommandPreview). Issue — человекочитаемая причина.
public sealed record LogcfgWriteProbe(
    bool CanWrite,
    string? LogcfgPath,
    string? GrantCommand,
    string? Issue);

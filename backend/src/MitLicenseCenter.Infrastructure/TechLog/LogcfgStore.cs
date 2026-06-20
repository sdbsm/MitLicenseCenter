using MitLicenseCenter.Application.TechLog;
using MitLicenseCenter.Infrastructure.Discovery;

namespace MitLicenseCenter.Infrastructure.TechLog;

// Windows-адаптер файловых операций над logcfg.xml в conf платформы (MLC-230). Путь —
// <корень 1С>\conf\logcfg.xml (OneCInstallRoots; стенд: C:\Program Files\1cv8\conf\logcfg.xml).
// Все ФС-операции здесь, за интерфейсом ILogcfgStore — юнит-тесты сервиса мокают его, build.ps1
// зелёный без живой 1С и без прав на Program Files. Бэкап исходного — рядом, с суффиксом .mlc-backup;
// проба прав — реальной попыткой открыть файл на запись (never-throws → структурный результат с
// точной командой icacls, зеркаль RAS-healing). Грант (M) на стенде проверен рабочим (MLC-229).
internal sealed class LogcfgStore : ILogcfgStore
{
    // Сервисный аккаунт панели — у него по умолчанию нет прав записи в conf (60_SAFETY ACL).
    private const string ServiceAccount = "NT SERVICE\\MitLicenseCenter";

    // Суффикс резервной копии исходного logcfg (рядом с ним, в conf).
    private const string BackupSuffix = ".mlc-backup";

    public string? ResolveLogcfgPath()
    {
        // Берём первый существующий каталог conf под корнем 1С (…\1cv8\conf). Файла logcfg.xml там
        // может ещё не быть (conf без настроенного ТЖ) — это валидно, важен сам каталог conf.
        foreach (var root in OneCInstallRoots.Get())
        {
            var confDir = Path.Combine(root, "conf");
            if (Directory.Exists(confDir))
            {
                return Path.Combine(confDir, "logcfg.xml");
            }
        }

        return null;
    }

    public LogcfgWriteProbe ProbeWriteAccess()
    {
        var path = ResolveLogcfgPath();
        if (path is null)
        {
            return new LogcfgWriteProbe(
                CanWrite: false,
                LogcfgPath: null,
                GrantCommand: null,
                Issue: "Не найден каталог conf платформы 1С (…\\1cv8\\conf). Проверьте установку 1С на узле.");
        }

        if (CanWrite(path))
        {
            return new LogcfgWriteProbe(CanWrite: true, LogcfgPath: path, GrantCommand: null, Issue: null);
        }

        // Нет прав — отдаём оператору точную команду гранта (зеркаль RAS sc-команды). На стенде
        // MLC-229 этот грант (M) проверен рабочим.
        return new LogcfgWriteProbe(
            CanWrite: false,
            LogcfgPath: path,
            GrantCommand: $"icacls \"{path}\" /grant \"{ServiceAccount}:(M)\"",
            Issue: $"Нет прав записи в {path}. Сервисный аккаунт {ServiceAccount} по умолчанию имеет " +
                   "только чтение conf — выдайте право Modify приведённой командой icacls (от администратора).");
    }

    public string? ReadLogcfg()
    {
        var path = ResolveLogcfgPath();
        return path is not null && File.Exists(path) ? File.ReadAllText(path) : null;
    }

    public void WriteLogcfg(string content)
    {
        var path = RequirePath();

        // Бэкап исходного — один раз, перед первой нашей записью. Если файла не было — бэкап не
        // создаётся (RestoreOriginal в этом случае удалит наш файл).
        if (File.Exists(path) && !File.Exists(path + BackupSuffix))
        {
            File.Copy(path, path + BackupSuffix);
        }

        File.WriteAllText(path, content);
    }

    public void RestoreOriginal()
    {
        var path = RequirePath();
        var backup = path + BackupSuffix;

        if (File.Exists(backup))
        {
            File.Copy(backup, path, overwrite: true);
            File.Delete(backup);
        }
        else if (File.Exists(path))
        {
            // Исходного logcfg не было (бэкап не делали) → возвращаем conf к «нет настроенного ТЖ».
            File.Delete(path);
        }
    }

    public bool HasBackup()
    {
        var path = ResolveLogcfgPath();
        return path is not null && File.Exists(path + BackupSuffix);
    }

    private string RequirePath() =>
        ResolveLogcfgPath()
        ?? throw new InvalidOperationException(
            "Не найден каталог conf платформы 1С (…\\1cv8\\conf) — управление logcfg.xml невозможно.");

    // Проба прав записи реальной попыткой открыть на запись (или создать, если файла нет). Never-throws:
    // любая ошибка доступа → false. Файл не модифицируется (открываем и сразу закрываем; созданный
    // пробой пустой файл удаляем).
    private static bool CanWrite(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                using var fs = new FileStream(path, FileMode.Open, FileAccess.Write, FileShare.ReadWrite);
                return true;
            }

            // Файла нет — проба прав на создание в каталоге conf через временный файл.
            using (var fs = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
            }

            File.Delete(path);
            return true;
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException or System.Security.SecurityException)
        {
            return false;
        }
    }
}

using System.Runtime.Versioning;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;
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
        // Закалка MLC-231 (60_SAFETY №6, передаточная гоча MLC-230): НЕ бэкапить как «исходный»
        // уже-НАШ конфиг (по маркеру) — иначе резервная копия исходного перезапишется нашим файлом,
        // и при снятии восстановится наш конфиг вместо реального исходного. Single-active по БД
        // почти закрывает этот край, но маркер-guard в store надёжнее.
        if (File.Exists(path) && !File.Exists(path + BackupSuffix) && !IsOurs(path))
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

    public long? GetAvailableFreeSpaceBytes(string directory)
    {
        // Свободное место на томе каталога сбора (60_SAFETY №3). Каталог может ещё не существовать —
        // берём корень тома по пути. Любая ошибка (несуществующий том, недоступность) → null =
        // «проверить не удалось», старт не блокируем (отказ — только при ЗАВЕДОМОЙ нехватке).
        try
        {
            var root = Path.GetPathRoot(Path.GetFullPath(directory));
            if (string.IsNullOrEmpty(root))
            {
                return null;
            }

            var drive = new DriveInfo(root);
            return drive.IsReady ? drive.AvailableFreeSpace : null;
        }
        catch (Exception ex) when (ex is ArgumentException or IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            return null;
        }
    }

    public long GetDirectorySizeBytes(string directory)
    {
        // Суммарный размер всех файлов каталога сбора (60_SAFETY №3). Каталога ещё нет/ошибка чтения
        // → 0 (сбор ещё ничего не записал — не повод для авто-стопа). Перечисляем рекурсивно;
        // отдельные недоступные файлы пропускаем, общий счёт не роняем.
        try
        {
            if (!Directory.Exists(directory))
            {
                return 0;
            }

            long total = 0;
            foreach (var file in Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories))
            {
                try
                {
                    total += new FileInfo(file).Length;
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
                {
                    // Файл исчез/недоступен между перечислением и чтением — пропускаем.
                }
            }

            return total;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            return 0;
        }
    }

    public IEnumerable<string> ReadCollectionLines(string directory)
    {
        // Потоковое чтение сырья ТЖ (MLC-238): перечисляем NDJSON-файлы каталога (рекурсивно) и отдаём
        // их строки ленивым перечислением — память не растёт на больших журналах. Never-throws: каталога
        // нет/ошибка перечисления → пустая последовательность; ошибка чтения отдельного файла → файл
        // пропускается (best-effort, как сторож размера каталога).
        string[] files;
        try
        {
            if (!Directory.Exists(directory))
            {
                yield break;
            }

            files = Directory.GetFiles(directory, "*", SearchOption.AllDirectories);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            yield break;
        }

        Array.Sort(files, StringComparer.Ordinal);

        foreach (var file in files)
        {
            // Открытие файла отделено от перечисления строк: ошибку открытия глотаем и пропускаем файл,
            // а во время самого чтения yield не может быть внутри try/catch — читаем построчно через
            // ReadLineSafely (FileShare.ReadWrite — платформа 1С может держать файл на запись).
            StreamReader? reader = null;
            try
            {
                var stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                reader = new StreamReader(stream, Encoding.UTF8);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
            {
                reader?.Dispose();
                continue;
            }

            using (reader)
            {
                while (true)
                {
                    string? line;
                    try
                    {
                        line = reader.ReadLine();
                    }
                    catch (Exception ex) when (ex is IOException or System.Security.SecurityException)
                    {
                        break; // обрыв чтения файла — переходим к следующему (best-effort)
                    }

                    if (line is null)
                    {
                        break;
                    }

                    yield return line;
                }
            }
        }
    }

    public void DeleteCollectionFiles(string directory)
    {
        // Удаление сырья ТЖ после успешного анализа (MLC-238, решение MLC-237 Q2). Идемпотентно и
        // never-throws: каталога нет → no-op; отдельный занятый/недоступный файл пропускаем (best-effort).
        // Удаляем только файлы, сам каталог сбора оставляем (под него выдан ACL агента, MLC-247).
        try
        {
            if (!Directory.Exists(directory))
            {
                return;
            }

            foreach (var file in Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories))
            {
                try
                {
                    File.Delete(file);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
                {
                    // Файл занят/недоступен — пропускаем (повторная ротация платформы/следующий цикл доснесёт).
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            // Перечисление не удалось — no-op (best-effort).
        }
    }

    public DirectoryAclProbeResult ProbeAgentDirectoryAccess(string directory, string agentAccount)
    {
        // MLC-247 A2 (41_LOGCFG_SPEC §6): процессы 1С пишут ТЖ под аккаунтом агента и должны иметь
        // полные права (Modify/FullControl) на каталог сбора. Никогда не бросаем — структурная
        // диагностика (зеркаль ProbeWriteAccess). На не-Windows ACL недоступны → «проверка невозможна».
        var grantCommand = BuildGrantCommand(directory, agentAccount);

        if (!OperatingSystem.IsWindows())
        {
            return new DirectoryAclProbeResult(
                HasAccess: false, Determined: false, GrantCommand: grantCommand,
                Issue: "Проверка прав на каталог сбора возможна только на Windows.");
        }

        return ProbeAgentDirectoryAccessWindows(directory, agentAccount, grantCommand);
    }

    // Точная команда гранта для оператора (наследование (OI)(CI) на подкаталоги <процесс>_<pid> и \dumps,
    // рекурсивно /T, право Modify (M)). 41_LOGCFG_SPEC §6: полные права на каталог сбора и \dumps.
    private static string BuildGrantCommand(string directory, string agentAccount)
        => $"icacls \"{directory}\" /grant \"{agentAccount}:(OI)(CI)(M)\" /T";

    [SupportedOSPlatform("windows")]
    private static DirectoryAclProbeResult ProbeAgentDirectoryAccessWindows(
        string directory, string agentAccount, string grantCommand)
    {
        try
        {
            if (!Directory.Exists(directory))
            {
                // Каталог ещё не создан — проверять нечего; не блокируем (создаст панель, грант — отдельно).
                return new DirectoryAclProbeResult(
                    HasAccess: false, Determined: false, GrantCommand: grantCommand,
                    Issue: $"Каталог сбора {directory} ещё не существует — права проверим после создания.");
            }

            var rules = new DirectoryInfo(directory)
                .GetAccessControl()
                .GetAccessRules(includeExplicit: true, includeInherited: true, typeof(NTAccount));

            // best-effort: ищем Allow-правило для agentAccount (по имени NTAccount) с правом записи
            // (Modify/FullControl/Write). Членство в группах НЕ разворачиваем — возможен ложный «нет».
            const FileSystemRights writeRights =
                FileSystemRights.Write | FileSystemRights.Modify | FileSystemRights.FullControl;

            foreach (FileSystemAccessRule rule in rules)
            {
                if (rule.AccessControlType != AccessControlType.Allow)
                {
                    continue;
                }

                if (!string.Equals(rule.IdentityReference.Value, agentAccount, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if ((rule.FileSystemRights & writeRights) != 0)
                {
                    return new DirectoryAclProbeResult(
                        HasAccess: true, Determined: true, GrantCommand: null, Issue: null);
                }
            }

            // Прямого Allow-правила записи для аккаунта не нашли — отдаём команду (best-effort).
            return new DirectoryAclProbeResult(
                HasAccess: false, Determined: true, GrantCommand: grantCommand,
                Issue: $"У аккаунта {agentAccount} не обнаружено прав записи на каталог сбора {directory}. " +
                       "Процессы 1С пишут ТЖ под своим аккаунтом — без полных прав журнал не пишется. " +
                       "Выдайте право Modify приведённой командой icacls (от администратора). " +
                       "Если право дано через группу — это ожидаемая ложная тревога (проверка прямых прав).");
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException
                                       or System.Security.SecurityException
                                       or PlatformNotSupportedException or InvalidOperationException)
        {
            // Прочитать ACL не удалось → «проверка невозможна», толерантно не блокируем.
            return new DirectoryAclProbeResult(
                HasAccess: false, Determined: false, GrantCommand: grantCommand,
                Issue: $"Не удалось проверить права на каталог сбора {directory}: {ex.Message}");
        }
    }

    // Наш ли logcfg по пути (по маркеру LogcfgBuilder.Marker). Never-throws: ошибка чтения → false
    // (трактуем как «не наш», т.е. бэкап делаем — безопаснее лишний бэкап, чем потеря исходного).
    private static bool IsOurs(string path)
    {
        try
        {
            return File.ReadAllText(path).Contains(LogcfgBuilder.Marker, StringComparison.Ordinal);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            return false;
        }
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

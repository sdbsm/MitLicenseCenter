using MitLicenseCenter.Application.TechLog;
using MitLicenseCenter.Infrastructure.TechLog;

namespace MitLicenseCenter.Tests.Unit.TechLog;

// Тест-дубль ILogcfgStore: моделирует conf\logcfg.xml + бэкап в памяти. Так юнит-тесты сервиса
// не ходят в ФС и не требуют живой 1С / прав на Program Files (build.ps1 зелёный везде).
internal sealed class FakeLogcfgStore : ILogcfgStore
{
    public string Path { get; set; } = @"C:\Program Files\1cv8\conf\logcfg.xml";
    public bool RootFound { get; set; } = true;
    public bool Writable { get; set; } = true;

    public string? Current { get; set; }   // фактический logcfg.xml (null = файла нет)
    public string? Backup { get; private set; }
    private bool _backupTaken;

    // Симуляция диска (60_SAFETY №3): свободное место (null = «том не определить») и размер каталога
    // сбора — тесты выставляют, чтобы проверить сторож места без реальной ФС.
    public long? FreeSpaceBytes { get; set; } = long.MaxValue;
    public long DirectorySizeBytes { get; set; }

    public int WriteCalls { get; private set; }
    public int RestoreCalls { get; private set; }

    public string? ResolveLogcfgPath() => RootFound ? Path : null;

    public LogcfgWriteProbe ProbeWriteAccess()
    {
        if (!RootFound)
        {
            return new LogcfgWriteProbe(false, null, null, "conf не найден");
        }

        return Writable
            ? new LogcfgWriteProbe(true, Path, null, null)
            : new LogcfgWriteProbe(false, Path,
                $"icacls \"{Path}\" /grant \"NT SERVICE\\MitLicenseCenter:(M)\"",
                "нет прав записи");
    }

    public string? ReadLogcfg() => Current;

    public void WriteLogcfg(string content)
    {
        // Закалка MLC-231 (60_SAFETY №6): уже-НАШ конфиг (по маркеру) НЕ бэкапим как «исходный» —
        // иначе резервная копия перезапишется нашим файлом. Зеркаль LogcfgStore.WriteLogcfg.
        var currentIsOurs = Current is not null && Current.Contains(LogcfgBuilder.Marker, StringComparison.Ordinal);
        if (Current is not null && !_backupTaken && !currentIsOurs)
        {
            Backup = Current;
            _backupTaken = true;
        }

        Current = content;
        WriteCalls++;
    }

    public void RestoreOriginal()
    {
        RestoreCalls++;
        if (_backupTaken)
        {
            Current = Backup;
            Backup = null;
            _backupTaken = false;
        }
        else
        {
            Current = null;
        }
    }

    public bool HasBackup() => _backupTaken;

    public long? GetAvailableFreeSpaceBytes(string directory) => FreeSpaceBytes;

    public long GetDirectorySizeBytes(string directory) => DirectorySizeBytes;

    // MLC-247 A2: проба прав агента на каталог сбора (seam). Тест выставляет AgentAclResult, чтобы
    // детерминированно проверить InstallAsync без реальных ACL/ФС. По умолчанию — «есть доступ».
    public DirectoryAclProbeResult AgentAclResult { get; set; } =
        new(HasAccess: true, Determined: true, GrantCommand: null, Issue: null);

    public string? AgentAclProbedDirectory { get; private set; }
    public string? AgentAclProbedAccount { get; private set; }

    public DirectoryAclProbeResult ProbeAgentDirectoryAccess(string directory, string agentAccount)
    {
        AgentAclProbedDirectory = directory;
        AgentAclProbedAccount = agentAccount;
        return AgentAclResult;
    }
}

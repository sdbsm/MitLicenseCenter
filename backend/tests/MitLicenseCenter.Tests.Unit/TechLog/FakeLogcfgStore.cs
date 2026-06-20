using MitLicenseCenter.Application.TechLog;

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
        if (Current is not null && !_backupTaken)
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
}

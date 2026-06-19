using System.Diagnostics;
using MitLicenseCenter.Application.Server;

namespace MitLicenseCenter.Infrastructure.Server;

// Завершение ОС-процесса по Pid на локальном узле поверх System.Diagnostics.Process
// (MLC-220, ADR-56). Тонкая граница (ADR-20): прямой Process не течёт в Web/эндпоинт.
// Тестируется ручным фейком (NSubstitute не проксирует internal — гоча трека «Сервер»):
// эта реализация поверх реального ОС-API в юнитах не вызывается.
internal sealed class LocalProcessTerminator : ILocalProcessTerminator
{
    // Имя ОС-процесса без расширения (как Process.ProcessName, напр. "rphost") либо null,
    // если процесса с таким Pid уже/ещё нет. Process.GetProcessById бросает
    // ArgumentException, когда процесс не найден — трактуем как «нет процесса» (null),
    // не как ошибку (idle race с завершившимся rphost — норма).
    public string? GetProcessName(int pid)
    {
        try
        {
            using var process = Process.GetProcessById(pid);
            return process.ProcessName;
        }
        catch (ArgumentException)
        {
            return null;
        }
        catch (InvalidOperationException)
        {
            // Процесс завершился между GetProcessById и чтением имени — считаем «нет».
            return null;
        }
    }

    // Жёсткое завершение процесса. Уже исчез (ArgumentException «нет такого процесса» /
    // InvalidOperationException «процесс уже завершился») → false (идемпотентность,
    // не ошибка). Завершён нами → true. Прочие сбои (нет прав и т.п.) пробрасываются.
    public bool Kill(int pid)
    {
        try
        {
            using var process = Process.GetProcessById(pid);
            process.Kill();
            return true;
        }
        catch (ArgumentException)
        {
            return false;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }
}

namespace MitLicenseCenter.Application.Server;

/// <summary>
/// Абстракция завершения ОС-процесса по <c>Pid</c> на локальном узле (single-host,
/// ADR-28). Тонкая граница над <see cref="System.Diagnostics.Process"/> — прямой доступ
/// к <c>Process</c> из Web/эндпоинта запрещён (ADR-20). Реализация (Infrastructure)
/// читает имя процесса и завершает его; тесты подменяют ручным фейком (NSubstitute не
/// проксирует internal-реализацию).
/// </summary>
/// <remarks>
/// Используется рестартом рабочего процесса 1С (<c>rphost</c>, MLC-220): у <c>rac</c> нет
/// команды «restart process», поэтому рестарт = завершение ОС-процесса <c>rphost</c> по
/// <c>Pid</c>, после чего кластер 1С авто-поднимает новый. Guard от переиспользования
/// <c>Pid</c> ОС-ом: перед kill проверяем, что процесс с этим <c>Pid</c> действительно
/// <c>rphost</c> (имя без расширения), — см. <see cref="GetProcessName"/>.
/// </remarks>
public interface ILocalProcessTerminator
{
    /// <summary>
    /// Имя ОС-процесса с указанным <c>Pid</c> без расширения (как
    /// <c>Process.ProcessName</c>, напр. <c>"rphost"</c>), либо <c>null</c>, если процесс
    /// с таким <c>Pid</c> не существует (уже завершился / никогда не был).
    /// </summary>
    string? GetProcessName(int pid);

    /// <summary>
    /// Завершает ОС-процесс с указанным <c>Pid</c> (жёсткое <c>Kill</c>). Если процесс уже
    /// исчез к моменту вызова — это не ошибка (идемпотентность): возвращает <c>false</c>,
    /// иначе <c>true</c>. Жёсткие сбои завершения пробрасываются вызывающему.
    /// </summary>
    bool Kill(int pid);
}

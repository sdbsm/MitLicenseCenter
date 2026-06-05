using MitLicenseCenter.Application.Clusters;
using MitLicenseCenter.Application.Sessions;

namespace MitLicenseCenter.Application.Jobs;

public interface IKillEnforcer
{
    /// <summary>
    /// Завершает превышающие лимит сессии по снимку <paramref name="snapshot"/>
    /// (newest-first до <c>Consumed == Limit</c>, идемпотентный протокол).
    /// </summary>
    /// <param name="snapshot">Базис расчёта over-limit и kill-кандидатов.</param>
    /// <param name="freshSessions">
    /// Предзагруженный свежий список сессий кластера для fresh-проверки kill (сверка
    /// дескриптора + наличия). <c>null</c> → enforcer делает свой
    /// <c>ListActiveSessionsAsync</c> (cold-путь). Hot-путь передаёт уже полученный
    /// тиком список, чтобы не плодить второй спавн <c>rac.exe</c> (ADR-3.3).
    /// </param>
    /// <param name="ct">Токен отмены.</param>
    /// <remarks>
    /// MLC-044: вызывающий ОБЯЗАН удерживать <see cref="IEnforcementGate"/> на всё время
    /// вызова (и на время получения <paramref name="freshSessions"/>, если он передаётся),
    /// иначе возможен over-kill при параллельном cold+hot enforcement (MLC-001).
    /// </remarks>
    Task EnforceAsync(SnapshotPayload snapshot, IReadOnlyList<ClusterSession>? freshSessions, CancellationToken ct);
}

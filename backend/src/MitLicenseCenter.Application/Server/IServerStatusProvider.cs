namespace MitLicenseCenter.Application.Server;

/// <summary>
/// Read-агрегатор статуса всех служб стека узла (MLC-213, ADR-54/55): сервер(ы) 1С
/// (<c>ragent</c>), RAS, SQL, IIS — единым снимком для раздела «Сервер» веб-панели.
/// Single-host (ADR-28): опрашивается только локальный узел.
/// </summary>
/// <remarks>
/// Композиция над уже существующими адаптерами (обнаружение <c>ragent</c> через реестр,
/// <c>IRasServiceManager</c>, статус службы SQL, <c>IIisLifecycleService</c>). Каждый
/// источник деградирует независимо (<see cref="RasStatusSummary.Available"/> /
/// <see cref="SqlStatusSummary.Available"/> / <see cref="IisStatusSummary.Available"/> +
/// <c>Error</c>) — сбой одного адаптера не валит весь снимок (паттерн discovery IIS,
/// MLC-047). Мутации (старт/стоп/рестарт сервера 1С) — отдельно через
/// <see cref="IWindowsServiceController"/>; здесь только наблюдение.
/// </remarks>
public interface IServerStatusProvider
{
    /// <summary>
    /// Сводный снимок статуса служб узла. Никогда не бросает на сбое отдельного
    /// адаптера (кроме <see cref="OperationCanceledException"/>): провал источника
    /// отражается его флагом <c>Available:false</c> + <c>Error</c>.
    /// </summary>
    Task<ServerStatusSnapshot> GetStatusAsync(CancellationToken ct);
}

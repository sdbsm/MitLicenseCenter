using MitLicenseCenter.Application.Clusters;

namespace MitLicenseCenter.Application.Performance;

// Live-срез нагрузки 1С «кто грузит» для раздела «Быстродействие» (MLC-066, ADR-26):
// активные сеансы с perf-полями (`rac session list`) + рабочие процессы (`rac process list`).
// Pull-по-требованию, НИЧЕГО не персистится (live-модель ADR-26) — собирается на каждый poll,
// пока вкладка открыта. Пустые списки = rac не настроен/недоступен (best-effort, как
// ListActiveSessionsAsync — отсутствие сигнала ≠ ошибка). CapturedAtUtc проставляет эндпоинт.
public sealed record OneCLoadSnapshot(
    DateTime CapturedAtUtc,
    IReadOnlyList<OneCSessionLoad> Sessions,
    IReadOnlyList<OneCProcessLoad> Processes);

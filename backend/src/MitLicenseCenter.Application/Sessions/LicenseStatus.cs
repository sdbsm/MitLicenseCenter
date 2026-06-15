namespace MitLicenseCenter.Application.Sessions;

// Трёхсостояние потребления лицензии сеансом (ADR-48). Источник истины — факт
// `rac session list --licenses`, а не эвристика по app-id (удалена в MLC-166).
//   Consuming    — сеанс присутствует в выводе `--licenses` (держит клиентскую лицензию).
//   NotConsuming — сеанс известен холодному факту, но в выводе `--licenses` отсутствует.
//   Pending      — факт по сеансу ещё неизвестен (свежий сеанс на горячем тире, либо
//                  факт временно недоступен). В подсчёт лимита и в kill-кандидаты НЕ входит.
public enum LicenseStatus
{
    Consuming,
    NotConsuming,
    Pending,
}

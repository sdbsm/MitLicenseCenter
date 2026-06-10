# MitLicense Center — Roadmap

Forward-looking status. The full system spec lives in `01`–`06`, `DECISIONS.md`, and `OPERATIONS.md`.

## Current status — v1 delivered

The application is a working control plane for single-node multi-tenant 1C hosting:

- **Tenants / Infobases / Publications** CRUD with the FK and uniqueness contracts in `03_DOMAIN_MODEL.md`. Tenant→Infobase distribution is visible both ways: a «Базы: N» link + per-client detail lens (`/tenants/:id`), and the bases list filters by client via URL (`?tenantId=`). Bases move between clients via an explicit `POST /infobases/{id}/reassign` (audited, name-collision-guarded). Publications live as a tab-and-columns part of the unified «Базы» page (UX rework `MLC-081..086`, 2026-06-10).
- **Session & license enforcement** — two-tier reconciliation loop (hot 3–5s / cold 20–30s), newest-first idempotent kill, manual kill from the Sessions Monitor.
- **1C cluster adapter** — RAS via `rac.exe` only (ADR-16 / ADR-3.3), with a 30s RAS health probe surfaced on the Dashboard.
- **IIS publications** — (re)publish via `webinst` + platform change via `web.config` rewrite + read-only status (ADR-4; ADR-4.1 surgical-patch/drift model revoked).
- **Settings** — encrypted `dbo.Settings` (DPAPI) with the 17-key catalog in `04_INFRASTRUCTURE.md`.
- **Audit** — immutable log with server-side paging/filtering and a daily retention purge.
- **Frontend** — React + TS SPA on shadcn/ui, Russian-only locale, Vitest test foundation.
- **CI** — GitHub Actions (build + test + lint), no CD (manual deploy).
- **Observability** — hot-path metrics via `System.Diagnostics.Metrics` (`rac.exe` spawns, cold/hot cycle latency, kills), opt-in EF query profiling, and a dependency `readiness` probe (`/api/v1/health/ready`) alongside the cheap liveness `/api/v1/health`. Snapshot read with `dotnet-counters` (no external systems — ADR-15).

Operator concerns (backup, network-edge auth) are documented in `OPERATIONS.md`.

## Backlog / deferred

- **UX-трек, этап 2: single-host бек-чистка** — gated. Этап 1 (`MLC-081..086`, frontend-only)
  выполнен 2026-06-10; этап 2 = бек-хвосты по описи §7 аудита (`.claude/plans/ux-audit-single-host.md`:
  колонка `Infobase.DatabaseServer`, параметр `server=` discovery, ключ `OneC.Cluster.Server`,
  переименование `Defaults.DatabaseServer`, сужение discovery-интерфейсов) + ADR «Single-host
  topology» + правка канона 01/03/04 + кандидаты из архивной секции трека. Триггер: пользователь
  пожил с новым UI и подтвердил single-host окончательно. До этого single-host в ADR не фиксируется
  (тег отката `v1-pre-ux-redesign`).
- **RAS Strategy B** — replace `rac.exe`-per-cycle with a long-lived TCP socket on 1545. The cross-call cluster-UUID cache (MLC-041) already roughly halved the steady-state spawn rate (the kill path and hot polling now cost ~1 spawn each), so the ≤26 procs/min budget has comfortable headroom; this further optimization stays gated on real-world latency measurement.
- **Multi-cluster / multi-node topology** — every adapter currently assumes single-node; opening this up requires re-reviewing each adapter and the single-node operational constraint.
- **UI: заспечено в `05`/`06`, но не построено в v1.** Дизайн-канон описывает эти фичи, код их пока не реализует (каждое место помечено «не в v1» по месту в `05_UI_REQUIREMENTS.md` / `06_UI_DESIGN.md`):
  - **Движок таблиц `@tanstack/react-table`** (`06` §2, §6) — таблицы собраны вручную на shadcn `Table`; без него отсутствуют производные UX: **меню видимости колонок**, **density-toggle** (компактно/комфортно в localStorage), **сериализация фильтров в URL** (шаринг отфильтрованного вида ссылкой).
  - **Графики дашборда на `recharts`** (`06` §2) — библиотека подключена на странице «Отчёты» (`/reports`, `MLC-050`), но **дашборд** остаётся на карточках/прогресс-барах; перевод его метрик на графики — опция на будущее.
  - **ESLint-правило, форсящее `StatusBadge`** (`06` §3) — пока поддерживается ревью, не линтером.
  - Это пробелы «код отстаёт от спеки», а не дефекты дизайна. Любой пункт куратор может промоутнуть в `PROJECT_BACKLOG.md` как трекаемую задачу `MLC-NNN`.

## Permanently out of scope (ADR-15)

**Scheduled** backup orchestration / any restore capability, and in-app 2FA (the operator-initiated
on-demand `COPY_ONLY` backup button is the narrow exception carved out by ADR-27). Re-introducing
either requires explicitly revoking ADR-15 first.

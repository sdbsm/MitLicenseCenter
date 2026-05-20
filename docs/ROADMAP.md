# Roadmap

Этот файл — единственный stage-level индекс проекта. **Дополняет, не заменяет:**

- `docs/01..06_*.md` — спека финального продукта (что должно быть в итоге, не порядок).
- `docs/DECISIONS.md` — 14 ADR + operational constraints (архитектурные «почему», не «когда»).
- `memory/decisions.md` — детальные per-PR решения (binding facts уровня имён классов / SHA).

Цель: за 30 секунд понять, где мы сейчас и что осталось.

---

## Stage 1 — Foundation / scaffold ✅ (2026-05-18)

**Main HEAD после стадии:** `e261d11` (+ CI/docs follow-ups до `b6d0449`).

Каркас без бизнес-логики: backend `.NET 10` solution (Domain / Application / Infrastructure / Web + Tests.Unit), EF Core 10 + ASP.NET Identity (схема `auth`), Hangfire dashboard, Asp.Versioning URL `/api/v1/`, Swashbuckle `/api/docs`, Data Protection. Frontend Vite 8 + React 19 + Tailwind 4 + shadcn-tokens (в CSS, ещё без shadcn-CLI), react-router 7, TanStack Query 5, react-hook-form + Zod, react-i18next + `ru.json`. Endpoints: `POST/GET /api/v1/auth/login|logout|me`, `GET /api/v1/health`. Pre-commit hook ручной (не `husky` npm-пакет), `.slnx` solution, GitHub Actions CI с раздельными backend (windows-latest) + frontend (ubuntu-latest) jobs.

PR'ов нет — заехало в main одним коммитом.

---

## Stage 2 — Domain layer + UI shell ✅ (2026-05-18..19)

**Main HEAD после стадии:** `8adc931` (PR #4).
**Plan-файл:** `~/.claude/plans/wise-squishing-reddy.md`.

Полные CRUD для Tenant / Infobase / Publication + AuditLog writer + audit UI + sessions snapshot stub-endpoint + layout shell + self-service смена пароля. Без интеграций с реальной 1С/IIS.

| PR | Commit | Что |
|---|---|---|
| #1 | `c45c2f4` | PR 2.1 — layout shell (`AppShell` + `Sidebar` 3-group + `Topbar`), shadcn init (16 ui-компонентов), self-service `POST /api/v1/auth/change-password` |
| #2 | `446659c` | PR 2.2 — Tenants vertical, `IAuditLogger`, AuditLog enum refactor (`ActionType` string→int), 409 ProblemDetails с machine-readable `code`, `JsonStringEnumConverter` глобально |
| #3 | `1553248` | PR 2.3 — Infobases + Publications vertical (aggregate, единый POST/PUT/DELETE на `/infobases`), tenant deletion guard активирован, FK Infobase→Tenant Restrict + Publication→Infobase Cascade |
| #4 | `8adc931` | PR 2.4 — Audit UI с server-side pagination + URL-state, Sessions snapshot stub-endpoint (контракт `SessionSnapshotEntry` зафиксирован под Stage 3) |

---

## Maintenance / out-of-stage

| PR | Commit | Что |
|---|---|---|
| #5 | `b18ce73` | `.husky/pre-commit` — hardening: `cygpath -u "$LOCALAPPDATA"` portability, PATH augmentation для pnpm/winget locations, hard-fail если `pnpm` или `dotnet` не найдены (закрыло silent-skip frontend-проверок из PR 2.4) |

---

## Stage 3 — Infrastructure adapters ✅ (2026-05-19)

**Main HEAD после стадии:** `46ed377` (merge PR #10).

Реальная инфра: 1С Cluster REST adapter с Polly v8 circuit breaker, IIS XML-patcher через `Microsoft.Web.Administration`, Hangfire cold reconciliation + hot-tier BackgroundService + kill enforcer, sessions live polling 15s, drift detection (`*/5 * * * *`) + on-demand reconcile, Settings entity с DPAPI-зашифрованными секретами.

| PR | Commit | Что |
|---|---|---|
| #6 | `ede3520` | PR 3.1 — Settings entity (`dbo.Settings`: plain в `ValueText`, secrets в `Value` через DPAPI purpose `mlc.settings.v1`), `SettingDefinitions` catalog (14 ключей Stage 3), `ISettingsSnapshot` TTL 30s, страница «Параметры», audit `SettingChanged=400` без plaintext в description |
| #7 | `5e90805` | PR 3.2 — `OneCRestClusterClient` (REST `/rm/cluster/{id}/session`, idempotent kill, `license.present` как `ConsumesLicense`), `ClusterCircuitState` (Polly v8, 3 fails → open, 60s probe, audit транзиций `ClusterAdapterCircuit*`), `GET /api/v1/cluster/status` |
| #8 | `04896d6` | PR 3.3 — `IActiveSessionSnapshotStore` (singleton, immutable-swap, денормализованные TenantName/InfobaseName), cold reconcile (Hangfire `* * * * *` + throttle до `Cold.IntervalSeconds`) + hot tier `HotTierPollingService` (BackgroundService 3–5s), `KillEnforcer` (newest-first, cap 20/cycle, re-fetch verify, `SessionKilled=200` + `AuditReason.LimitExceeded`), `POST /sessions/{id}/kill` (Admin manual, `AuditReason.ManualByAdmin`) |
| #9 | `731872a` | PR 3.4 — Sessions Monitor UI (`refetchInterval: 15s` enabled, `<RelativeTime>` + `<StatusBadge>` материализованы как reusable в `components/ui/`, URL-state `?q=&infobaseId=`, kill-AlertDialog с retype `appId`), Admin-gate скрывает Action-column для Viewer |
| #10 | `5cbb01c` | PR 3.5 — Publication drift (`Stage3PublicationDrift` миграция: `LastDriftStatus`/`LastDriftCheckAt`/`LastDriftDetails`), `PublicationDriftStatus` enum `{InSync=0, Drift=1, Missing=2, Error=3}`, `DriftCheckJob` (`*/5 * * * *` + on-demand, throttle до `Settings.Drift.IntervalMinutes`), IIS adapter (`OneCIisPublishingService` через `ServerManager` + `XDocument` surgical-patch + atomic `File.Replace`, `VrdCustomXml` merge replace-child-by-LocalName / append-if-missing), endpoints `POST /check-drift` (202 + correlationId), `GET /drift-status`, `POST /reconcile` (sync, IIS exception → 409 `IIS_RECONCILE_FAILED`/`IIS_ACCESS_DENIED`) |

---

## Stage 4 — Planned (scope TBD)

Формально не открыта; ниже — накопленные переносы из Stage 2/3 (по ним вести plan-mode сессию когда подходит время):

- **Backup orchestration** (ADR-9) — full / diff / log Hangfire jobs, verification restore, backup-keys в `SettingDefinitions` catalog (PR 3.1 явно отложил), Dashboard last-backup-status badge.
- **Admin management UI** — создание / сброс пароля других admin'ов, 2FA toggle. ADR-7 предусмотрел, Stage 2/3 не делали; сейчас только seed-admin из миграции.
- **Полнофункциональный Dashboard** — счётчики Tenants / Active Sessions / Consumed-vs-Limit per-tenant, cluster circuit badge (PR 3.2 даёт `/cluster/status`), last-backup-status. PR 2.1 оставил Dashboard пустым.
- **«Публикации» как отдельная list-страница** — список всех Publications с drift-status badge + Reconcile-кнопкой. Stage 2 PR 2.3 управлял Publication внутри карточки Infobase; sidebar-пункт всё ещё `ComingSoonPage`.
- **`<DatePicker>` polished** — PR 2.4 положил `<input type="date">` как temp в фильтрах Audit.
- **Per-publication override `IIS.DefaultVrdRoot`** — PR 3.5 оставил один глобальный из `Settings`.
- **(опционально)** Deployment automation `Deploy-MitLicenseCenter.ps1` — Tooling Constraints явно отложили на «deployment-readiness phase».

Открытие Stage 4 — отдельная plan-mode сессия; scope формировать с этого списка.

---

## Как обновлять этот файл (binding)

- **Стадия закрывается** (последний sub-PR merged в main) → добавить «Main HEAD после стадии» + сводную таблицу PR'ов в том же turn'е, в котором merged последний sub-PR.
- **Maintenance-PR вне стадии** → добавить в раздел «Maintenance / out-of-stage».
- **Stage N открывается plan-mode сессией** → создать раздел стадии с пометкой ⏳ (in progress) и перенести соответствующие пункты из «Planned» в `Stage N`.
- Per-PR детали (имена классов, SHA, отклонения от плана, code-уровень) живут в `memory/decisions.md`. ROADMAP содержит только stage-level срез.

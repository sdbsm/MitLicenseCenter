# MitLicense Center — Roadmap

This is the stage-level index. Memory's `MEMORY.md` is the dense decision-oriented mirror; `DECISIONS.md` holds the locked ADRs; the per-stage planning chats live in `~/.claude/plans/`. This file just tracks **where we are**, what's closed, and what's queued.

## Stage 1 — Tooling baseline · **Done**

Repo layout (`backend/` .NET solution, `frontend/` Vite/React, `docs/`, `scripts/`), .NET 10 LTS + EF Core 10, pnpm 11 + Node 24, Vite 8 + React 19, Tailwind 4, `.slnx` solution format, manual `.husky/pre-commit` (no husky npm package), UTF-8 BOM convention for `scripts/*.ps1`. Closed via the Stage 1 PR chain. See `memory/tooling.md` for pinned versions.

## Stage 2 — Domain foundation · **Done**

| PR | Scope |
|---|---|
| 2.1 | shadcn/ui shell + AppShell layout + 3-group sidebar + self-service `/auth/change-password` |
| 2.2 | Tenants vertical + `IAuditLogger` + 409 ProblemDetails with machine-readable `code` extension + `JsonStringEnumConverter` |
| 2.3 | Infobases + Publications aggregate (single POST/PUT/DELETE), FK + uniqueness contracts, Tenant deletion guard |
| 2.4 | Audit page with server-side pagination + filters; `GET /sessions/snapshot` contract stub (Stage 3 wires the real adapter) |

Five locked ADRs (ADR-1 frontend, ADR-2 1C REST, ADR-7 Identity auth, ADR-10 versioning, ADR-11 shadcn) + operational constraints all bind at end of Stage 2.

## Stage 3 — Live infrastructure integration · **Done**

The stage that turned a domain CRUD app into an actual control plane.

| PR | Scope | New ADR(s) |
|---|---|---|
| 3.1 | `dbo.Settings` table + DPAPI secret payloads + «Параметры» Admin UI + `ISettingsSnapshot` (TTL ≈ 30s) | — |
| 3.2 | `OneCRestClusterClient` (typed HttpClient) + `ClusterCircuitState` (Polly v8) + `ResilientClusterClient` decorator + `IRasFallbackClusterClient` marker + `/cluster/status` | **ADR-3.1** REST endpoint contract, **ADR-3.2** Polly circuit-breaker config |
| 3.3 | `IActiveSessionSnapshotStore` (singleton, immutable-swap) + `HotTierPollingService` (BackgroundService, not Hangfire) + cold `ReconciliationJob` (Hangfire 1-min CRON + sub-minute throttle) + `KillEnforcer` (cap 20/cycle, newest-first, re-fetch verification) + `POST /sessions/{id}/kill` | **ADR-6.1** Hot-tier in BackgroundService, **ADR-6.2** canonical `SessionKilled=200` + `AuditReason` differentiator |
| 3.4 | Sessions Monitor UI vertical — `<RelativeTime>` + `<StatusBadge>` as reusable `components/ui/`, URL-state filters, `appId` retype kill token, Admin column gate | — |
| 3.5 | `Stage3PublicationDrift` migration + `VrdPatcher` (pure static) + `OneCIisPublishingService` (real IIS + `XDocument` + atomic `File.Replace`) + `DriftCheckJob` (5-min Hangfire) + check-drift / drift-status / reconcile endpoints | **ADR-4.1** VRD surgical-patch + VrdCustomXml merge strategy |
| 3.6 | Publications UI vertical — flatten `/api/v1/infobases?pageSize=200` to `PublicationListItem`, drift badge mapping per ui_design.md canon, optimistic poll via imperative `setInterval` inside click-handler, reconcile retype + Admin gate | — |
| 3.7 | `GET /dashboard/summary` (Viewer, 5s `IMemoryCache`) + cluster status card + top-5 tenants by consumption + Dashboard real KPIs | — |
| 3.8 | `RacExecutableRasClusterClient` (real `rac.exe` wrapper, replaces `StubRasClusterClient`) + `RacOutputParser` (pure static, defensive) + `IRacProcessRunner` abstraction + Stub moved to `Clusters/Testing/` | **ADR-3.3** rac.exe CLI contract |

### Stage 3 retrospective (what held vs what deviated)

**Held verbatim from the plan:**
- Canonical `SessionKilled=200` + `AuditReason` (rejected separate `SessionKilledManual=201`).
- Hot-tier as `BackgroundService` (not Hangfire — CRON minimum is 1min).
- VRD surgical-patch only (never `webinst` on existing publications); `replace-child-by-LocalName / append-if-missing` overlay strategy → operator's custom XML never lost on reconcile.
- Idempotent kill protocol: re-fetch + `(InfobaseId, AppId, StartedAt)` verification + 404/AlreadyGone tolerance.
- Drift audit semantics: 210 only on transition to `{Drift,Missing,Error}`; reconcile writes 211, not the drift job.
- Settings catalog frozen at the 14 keys defined in PR 3.1 (no Stage 3 PR ever added a 15th).

**Justified deviations from the plan-file:**
- **PR 3.2**: `RemoveAllResilienceHandlers()` on `OneCRestClusterClient`'s `HttpClient` — global `AddStandardResilienceHandler()` from `Program.cs` was double-wrapping the circuit-breaker. Plan didn't anticipate the global handler.
- **PR 3.6**: Drift status colour mapping `Missing → info / Drift → danger` per `docs/06_UI_DESIGN.md` canon (ADR-11), not the plan-file's `Missing → danger / Drift → warning`. Ui_design.md wins.
- **PR 3.6**: Optimistic poll implemented as imperative `setInterval` inside click-handler (with `useRef` handle + unmount cleanup), NOT React-effect-driven `refetchInterval`. Sidesteps React-19's `react-hooks/set-state-in-effect` lint without `eslint-disable`.
- **PR 3.7**: `IMemoryCache` registered in `Program.cs` (Web slice), not `Infrastructure/DependencyInjection.cs`. Cache lives where the response is built.
- **PR 3.8**: Plan called the new ADR "ADR-3.2" but that slot was already used by PR 3.2's circuit-breaker config → renumbered to **ADR-3.3**. Plan also documented an `--auth=u:p` rac.exe flag that doesn't exist, a `--result-format=tab` flag that doesn't exist, and a default `rac.exe` path that doesn't exist on 1C 8.5 — all corrected via live experiment captured in ADR-3.3.

**Acknowledged debts carried into Stage 4:**
- No frontend test framework (Vitest deferred — listed under Open items in the Stage 3 plan as accepted risk).
- `rac.exe`-per-cycle spawning works but burns ~26 processes/min under sustained over-quota load. Strategy B (long-lived TCP socket on 1545 → no per-cycle spawn) is the obvious next optimisation.
- ~~Per-publication physical-path override deferred~~ → **Closed by Stage 4 PR 4.1.** `PhysicalPathOverride` column on `Publication` + `VrdPathResolver` static helper; override-first, convention fallback. Section C of Stage 3 operator verification now unblocked.
- Multi-cluster topology — `OneCRestClusterClient` and `RacExecutableRasClusterClient` both assume single-cluster, single-node deployment. ADR-3.1 documents this.

## Stage 4 — Operational hardening · **Done** (closed 2026-05-23)

Narrative: "Operational hardening release." Scope was pivoted and locked 2026-05-22 — backup orchestration (ADR-9) and TOTP 2FA permanently revoked as out-of-app scope (see ADR-15). Each sub-PR has its own deep-dive plan file in `~/.claude/plans/`.

| PR | Scope | Status | ADR(s) |
|---|---|---|---|
| 4.1 | Per-publication physical-path override — `PhysicalPathOverride` column on `Publication`, `VrdPathResolver` static helper, override-first + convention fallback, real-time placeholder in `InfobaseFormDialog`. Closes PR 3.6 Section C verification gap. | **Done** | ADR-4.1 updated |
| 4.2 | Frontend Vitest + React Testing Library setup — Vitest 4 + jsdom 27 + RTL 16 + jest-dom 6 + user-event 14; `pnpm test`/`pnpm test:watch`; 4 specimen tests (`urlState`, `StatusBadge`, `RelativeTime`, `useDashboardSummary`); ESLint test-globals override; CI `Test` step after `Build`. Foundation only — exhaustive coverage becomes per-PR responsibility starting here. | **Done** | — |
| 4.3 | Audit retention policy — `Audit.RetentionDays` Settings key (15th key in catalog, default 365, [30, 3650]); `AuditRetentionJob` (daily 03:00 UTC CRON, batched `DELETE TOP (5000)` с commit-per-batch via `ExecuteSqlInterpolatedAsync`); `AuditLogsPurged=500` opens 500-серию (system maintenance); `GET /api/v1/audit/retention` Viewer-readable; banner на `/audit` через vendored shadcn `<Alert>` + `useAuditRetention` hook (staleTime 5min); pure helper `isFilterBeyondRetention` с 8 it-блоками Vitest (PR 4.2 foundation в действии). | **Done** | — |
| 4.4 | ADR revocations + docs/memory cleanup — ADR-9 (backup) formally revoked, ADR-7 TOTP mention removed, ADR-15 (backup/2FA responsibility boundary) locked, MEMORY.md + memory/ consistency pass. | **Done** | ADR-9 revoked, ADR-15 new, ADR-7 updated |

**Notable scope pivots during execution:**
- **PR 4.3**: roadmap planning called for a `Stage4AuditRetentionIndex` migration to add a covering index on `dbo.AuditLogs.[Timestamp]`. Reality check — `IX_AuditLogs_Timestamp` (single-column) already exists in `InitialCreate` (line 217-221) and is sufficient for `DELETE WHERE Timestamp < @cutoff` (no SELECT projection, included columns add nothing). Migration was skipped entirely — no schema changes in PR 4.3. The roadmap line about Dashboard health-card surfacing was also dropped: retention surfaces as a banner on `/audit` (operator-visible where it matters) and a single AuditLogs row per purge cycle; cluttering Dashboard with another KPI would diminish the existing 6 cards. Backend unit-test for `AuditRetentionJob` was deferred (in-memory EF provider doesn't support `ExecuteSqlInterpolatedAsync`; full Testcontainers-MSSQL test is out-of-scope foundation PR'а — verification done via 8 Vitest assertions on pure helper + live preview smoke).

**Revoked from original Stage 4 scope:**
- ~~Backup orchestration (ADR-9)~~ — out of application scope permanently. Operator uses SQL Maintenance Plans / Veeam / Windows Server Backup. New ADR-15 documents the responsibility boundary.
- ~~TOTP 2FA~~ — internal LAN/VPN deployment; network-level auth protection (firewall + AD/SSO + physical access) is sufficient. `TwoFactorEnabled` column stays (Identity base class) but is never activated. ADR-7 updated accordingly.

**Deferred beyond Stage 4:**
- RAS long-lived TCP socket (Strategy B) — `rac.exe`-per-cycle budget (~26 processes/min) is within OS/AV tolerances on single-node; re-evaluate after real-world production latency measurement.
- Multi-cluster / multi-node topology — single-node assumption locked; opening it requires re-review of every adapter.

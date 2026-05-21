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
- Per-publication physical-path override deferred (PR 3.6 verification gap — operator must align IIS physical path with `{IIS.DefaultVrdRoot}/{siteName}/{virtualPath}` convention or drift detection reports `Missing` on healthy publications).
- Multi-cluster topology — `OneCRestClusterClient` and `RacExecutableRasClusterClient` both assume single-cluster, single-node deployment. ADR-3.1 documents this.

## Stage 4 — Planned

Not started. Likely scope (subject to change once Stage 3 ships):

1. **Backup orchestration (ADR-9 activation).** Hangfire-driven `BACKUP DATABASE` / `BACKUP LOG`, full nightly + differential 6h + tx log 15min, weekly verify-restore to `MitLicenseCenter_RestoreTest`, daily companion-artifacts copy (DPAPI keys, `appsettings.Production.json`). Standalone `MitLicenseCenter.Restore.exe` CLI for DR.
2. **RAS long-lived TCP socket strategy.** Replace `rac.exe`-per-cycle with a persistent connection to `ras.exe` on 1545. Removes the per-cycle process spawn cost. Tradeoff: protocol re-implementation work (rac.exe is the 1C-supplied wire-protocol implementation). Decide after measuring real-world per-cycle latency in Stage 3 production deployment.
3. **Per-publication VRD path override.** Schema column on `Publication` for the absolute physical path of the IIS folder; resolver uses it when set, falls back to the `{IIS.DefaultVrdRoot}/{siteName}/{virtualPath}` convention otherwise. Closes the PR 3.6 verification gap for non-conventional layouts.
4. **Frontend test framework.** Vitest + React Testing Library — the Stage 3 frontend shipped without coverage as accepted risk; back-fill happens here with a tooling PR before adding more UI features.
5. **Audit retention.** Stage 3 writes audit rows indefinitely. Retention policy (e.g. roll over annually to a cold table) + Settings key for retention window. Already a frequent stakeholder ask in the plan margin.
6. **TOTP 2FA for admin accounts (off by default per ADR-7).**
7. **Multi-cluster / multi-node topology** if and when a deployment ever needs it. Currently locked at single-node — opening this means re-reviewing every adapter.

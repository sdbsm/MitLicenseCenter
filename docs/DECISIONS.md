# Architecture Decision Records (ADR)

Current, binding architectural decisions for MitLicense Center v1. Each ADR states the decision, the reason, and the key rejected alternative. ADR numbers are stable — code and other docs reference them by number.

## 1. Frontend Framework
- **Decision:** React (Single Page Application) with TypeScript.
- **Rejected:** Blazor WebAssembly, ASP.NET Core MVC, Vue.js.
- **Reason:** Strict API boundary between the frontend and the .NET infrastructure backend; the UI is fully decoupled from server logic.

## 2. 1C Cluster Integration Method
- **Decision:** Administer the 1C cluster exclusively through RAS (`ras.exe`, default TCP 1545) driven by the `rac.exe` CLI. This is the **sole** cluster adapter — see **ADR-16** for the consolidation rationale and **ADR-3.3** for the full CLI contract.
- **Rejected:** 1C Cluster REST API (not published by default on the target single-node 8.5 deployments — see ADR-16); COM-connection (`V83.COMConnector`, slow and leaky); direct database tampering.
- **Reason:** RAS via `rac.exe` answers reliably on every target deployment; no extra HTTP surface to expose or maintain.

## 3. License Enforcement Approach
- **Decision:** Snapshot-based background reconciliation loop (see ADR-6 for cadence).
- **Rejected:** Real-time connection interception / web-server hooks.
- **Reason:** The 1C platform offers no reliable native hook to reject a login based on external multi-tenant rules. The background worker keeps the cluster unblocked during login.

## 4. IIS Publication Updates (`default.vrd`)
- **Decision:** XML parsing and surgical modification of `default.vrd` (see ADR-4.1 for the patch/merge contract).
- **Rejected:** The `webinst` CLI on existing publications.
- **Reason:** `webinst` overwrites `default.vrd`, destroying custom OData / HTTP-services / OpenID config. Patching the XML touches only what must change and preserves customizations.

## 5. Architectural Style
- **Decision:** Modular Monolith (C# / .NET) with strict logical boundaries between modules.
- **Rejected:** Distributed microservices.
- **Reason:** The system runs tightly against a specific single-node Windows stack (IIS, 1C Server, MSSQL). Microservices add network and operational overhead for no benefit at this scale.

## 6. Reconciliation Loop Cadence (Two-Tier)
- **Decision:** Two-tier loop. **Hot** — tenants at ≥ 90% consumption polled every 3–5s. **Cold** — full snapshot of all tenants every 20–30s. A tenant exits the hot tier after two consecutive cold cycles below the threshold.
- **Rejected:** A single uniform 15–30s loop; per-second polling for everyone.
- **Reason:** A single 30s loop leaves a window for an over-quota user to log in and lose unsaved work when killed; a global 3–5s loop overloads the cluster as bases grow. The hot/cold split bounds the enforcement window to ≤ 5s for at-risk tenants while keeping baseline load low.

### ADR-6.1 — Hot-tier polling lives in a BackgroundService, not Hangfire
- **Decision:** Hot-tier polling runs as `HotTierPollingService : BackgroundService`. Cold reconciliation stays in Hangfire (`* * * * *` CRON with an internal throttle to `Polling.ColdIntervalSeconds`).
- **Reason:** Hangfire's CRON minimum is 1 minute; the hot tier needs a 3–5s cadence. Kill enforcement runs only at the end of each cold cycle — hot polling updates the UI snapshot but does not kill.

### ADR-6.2 — `SessionKilled=200` + `AuditReason` differentiator
- **Decision:** A single `AuditActionType.SessionKilled = 200` covers both automatic and manual kills; the distinction lives in `AuditReason` (`LimitExceeded = 1`, `ManualByAdmin = 2`).
- **Rejected:** A separate `SessionKilledManual = 201` value.
- **Reason:** `AuditReason` already provides the filtering; a second enum value doubles the surface for nothing. `LimitChanged = 201` is reserved for future tenant-limit-change audit.

## 7. Admin Authentication
- **Decision:** ASP.NET Core Identity with local accounts in MSSQL (schema `auth`); cookie auth (HttpOnly, Secure, SameSite=Strict). Two roles: `Admin` (full access incl. kill and reconcile) and `Viewer` (read-only). The first admin is seeded by migration with a random password written to the service log. Authentication is exactly username + password + cookie — **in-app 2FA is out of scope** (ADR-15); the secondary factor is delegated to the network edge.
- **Rejected:** Windows Integrated Auth (fragile for non-Windows-account staff); JWT bearer (cookie is simpler/safer for a same-origin SPA); external IdPs (violates "no external systems").
- **Reason:** Fully local, no external dependencies, native to .NET, shares the domain MSSQL. The Identity base-class column `AspNetUsers.TwoFactorEnabled` stays in the schema (removing it needs a custom user-class refactor) but is never read or written.

## 8. Secret Management
- **Decision:** ASP.NET Core Data Protection API (DPAPI-backed on Windows). Key ring under `%ProgramData%\MitLicenseCenter\keys\`, scoped to the service account. Secrets stored encrypted in `dbo.Settings` (`Value VARBINARY(MAX)`); protector purpose-string `mlc.settings.v1`. Dev uses `%LocalAppData%\...\keys\`.
- **Rejected:** HashiCorp Vault, Azure Key Vault (violate "no external systems"); plaintext config files.
- **Reason:** Built into .NET, machine/account-scoped, zero external dependencies. The operator backs up the key ring alongside the database — without it a restored backup is unreadable (ADR-15, `OPERATIONS.md`).

## 10. REST API Versioning
- **Decision:** URI versioning — all endpoints under `/api/v1/...` (`Asp.Versioning.Mvc`). Breaking changes introduce `/api/v2/...`. OpenAPI/Swagger UI at `/api/docs`; the TypeScript client is generated from the OpenAPI spec.
- **Rejected:** Header-only versioning (harder to discover/test); no versioning.
- **Reason:** URI versioning is the most discoverable and tooling-friendly; frontend and backend deploy together so complex negotiation is unnecessary.

## 11. UI Component Library
- **Decision:** shadcn/ui (copy-paste components owned in the repo) on top of Radix UI, styled with Tailwind CSS. Auxiliary: `lucide-react`, `@tanstack/react-table`, `react-hook-form` + `zod`, `sonner`, `recharts`, `date-fns`/`ru`. No other component libraries mixed in.
- **Rejected:** Material UI, Ant Design (heavier, opinionated, farther from the target look); a fully custom design system (overkill for a 5–20 user panel).
- **Reason:** Owned in-repo (no vendor lock-in), accessible Radix primitives, CSS-variable theming (light + dark). Visual language, status semantics, table patterns and Russian microcopy are codified in `06_UI_DESIGN.md`.

## 12. Backend Runtime = .NET 10 (LTS)
- **Decision:** Target `net10.0` (LTS until Nov 2028).
- **Rejected:** `net8.0` (less runway); `net9.0` (STS, short window); .NET Framework 4.8 (legacy).
- **Reason:** Maximum LTS runway, latest C# 14 / EF Core 10 / ASP.NET Core 10; all planned libraries are compatible.

## 13. Repository Layout = Monorepo
- **Decision:** Single private GitHub repo — `backend/` (.NET solution), `frontend/` (Vite/React/TS), `docs/`, `scripts/`, `.github/`.
- **Rejected:** Separate frontend/backend repos.
- **Reason:** Atomic cross-cutting commits, single CI pipeline, one source of truth. The OpenAPI-generated TS client lives next to the API that produces it.

## 14. CI/CD = GitHub Actions, CI Only (No CD in v1)
- **Decision:** GitHub Actions on every push and PR to `main`. Backend: `restore → build → test`. Frontend: `install → lint → type-check → test → build`. PRs are blocked while either job is red. No deployment automation — deploy is manual via `scripts/Deploy-MitLicenseCenter.ps1`.
- **Rejected:** No CI (unacceptable for a system that auto-kills sessions); full CD on tag (premature); self-hosted runners / Jenkins / Azure DevOps (extra infra).
- **Reason:** Minimum viable safety net; CD waits until the deployment story stabilizes.

## 15. Backup and Two-Factor Authentication Scope Boundary
- **Decision:** Backup orchestration and in-app 2FA are permanently **out of scope**.
  - **Backup** is the operator's responsibility via platform tooling (SQL Maintenance Plans, Veeam, Windows Server Backup, Robocopy + Task Scheduler). The app never invokes `BACKUP DATABASE`, never schedules backup jobs, never ships a restore CLI, never surfaces backup status. The backup unit = MSSQL database + Data Protection key ring (+ optional `appsettings.Production.json`); restoring one without the other yields an unreadable database. See `OPERATIONS.md`.
  - **2FA** is a network-level concern — LAN/VPN access, perimeter firewall, AD/SSO at the edge, physical access control. `AspNetUsers.TwoFactorEnabled` stays in the schema but is operationally inert.
- **Rejected:** Hangfire-orchestrated backups (doubles the failure surface; backup is already solved at the platform layer); in-app TOTP/WebAuthn/SMS (duplicates or weakens the network-edge protection every deployment already has).
- **Reason:** Scope discipline — the app's job is 1C session licensing. **Locked:** re-introducing either concern requires explicitly revoking ADR-15 first.

## 16. RAS as Sole 1C Cluster Adapter
- **Decision:** The 1C cluster adapter layer is exclusively `rac.exe`. Operator-tunable inputs: `OneC.RAS.Endpoint` (default `localhost:1545`) and `OneC.RAS.ExePath` (no default — see ADR-3.3). `OneC.Cluster.AdminUser` / `OneC.Cluster.AdminPassword` feed `rac.exe`'s `--cluster-user` / `--cluster-pwd` flags (both may be empty for clusters with no registered administrators).
- **Dashboard surface:** the cluster card is a **RAS health card** backed by `RasHealthProbingService : BackgroundService` (30s `PingAsync`) writing to `IRasHealthReader`. Three states: `OK`, `Сбой` (danger, last-error + consecutive-failures), `Проверка…` (neutral, first 30s after startup). Health transitions are not audited. `AuditActionType` `300`/`301` (`ClusterAdapterCircuit{Opened,Closed}`) remain reserved historical so old rows render.
- **Rejected:** Keep REST primary with RAS fallback (REST is empirically absent on target deployments — `HttpClient` hangs the full 100s timeout before the breaker trips); RAS-only behind a feature flag (same dual-path maintenance cost); repurpose the Polly breaker to wrap RAS (degenerates to a retry loop); drop the Dashboard card entirely (loses a cheap, meaningful health signal).
- **Reason:** Reality discipline — REST is not in the deployment environment, and the dual-adapter codebase leaked failure modes operators couldn't self-diagnose. **Locked:** re-introducing any non-`rac.exe` cluster adapter requires explicitly revoking ADR-16 first.

## 17. Add-Infobase Form Defaults (UI-only, not field globalization)
- **Decision:** The «Добавить инфобазу» form is simplified to three always-visible per-base fields (tenant, cluster infobase picked by name, SQL database); all deployment-uniform fields move into a collapsed «Дополнительно» disclosure. The repeated values (SQL server, IIS site, platform version) get **form-prefill settings** — `Defaults.DatabaseServer`, `IIS.DefaultSiteName`, `OneC.DefaultPlatformVersion` — set once in «Параметры» and pre-filled into each new base.
- **Scope boundary:** these three keys are **UI-only** — no backend service reads them. `Infobase`/`Publication` keep storing the values per-base; the form merely pre-fills and the operator may override per-base in «Дополнительно». Picking the cluster infobase auto-fills the display name (no second "name" field on the main form); the virtual path defaults to `"/" + slug(databaseName)`.
- **Rejected:** globalizing the fields (removing `DatabaseServer`/`SiteName`/`PlatformVersion` from the entities and resolving them from settings at runtime) — it forbids any base differing from the rest and forces a domain-model migration, RAS/publication-resolver rewrites, and doc-canon churn for a pure UX problem.
- **Reason:** Simplicity for the sysadmin operator without sacrificing per-base flexibility. **Locked:** the form-prefill keys must not grow backend readers — promoting one to a runtime-resolved global requires revoking ADR-17 first.

## 18. Fail-fast Bootstrap
- **Decision:** Database migrations and seeding run **synchronously in the startup pipeline, before `app.Run()` opens the listener** — not in a fire-and-forget `Task.Run` from `ApplicationStarted`. Order is fixed: EF Core migrations + roles/first-admin (`IdentitySeeder`) → settings catalog (`SettingsSeeder`, which requires the `dbo.Settings` table the migration creates). Any failure logs `Critical` and lets the exception propagate out of `Main`, so the process exits non-zero **without ever accepting a request**. `IdentitySeeder` skips `MigrateAsync` on a non-relational provider (`Database.IsRelational()` is false) so `WebApplicationFactory<Program>` integration tests on the EF in-memory provider boot without a relational migration call.
- **Rejected:** The previous `_ = Task.Run(async () => { … throw; })` inside `ApplicationStarted` — the `throw` was **unobserved**, so a failed migration or seed left the host listening in a half-initialized state (no admin account, or an unmigrated database) with no operator-visible signal. Calling `IHostApplicationLifetime.StopApplication()` after traffic is already accepted was also rejected as strictly weaker than never opening the port.
- **Reason:** Operability — a running, listening process must be a fully migrated and seeded one (see `OPERATIONS.md`). The first admin is still seeded with a random password written to the log (ADR-7), now guaranteed before the first request can arrive. **Locked:** initialization that must complete before serving traffic belongs in the synchronous pre-`Run` block, not a detached task.

### ADR-3.3 — 1C RAS `rac.exe` CLI contract
The authoritative `rac.exe` integration contract. `RacExecutableRasClusterClient` (in `Infrastructure/Clusters/`) binds to it; `RacOutputParser` and `RacOutputParserTests` reference this ADR by number.

- **Tool path.** Operator-configurable via `Settings.OneC.RAS.ExePath`. No seeded default — on 1C 8.5 `rac.exe` lives in the version-specific `C:\Program Files\1cv8\<version>\bin\` (not `1cv8\common\`), so the operator supplies the path in the «Параметры» UI.
- **RAS endpoint.** `Settings.OneC.RAS.Endpoint` (default `localhost:1545`), passed as the **first positional argument** (`rac.exe <host:port> <mode> <command> ...`) so a misconfigured endpoint surfaces as a connection error rather than silently hitting localhost.
- **Authentication.** `--cluster-user=<name>` `--cluster-pwd=<password>` from the cluster-admin settings (two separate flags — `--auth=u:p` does not exist). When both are empty, the flags are omitted (`rac` allows anonymous administration of clusters with no registered administrators).
- **Encoding.** `rac.exe` writes stdout/stderr in the **parent process's active OEM code page** (CP866 on RU Windows), not UTF-8. `SystemProcessRacRunner` reads `StandardError.BaseStream` / `StandardOutput` raw and decodes via `Encoding.GetEncoding(CultureInfo.CurrentCulture.TextInfo.OEMCodePage)` (registered through `CodePagesEncodingProvider.Instance`; falls back to UTF-8 where `OEMCodePage = 0`). `ProcessStartInfo.Standard*Encoding` is **not** set — setting it does not flip the child's output, it only mis-decodes bytes already on the pipe.
- **Output format.** Vertical key-value blocks (no tabular `--result-format`): `kebab-key                 : value`, CRLF line terminators, records separated by one or more blank lines. String values are unquoted (no whitespace) or double-quoted (with whitespace); empty values are an empty string after the colon-space.
- **Parser (`RacOutputParser`, pure static).** Defensive regex `^\s*(?<key>[a-z0-9-]+)\s*:\s?(?<value>.*?)\s*$`. Records split on consecutive blank lines. Unknown keys ignored; records missing a required key (`session`/`infobase` for sessions, `cluster` for clusters) dropped + logged at Debug. Empty stdout → empty list. **Never throws** — a malformed line is a Debug-level skip.
- **Commands.**

  | Adapter method | Invocation |
  |---|---|
  | `PingAsync` | `rac.exe <ep> cluster list` — exit 0 + ≥1 cluster → `Ok=true`; non-zero → `Ok=false`, `Error=stderr`. |
  | `ListActiveSessionsAsync` | `rac.exe <ep> session list --cluster=<uuid> [--cluster-user=<u> --cluster-pwd=<p>]` — `<uuid>` resolved from a prior `cluster list` in the same call (cached for that call only). |
  | `KillSessionAsync` | `rac.exe <ep> session terminate --cluster=<uuid> --session=<sid> [auth] --error-message="<reason>"` — the command is `session terminate` (not `session kill`); `--error-message` is shown to the kicked user. |

- **Session field mapping (`rac` → `ClusterSession`).** `session → SessionId` (kill target), `infobase → ClusterInfobaseId`, `app-id → AppId`, `user-name → UserName`, `host → Host`, `started-at → StartedAtUtc` (ISO `YYYY-MM-DDTHH:MM:SS`, no offset; parsed `AssumeUniversal | AdjustToUniversal`). `ConsumesLicense` is **derived** via the app-id heuristic `{1CV8, 1CV8C, WebClient, Designer, COMConnection} → true` (the default `session list` output has no license field).
- **Error semantics (all failures exit `255`).** Unreachable RAS / unknown cluster UUID → empty list + Warning (`ListActiveSessionsAsync`) or `Ok=false` (`PingAsync`). `session terminate` with stderr containing `«Сеанс с указанным идентификатором не найден»` → `Killed=false, AlreadyGone=true` (idempotent, matches a 404). Malformed UUID arg → `Killed=false, AlreadyGone=false` + Error.
- **Process lifecycle.** `System.Diagnostics.Process`, `UseShellExecute=false`, stdout/stderr redirected. A fixed **30s** per-invocation deadline; `CancellationToken.Register(() => process.Kill(entireProcessTree: true))` — `entireProcessTree` because `rac` spawns a transient child for the gRPC dialog with RAS. Process work is hidden behind `IRacProcessRunner` → `SystemProcessRacRunner` so the adapter is unit-testable with a fake runner.
- **Spawn cadence (binding).** ≤ 2 `rac.exe` per `ListActiveSessionsAsync` (one `cluster list` + one `session list`) and ≤ 1 per kill; cap 20 kills/cycle → worst case ~26 procs/min under sustained over-quota load. The long-lived-socket optimization (Strategy B) is a backlog item (`ROADMAP.md`); until then this budget is the contract.

### ADR-4.1 — VRD surgical-patch and `VrdCustomXml` merge strategy
The authoritative `default.vrd` mutation contract. `VrdPatcher` references this ADR by number; `OneCIisPublishingService.ApplyDesiredStateAsync` implements it.

- **In-place only.** The patch is the only allowed mutation on an existing `default.vrd` — `webinst` is never invoked, the file is never overwritten wholesale.
- **Path resolution** via `VrdPathResolver.Resolve`: override-first (`PhysicalPathOverride`) then convention `{IIS.DefaultVrdRoot}/{siteName}/{trimmedVirtualPath}/default.vrd` (see `04_INFRASTRUCTURE.md`).
- **Three surgical mutations (`VrdPatcher.Patch`):** (1) `<standardOdata enable="…">` toggle (node created with only `enable` if missing); (2) `<httpServices publishByDefault="…">` toggle; (3) any attribute value containing `wsisapi.dll` has its `\d+\.\d+\.\d+\.\d+` segment replaced with `publication.PlatformVersion` (no-op if absent on newer builds that move the ISAPI handler to `web.config`).
- **`VrdCustomXml` overlay** = **replace-child-by-LocalName / append-if-missing**: each overlay child matched against VRD-root children by `XName.LocalName` (namespace-agnostic); a matching sibling is replaced verbatim (operator tuning wins), a non-matching one is appended. Existing custom nodes not present in the overlay (`<openid>`, …) are **never** dropped — this is the cornerstone guarantee. A malformed `VrdCustomXml` surfaces as `InvalidOperationException` → reconcile endpoint maps it to `409` `IIS_RECONCILE_FAILED` (no half-applied write).
- **Idempotency & atomic write.** `Patch(x) == Patch(Patch(x))`; the file is written only when content changed (preserving mtime), to `default.vrd.mlc.tmp` first, then `File.Replace`.
- **Drift audit semantics.** `DriftCheckJob` writes `PublicationDriftDetected = 210` **only** on a status transition **and** only when the new status ∈ `{Drift, Missing, Error}`. The `Drift → InSync` transition from a successful reconcile is audited by the reconcile endpoint as `PublicationReconciled = 211` — the job never writes `211`, the endpoint never writes `210`.
- **Test strategy.** `VrdPatcher` and `PublicationDriftDetector` are pure static helpers (unit-tested without `ServerManager` or a filesystem). `OneCIisPublishingService` is `[SupportedOSPlatform("windows")]` and validated by smoke tests against a real publication.

## Locked Operational Constraints (binding)
- **Kill priority:** when `Consumed > Limit`, kill sessions ordered by `StartedAt DESC` (newest first) until `Consumed == Limit`. Simplest to explain to users and avoids interrupting established sessions.
- **Idempotent kill protocol:** before a kill, re-fetch the session by ID and verify `(InfobaseId, AppID, StartedAt)` match the snapshot; mismatch → skip and wait for the next cycle; "session not found" → treated as a successful kill. Every kill writes an `AuditLog` entry with reason and snapshot context. **Audit on real outcome only:** the `AuditLog` row is written **iff** `KillSessionResult` is `Killed` **or** `AlreadyGone` (the idempotent "already terminated" success). When RAS is unreachable / `rac.exe` errors (both flags `false`) **nothing is killed and nothing is audited** — the immutable log must never record a kill that did not happen. Both paths obey this: the automatic `KillEnforcer` (reason `LimitExceeded`, initiator `System`) logs-and-skips the failed session for the next cycle, and the manual `POST /api/v1/sessions/{id}/kill` (reason `ManualByAdmin`, initiator = admin) returns an error to the operator instead of a false `204` — `409 SESSION_STALE` on descriptor mismatch (refresh and retry), `502 CLUSTER_UNAVAILABLE` when RAS is down. `404` still means the session was not in the current snapshot at all.
- **Drift detection:** a Hangfire job runs every 5 minutes (throttled to `Drift.IntervalMinutes`); `POST /api/v1/publications/{id}/check-drift` is available on demand. **Drift is never auto-corrected** — reconcile is an explicit, audited admin action.
- **Deployment topology:** single-node — backend, IIS, MSSQL and the 1C cluster all on the same Windows Server host. No remoting, no WinRM, no cross-host adapters. Changing this assumption requires re-reviewing every infrastructure adapter.

## Tooling Constraints (binding alongside ADR-13/14)
- **.NET solution format:** `.slnx`. CI and scripts reference `backend/MitLicenseCenter.slnx`.
- **Frontend package manager:** `pnpm` (pinned via `packageManager` in `frontend/package.json`). Supported install path on Windows is the standalone winget package (`winget install pnpm.pnpm`) — Corepack works on Linux/macOS or an elevated Windows shell but can't write to `C:\Program Files\nodejs\` for a non-admin user.
- **Node.js minimum:** Node 22.13+ (pnpm 11 fails on older with `No such built-in module: node:sqlite`).
- **Pre-commit hooks:** a custom `.husky/pre-commit` registered via `git config core.hooksPath .husky` (set idempotently by `frontend/scripts/install-git-hooks.mjs`). The `husky` npm package is intentionally not installed. The hook runs `lint-staged` on staged JS/TS/JSON/CSS and `dotnet format --verify-no-changes` on staged `.cs`; requires Git Bash on Windows.
- **PowerShell script encoding:** all `scripts/*.ps1` are saved as **UTF-8 with BOM** — Windows PowerShell 5.1 reads BOM-less UTF-8 as cp1251 and corrupts Cyrillic.
- **shadcn on Windows:** add components via `scripts/shadcn-add.ps1` (workaround for `pnpm dlx shadcn` — uses `pnpm add --ignore-scripts --config.node-linker=hoisted` in a temp folder).
- **Dev scripts:** `build.ps1` (build + test + lint, "is the project healthy?"), `dev.ps1` (backend `dotnet watch` + frontend `pnpm dev` in parallel), `db-reset.ps1` (drop + migrate + seed admin).

## Revoked ADRs
The following decisions were revoked and no longer apply; their numbers are retained only so cross-references stay valid:
- **ADR-9 — Backup & Restore Automation** — revoked; superseded by **ADR-15** (backup is operator responsibility).
- **ADR-3.1 — 1C Cluster REST API endpoint contract** — revoked; superseded by **ADR-16** + **ADR-3.3** (`rac.exe` is the sole adapter).
- **ADR-3.2 — Polly v8 circuit breaker configuration** — revoked; the circuit breaker was removed together with the REST adapter (ADR-16).

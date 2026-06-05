# Infrastructure Integration & Adapters

This document defines how the platform interacts with the underlying hosting infrastructure (1C Cluster, IIS, Windows Server). All infrastructure logic MUST be abstracted behind interfaces (Adapter pattern).

## 1. 1C Cluster Integration

### Sole adapter: RAS via `rac.exe` (ADR-16)

The 1C cluster adapter is exclusively `rac.exe` driven against a local `ras.exe` (listening on TCP 1545 by default). See **ADR-16** for why this is the sole adapter.

- **Get sessions:** `rac.exe <endpoint> session list --cluster=<uuid>`. The cluster UUID is resolved by `rac.exe <endpoint> cluster list` and **cached across calls** in `IClusterUuidCache` (singleton, key = `(ExePath, Endpoint)`, MLC-041) — a warm cache skips the `cluster list` spawn entirely; it re-resolves only on a cold cache, an endpoint/path change, or a stale-UUID error.
- **Kill session:** `rac.exe <endpoint> session terminate --cluster=<uuid> --session=<session-uuid> --error-message="<reason>"`.
- **Authentication:** `--cluster-user=<user>` / `--cluster-pwd=<password>` from `Settings.OneC.Cluster.AdminUser` and `Settings.OneC.Cluster.AdminPassword`. Both can be left empty for clusters with no registered administrators (anonymous mode). **Accepted risk (ADR-21):** the password is passed on the `rac.exe` command line — readable by other processes/local admins for the brief life of each invocation — because supported `rac.exe` versions accept it **only** via `--cluster-pwd` (no stdin/file/env channel). On this single-node, admin-only host the residual risk is dominated by the operator's existing privileges; the value is DPAPI-encrypted at rest (ADR-8) and decrypted only at spawn time. Revisit if `rac.exe` ever gains a non-cmdline password channel. See **ADR-21**.
- Implementation: `RacExecutableRasClusterClient` (`Infrastructure/Clusters/`) wraps the process via `IRacProcessRunner` → `SystemProcessRacRunner`. Output is parsed by the pure-static `RacOutputParser` (CP866 OEM code page on RU Windows — see ADR-3.3 for the encoding caveat). Spawn budget: ≤ 26 `rac.exe` processes per minute under sustained over-quota load (warm UUID cache halves the steady-state hot-polling and kill-path spawn rate; MLC-041) — well within OS / antivirus tolerances on a single-node deployment. See **ADR-3.3** for the exact CLI contract, output grammar, kill-idempotency markers, and error semantics.
- **Tool path:** `Settings.OneC.RAS.ExePath` (operator-configured — version-specific, no seeded default since 1C 8.5 moved `rac.exe` out of `1cv8\common\` into `1cv8\<version>\bin\`).
- **RAS endpoint:** `Settings.OneC.RAS.Endpoint` (default `localhost:1545`).

### Idempotent kill protocol
Before issuing a kill, the adapter pipeline (`KillEnforcer` → `IClusterClient.KillSessionAsync`):
1. Re-fetches the target session by `SessionId` from the cluster snapshot.
2. Verifies that `(InfobaseId, AppID, StartedAt)` match the snapshot that triggered the decision.
3. Issues the kill only on match. A mismatch causes the kill to be skipped — the next reconciliation cycle will re-evaluate.
4. Treats `rac.exe` stderr `«Сеанс с указанным идентификатором не найден»` as a successful (idempotent) kill — the session is no longer there, which is the desired end state.

Every kill is recorded in `AuditLog` with reason (`LimitExceeded` or `ManualByAdmin`) and snapshot context.

### RAS health probe (ADR-16)

`RasHealthProbingService : BackgroundService` calls `IClusterClient.PingAsync` every 30s and publishes the result to `IRasHealthReader` (a singleton consumed by the `/api/v1/dashboard/summary` endpoint). The Dashboard surfaces three states on the RAS health card: `OK` (success), `Сбой` (danger, with last-error tooltip + consecutive-failures count), and `Проверка…` (neutral, only during the first 30s after backend startup before the initial probe completes). Health transitions are **not** written to `AuditLog` — the operator sees state in real time on the card.

### Long-lived RAS TCP socket (Strategy B) — deferred
Replacing `rac.exe`-per-cycle with a long-lived socket on 1545 is a backlog item (see `ROADMAP.md`), gated on real-world latency measurement. Until then, the spawn budget above is the binding contract.

## 2. IIS & Publication Management

### The `default.vrd` Lifecycle (Crucial)
A standard 1C web publication is defined by a `default.vrd` XML file (usually located in `C:\inetpub\wwwroot\{VirtualPath}`).
- **The Problem:** The standard 1C `webinst` command-line utility completely overwrites this file, destroying custom settings for HTTP Services, standard OData, and OpenID.
- **The Solution:** The platform's IIS Adapter must manipulate `default.vrd` using XML parsing (`XDocument` or `XmlDocument` in C#).

When a 1C Platform Update occurs, the system must ONLY:
1. Locate the `default.vrd` file.
2. Find the attribute defining the path to the ISAPI module (e.g., `C:\Program Files\1cv8\8.3.xx.xxxx\bin\wsisapi.dll`).
3. Replace the version segment in the path with the new platform version.
4. Save the XML file, leaving all other nodes (`<httpServices>`, `<standardOdata>`, etc.) entirely intact.

### IIS Administration
- The backend interacts with IIS using the `Microsoft.Web.Administration` library.
- It can manage Application Pools (recycle, start, stop) and Sites.
- Desired State: Ensure the IIS Virtual Directory points to the correct physical path containing the `default.vrd`.

### Operational note (path resolution)
- `OneCIisPublishingService` reads and patches `default.vrd` via `XDocument` + `Microsoft.Web.Administration.ServerManager`. Path resolution (via `VrdPathResolver.Resolve`):
  - **Override-first**: if the Publication has `PhysicalPathOverride` set, the resolver uses `{PhysicalPathOverride}\default.vrd`. The add/edit form prefills it as `{Settings.IIS.DefaultVrdRoot}\{databaseName}` (editable), so new publications normally take this branch; it is the physical folder of the IIS application, exactly as shown in IIS Manager.
  - **Convention fallback**: `{Settings.IIS.DefaultVrdRoot}/{siteName}/{trimmedVirtualPath}/default.vrd` — operator-configurable from the Settings page.
- Service-account requirements specific to drift/reconcile (these add to the generic permissions in §3 below):
  - **R/W** on every physical folder containing a managed `default.vrd` file. Reconcile writes to `default.vrd.mlc.tmp` and atomically swaps via `File.Replace`, so the account also needs delete-on-rename permission in that folder.
  - **IIS Metabase read** at minimum (`ServerManager.OpenRemote(null)` / `new ServerManager()`). Sites enumeration is read-only — drift detection does not mutate IIS configuration; only `default.vrd` is mutated.
  - On permission failure (`UnauthorizedAccessException` / `COMException`) the adapter returns `Error` status. `POST /reconcile` translates the same exceptions (plus `IOException` / `InvalidOperationException`) into `409 ProblemDetails` with `code: IIS_RECONCILE_FAILED` (or `IIS_ACCESS_DENIED`) — see ADR-4.1 for the full mutation-and-merge contract. **The full exception is logged server-side; the response `Detail` is a sanitized Russian message (no paths, server/site names, or COM/IO text) carrying a `correlationId` extension so the operator can locate the log entry.**
- **Discovery error contract (`GET /discovery/databases`, `/discovery/iis-sites`).** On any infrastructure failure the endpoint returns `200 OK` with `DiscoveryResponse { Available: false, Error: <short Russian message> }` so the form falls back to manual entry. The raw `ex.Message` (SQL/COM/IO text — may contain server names or paths) is **never** placed in `Error`; the full exception is logged via a source-generated logger. `OperationCanceledException` (request abort) is **not** caught as a discovery error — it propagates.

### Operational note (IIS physical-path alignment)
The drift detector resolves the on-disk VRD path as described above. **If the path is wrong, drift detection reports `Missing` even for a healthy publication.** Two resolution strategies:
1. **Per-publication override (prefilled for every new publication)**: the add/edit form sets `PhysicalPathOverride` to `{IIS.DefaultVrdRoot}\{databaseName}` (e.g., `C:\inetpub\wwwroot\acme_bp`); edit it to the exact physical folder shown in IIS Manager if the layout differs. Drift detection will use this path immediately.
2. **Convention alignment** (fallback when the override is cleared): ensure IIS physical path matches `{IIS.DefaultVrdRoot}/{siteName}/{trimmedVirtualPath}`. Service account also needs:
   - **IIS-admin rights**: read `applicationHost.config` and `redirection.config`, run `ServerManager` against the local IIS. `Network Service` is sufficient on a stock single-node install; custom accounts need `IIS_IUSRS` membership.
   - **R/W access** to the physical folder path (the one referenced by the resolved VRD path).

## 3. Windows Server & Security Context

### Deployment Topology — Single Node
All components run on the same Windows Server host: the .NET backend service, the IIS hosting the 1C publications, the MSSQL instance, and the 1C cluster itself. No remoting, no WinRM, no cross-host adapters. The IIS Adapter operates against a local IIS only, and `default.vrd` files live on the local filesystem. **If this topology assumption ever changes, every infrastructure adapter requires a re-review** (remote IIS administration via WinRM/PowerShell Remoting, distributed locks, etc.).

### Service Account & Permissions
- **Service Account:** The backend .NET service runs under a specific Windows Service Account (e.g., `NT AUTHORITY\NETWORK SERVICE` or a custom local/AD account).
- **Permissions:** This account requires:
  - Read/Write access to the physical folders where `default.vrd` files are stored.
  - Permissions to interact with the IIS Metabase.
  - Network access to the 1C Cluster API/RAS ports (default 1545, etc.).
  - **Execute** permission on the resolved `Settings.OneC.RAS.ExePath` (typically `C:\Program Files\1cv8\<version>\bin\rac.exe`) and **read** access to the rest of `1cv8\<version>\bin\` so co-located DLLs load. Default ACLs grant this to `Users` on stock 1C installs; locked-down custom service accounts may need explicit Read+Execute grant.
  - Read/Write access to `%ProgramData%\MitLicenseCenter\keys\` (Data Protection key ring — see ADR-8).

### Secrets Storage
Secrets (1C cluster credentials, MSSQL connection strings, RAS credentials) are stored encrypted using the **ASP.NET Core Data Protection API**, which is DPAPI-backed on Windows. Keys live under `%ProgramData%\MitLicenseCenter\keys\` in production (machine-scoped, service-account-readable) and **must be included in the operator's backup set (see `OPERATIONS.md` and ADR-15)** — without them restored backups cannot decrypt their own secrets. See ADR-8 for the full policy.

**Development override:** when `ASPNETCORE_ENVIRONMENT=Development` the backend persists keys to `%LocalAppData%\MitLicenseCenter\keys\` instead. This avoids the need for elevation just to run the app locally. Local dev keys are NOT a substitute for the production key ring and must never be confused with the production key ring the operator backs up.

**Persistence layout:** runtime configuration lives in the `dbo.Settings` table. Each row has both a `ValueText NVARCHAR(MAX) NULL` column (plain payload) and a `Value VARBINARY(MAX) NULL` column (DPAPI-encrypted UTF-8 bytes). `IsSecret BIT` decides which column is authoritative on write. Operationally this means: **deleting `%ProgramData%\MitLicenseCenter\keys\` makes every `IsSecret=true` row unreadable** — the 1C cluster admin password and (later) RAS credentials would have to be re-entered through the «Параметры» UI. The Settings table is part of the operator's MSSQL backup; the DPAPI key ring is part of the operator's file-level backup. Both must be restored together — see `OPERATIONS.md` and ADR-15. The DPAPI protector purpose-string is `mlc.settings.v1` — a future scheme change bumps the suffix and migrates rows, leaving the key ring file format unchanged.

### Admin Authentication
Administrators authenticate against the panel using ASP.NET Core Identity with local accounts stored in the same MSSQL domain database (schema `auth`). Cookie-based session auth (HttpOnly, Secure, SameSite=Strict). Two roles: `Admin` and `Viewer`. See ADR-7.

## 4. Runtime Settings Catalog

Runtime configuration lives in `dbo.Settings`. The catalog is the single source of truth in `MitLicenseCenter.Application/Settings/SettingDefinitions.cs` — adding a key = a commit to `SettingKey.cs` + an entry in the catalog (+ a reader in `ISettingsSnapshot` if a hot-path needs it). The endpoint validates against the catalog; the seeder seeds from it idempotently.

| Key | Secret | Kind | Default | Range | Used by |
|---|---|---|---|---|---|
| `OneC.Cluster.AdminUser` | no | Text | — | — | `rac.exe --cluster-user` |
| `OneC.Cluster.AdminPassword` | **yes** | Text | — | — | `rac.exe --cluster-pwd` |
| `OneC.RAS.Endpoint` | no | HostPort | — | port [1024, 65535] | RAS adapter |
| `OneC.RAS.ExePath` | no | Path | — *(no seeded default — 1C 8.5 keeps `rac.exe` in `1cv8\<version>\bin\`)* | — | RAS adapter |
| `OneC.LicenseConsumingAppIds` | no | Text | `1CV8,1CV8C,WebClient,Designer,COMConnection` | comma-separated, case-insensitive; empty → default | RAS adapter (`ConsumesLicense` whitelist — MLC-024) |
| `IIS.DefaultVrdRoot` | no | Path | `C:\inetpub\wwwroot` | — | VRD path resolver + add-infobase physical-path prefill |
| `Defaults.DatabaseServer` | no | Text | — | — | add-infobase form prefill |
| `IIS.DefaultSiteName` | no | Text | `Default Web Site` | — | add-infobase form prefill |
| `OneC.DefaultPlatformVersion` | no | Text | — | — | add-infobase form prefill |
| `Polling.HotIntervalSeconds` | no | Number | `4` | [2, 60] | `HotTierPollingService` |
| `Polling.ColdIntervalSeconds` | no | Number | `25` | [10, 300] | cold `ReconciliationJob` throttle |
| `Polling.HotThresholdPercent` | no | Number | `90` | [50, 100] | `HotTierRegistry` |
| `Drift.IntervalMinutes` | no | Number | `5` | [1, 60] | `DriftCheckJob` throttle |
| `Audit.RetentionDays` | no | Number | `365` | [30, 3650] | `AuditRetentionJob` (daily 03:00 UTC) |

`SettingValueKind ∈ {Text, Number, Url, HostPort, Path}`. A `null`/whitespace payload clears the value (`IsSet=false`); for a secret that means "remove the password". Validation: `Number` = `int.TryParse` + range; `HostPort` = `^[^\s:]+:\d+$` + port in [1024, 65535]; `Path`/`Text` = non-empty. The whitelist takes priority over DB rows — a key not in the catalog is never surfaced.

The three `*Default*` form-prefill keys (`Defaults.DatabaseServer`, `IIS.DefaultSiteName`, `OneC.DefaultPlatformVersion`) are **UI-only**: no backend service reads them. They seed the «Добавить инфобазу» form so the operator does not retype the same SQL server / IIS site / platform version for every base. The values are still persisted per-base on `Infobase`/`Publication`; the form just pre-fills them and lets the operator override in the «Дополнительно» disclosure.

## 5. Background Job Execution

Infrastructure operations (especially mass IIS updates or 1C cluster scans) can be blocking.
- The Reconciliation Loop (Session Monitor) runs asynchronously and on a two-tier cadence (hot 3–5s for at-risk tenants, cold 20–30s for everyone). See ADR-6.
- Direct calls to the 1C Cluster Adapter MUST have strict timeouts to prevent the background worker from hanging if rac.exe / ras.exe become unresponsive. `RacExecutableRasClusterClient` uses a 30s per-invocation deadline (ADR-3.3).
- **`RasHealthProbingService`** (ADR-16) runs as a `BackgroundService` with a 30s `IClusterClient.PingAsync` cadence. Publishes `IRasHealthReader` snapshot for the Dashboard RAS health card. Audit-neutral. This snapshot is the **only** RAS source the readiness probe reads — the `GET /api/v1/health/ready` endpoint (MLC-040) reuses it and never spawns its own `rac.exe`, so health requests stay inside the spawn budget ([ADR-3.3](DECISIONS.md#33-rac-cli-spawn-contract)). Liveness `GET /api/v1/health` is unchanged (cheap, dependency-free). See `OPERATIONS.md` «Проверки готовности».
- A separate **drift-detection job** runs every 5 minutes (throttled to `Settings.Drift.IntervalMinutes`), comparing each `Publication`'s desired state against the actual `default.vrd` + IIS state. Results are written to `Publication.LastDriftStatus` / `LastDriftCheckAt` / `LastDriftDetails`. Drift is reported, never auto-corrected — reconcile is an explicit admin action.
- An **audit retention job** runs daily at 03:00 UTC and deletes rows from `dbo.AuditLogs` older than `Settings.Audit.RetentionDays` (default 365, range [30, 3650]). Implementation: batched `DELETE TOP (5000)` with commit-per-batch via `ExecuteSqlInterpolatedAsync` against `[Timestamp]`. Audit row `AuditLogsPurged=500` is written only when the total delete count is non-zero. CRON is fixed in code — retention window is operator-tunable, cadence is not.

## 6. Backups are operator responsibility

Per ADR-15, backup orchestration is **out of application scope**. The operator backs up the MSSQL database, the Data Protection key ring under `%ProgramData%\MitLicenseCenter\keys\`, and optionally `appsettings.Production.json`, using platform tooling (SQL Server Maintenance Plans, Veeam, Windows Server Backup, Robocopy + Scheduled Task). The key ring and the database form one logical backup unit — restoring one without the other yields an unreadable database. See `OPERATIONS.md` for the operator's checklist.
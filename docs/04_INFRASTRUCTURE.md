# Infrastructure Integration & Adapters

This document defines how the platform interacts with the underlying hosting infrastructure (1C Cluster, IIS, Windows Server). All infrastructure logic MUST be abstracted behind interfaces (Adapter pattern).

## 1. 1C Cluster Integration

### Primary Method: 1C Cluster REST API
Since the platform targets 1C versions 8.3 to 8.5, the primary integration method is the **1C Cluster Administration REST API**.
- **Get Sessions:** A GET request to the cluster API retrieves the list of active sessions, infobase IDs, AppIDs, and user info.
- **Kill Session:** A DELETE (or specific POST) request to the cluster API using the specific `SessionId` forces the termination of a session.

### Idempotent Kill Protocol
Before issuing a kill, the adapter:
1. Re-fetches the target session by `SessionId` from the cluster.
2. Verifies that `(InfobaseId, AppID, StartedAt)` match the snapshot that triggered the decision.
3. Issues the kill only on match. A mismatch causes the kill to be skipped — the next reconciliation cycle will re-evaluate.
4. Treats a `404 / session not found` response as a successful (idempotent) kill — the session is no longer there, which is the desired end state.

Every kill is recorded in `AuditLog` with reason (`LimitExceeded` or `ManualByAdmin`) and snapshot context.

### Fallback Method: RAS (Remote Administration Server)
If the REST API is unavailable, the system falls back to communicating with the RAS service via TCP (using custom C# wrappers or direct socket communication, avoiding excessive `rac.exe` process spawning).

### REST → RAS Failover (Circuit Breaker)
The 1C Cluster Adapter wraps REST calls in a Polly-based circuit breaker:
- **Open trigger:** three consecutive REST failures or timeouts within a short window.
- **Open behavior:** subsequent calls are routed to the RAS adapter.
- **Half-open probe:** every 60 seconds a single REST probe is attempted. On success the circuit closes and REST resumes.
- Circuit state transitions (`CircuitOpened`, `CircuitClosed`) are written to `AuditLog` so admins can see when fallback was in effect.

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

## 3. Windows Server & Security Context

### Deployment Topology — Single Node
All components run on the same Windows Server host: the .NET backend service, the IIS hosting the 1C publications, the MSSQL instance, and the 1C cluster itself. No remoting, no WinRM, no cross-host adapters. The IIS Adapter operates against a local IIS only, and `default.vrd` files live on the local filesystem. **If this topology assumption ever changes, every infrastructure adapter requires a re-review** (remote IIS administration via WinRM/PowerShell Remoting, distributed locks, etc.).

### Service Account & Permissions
- **Service Account:** The backend .NET service runs under a specific Windows Service Account (e.g., `NT AUTHORITY\NETWORK SERVICE` or a custom local/AD account).
- **Permissions:** This account requires:
  - Read/Write access to the physical folders where `default.vrd` files are stored.
  - Permissions to interact with the IIS Metabase.
  - Network access to the 1C Cluster API/RAS ports (default 1545, etc.).
  - Read/Write access to `%ProgramData%\MitLicenseCenter\keys\` (Data Protection key ring — see ADR-8).
  - Read/Write access to the backup destination folder (see ADR-9).

### Secrets Storage
Secrets (1C cluster credentials, MSSQL connection strings, RAS credentials) are stored encrypted using the **ASP.NET Core Data Protection API**, which is DPAPI-backed on Windows. Keys live under `%ProgramData%\MitLicenseCenter\keys\` in production (machine-scoped, service-account-readable) and are included in the backup set — without them restored backups cannot decrypt their own secrets. See ADR-8 for the full policy.

**Development override:** when `ASPNETCORE_ENVIRONMENT=Development` the backend persists keys to `%LocalAppData%\MitLicenseCenter\keys\` instead. This avoids the need for elevation just to run the app locally. Local dev keys are NOT a substitute for the production key ring and must never be deployed alongside a production database backup.

**Persistence layout (Stage 3 PR 3.1):** runtime configuration lives in the `dbo.Settings` table. Each row has both a `ValueText NVARCHAR(MAX) NULL` column (plain payload) and a `Value VARBINARY(MAX) NULL` column (DPAPI-encrypted UTF-8 bytes). `IsSecret BIT` decides which column is authoritative on write. Operationally this means: **deleting `%ProgramData%\MitLicenseCenter\keys\` makes every `IsSecret=true` row unreadable** — the 1C cluster admin password and (later) RAS credentials would have to be re-entered through the «Параметры» UI. The Settings table itself is part of the regular database backup; the DPAPI key ring is part of the daily companion-artifacts copy (see §5). Both must be restored together. The DPAPI protector purpose-string is `mlc.settings.v1` — a future scheme change bumps the suffix and migrates rows, leaving the key ring file format unchanged.

### Admin Authentication
Administrators authenticate against the panel using ASP.NET Core Identity with local accounts stored in the same MSSQL domain database (schema `auth`). Cookie-based session auth (HttpOnly, Secure, SameSite=Strict). Two roles: `Admin` and `Viewer`. See ADR-7.

## 4. Background Job Execution

Infrastructure operations (especially mass IIS updates or 1C cluster scans) can be blocking.
- The Reconciliation Loop (Session Monitor) runs asynchronously and on a two-tier cadence (hot 3–5s for at-risk tenants, cold 20–30s for everyone). See ADR-6.
- Direct calls to the 1C Cluster API MUST have strict timeouts to prevent the background worker from hanging if the 1C cluster becomes unresponsive.
- A separate **drift-detection job** runs every 5 minutes, comparing each `Publication`'s desired state against the actual `default.vrd` + IIS state. Results are written to `Publication.LastDriftStatus` / `LastDriftCheckAt` / `LastDriftDetails`. Drift is reported, never auto-corrected — reconcile is an explicit admin action.
- **Backup jobs** (full nightly, differential every 6 hours, transaction log every 15 minutes) are also scheduled here, plus a weekly **verification restore** to a `MitLicenseCenter_RestoreTest` database. See ADR-9.

## 5. Backup & Restore

- **Backup orchestration:** Hangfire jobs invoke `BACKUP DATABASE` / `BACKUP LOG` via ADO.NET against the domain database.
- **Schedule:** Full = nightly; Differential = every 6h; Transaction log = every 15 min.
- **Companion artifacts:** Data Protection keys (`%ProgramData%\MitLicenseCenter\keys\`) and `appsettings.Production.json` are copied to the backup folder daily — without them a restored database is unreadable.
- **Destination:** Configurable local folder (default `D:\Backups\MitLicenseCenter\`), with an optional secondary copy to a network share.
- **Retention:** Configurable; defaults — 30 days for full backups, 7 days for hourly increments.
- **Verification:** Weekly automated restore to `MitLicenseCenter_RestoreTest`, row-count assertions, result published to `AuditLog` and surfaced on the Dashboard as a health indicator.
- **Restore CLI:** A standalone tool `MitLicenseCenter.Restore.exe` ships with the product for disaster recovery; documented in `OPERATIONS.md`.
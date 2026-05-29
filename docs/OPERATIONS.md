# Operations Manual

This document covers tasks that live **outside** the MitLicense Center application — concerns the operator handles via platform tooling rather than in-app features. The application surface itself is documented in `docs/05_UI_REQUIREMENTS.md` and `docs/06_UI_DESIGN.md`.

## Backup is operator responsibility

Per [ADR-15](DECISIONS.md#15-backup-and-two-factor-authentication-scope-boundary), the application does not orchestrate its own backups. The operator is responsible for backing up three artefacts:

| Artefact | Path | Notes |
| --- | --- | --- |
| MSSQL database | `MitLicenseCenter` (default catalog name on the SQL Server instance) | Standard `BACKUP DATABASE` / Maintenance Plan / Veeam / etc. Full + differential + transaction-log cadence per the operator's RPO. |
| Data Protection key ring | `%ProgramData%\MitLicenseCenter\keys\` | **CRITICAL.** Without these keys, a restored database **cannot decrypt** its own `dbo.Settings` rows (1C cluster admin password, future RAS credentials). Back up at the same cadence as the database. |
| Application config (optional) | `appsettings.Production.json` next to the deployed binary | Only required if it carries environment-specific settings the operator wants preserved. Most deployments keep runtime config in `dbo.Settings` instead. |

### Recommended tooling

- **MSSQL:** SQL Server Maintenance Plan (works on Express via the standalone Backup utility), Veeam, or Windows Server Backup. Schedule full + differential + transaction-log per the deployment's recovery objective. **Run a periodic restore drill against a non-production instance** — backups that have not been restored are not backups.
- **Key ring + config:** Robocopy + Windows Task Scheduler (or any file-level backup tool) at the same cadence as the database. Veeam or Windows Server Backup also work; pick whichever is already running elsewhere in the environment.

### Hard warning — key ring and database must be restored together

The key ring and the database form **one logical backup unit**. A database backup without its matching key ring backup is an **unreadable** database: every `IsSecret=true` row in `dbo.Settings` was encrypted with the keys in `%ProgramData%\MitLicenseCenter\keys\`, and there is no recovery path without them.

If the key ring is ever lost without a backup, the operator must re-enter every secret setting through the «Параметры» admin UI (currently: 1C cluster admin password; in the future: RAS credentials, any new secret added to the Settings catalog). The non-secret rows (URLs, intervals, paths) are unaffected — they live in `ValueText` as plaintext.

## Authentication is delegated to the network edge

Per [ADR-15](DECISIONS.md#15-backup-and-two-factor-authentication-scope-boundary) and the [Status update on ADR-7](DECISIONS.md#status-update-stage-4-pr-44), the application performs username + password + HttpOnly cookie session authentication and nothing more. Secondary factor protection is the responsibility of the deployment environment:

- **Network reachability:** LAN-only or VPN. The application's HTTPS endpoint must not be exposed to the public internet.
- **Perimeter:** firewall rules limiting source IPs to the operator network / VPN concentrator.
- **Identity:** AD / SSO authentication at the network edge if the environment uses it.
- **Physical:** restricted physical access to the operator workstations from which the panel is reached.

If the deployment cannot satisfy these network-level controls, the operator should re-evaluate whether the panel is being used in its intended environment (internal 1C-hosting operator station) before requesting in-app 2FA — ADR-15 makes the latter explicitly off-limits without a deliberate revocation.

## Upgrading from Stage 3 / Stage 4 with REST configured

> **Applies to: existing deployments upgrading to Stage 5 PR 5.1 or later.**

Stage 5 PR 5.1 removes the 1C Cluster REST adapter entirely; `rac.exe` becomes the sole 1C cluster adapter (see [ADR-16](DECISIONS.md#16-ras-as-sole-1c-cluster-adapter)). Most deployments are unaffected — REST was not published by default on 1C 8.5 and most operators were already running RAS via `rac.exe` regardless. But if a deployment had successfully configured `OneC.Cluster.RestApiUrl` and was relying on REST as the primary adapter, the upgrade silently deletes the row and the system needs to be reconfigured for RAS before session enforcement resumes.

### What the upgrade migration does

`Stage5DropRestClusterSettings` migration deletes the following rows from `dbo.Settings`:

- `OneC.Cluster.RestApiUrl`
- `OneC.Cluster.RestApiTimeoutSeconds`
- `CircuitBreaker.ProbeIntervalSeconds`
- `CircuitBreaker.FailureCount`

The migration is **roll-forward only** — `Down()` throws `NotSupportedException` referencing ADR-16. The two surviving cluster-admin keys (`OneC.Cluster.AdminUser`, `OneC.Cluster.AdminPassword`) are **preserved** — they now feed `rac.exe`'s `--cluster-user` / `--cluster-pwd` flags instead of REST Basic-auth.

### Operator checklist before upgrade

1. **Confirm `rac.exe` is installed on the application host.** On 1C 8.5 it lives at `C:\Program Files\1cv8\<version>\bin\rac.exe` (the legacy `C:\Program Files\1cv8\common\` path no longer ships the utility).
2. **Confirm `ras.exe` is running as a Windows service** on the application host (`localhost:1545` by default). If not, install it via `racsvc.exe -instsrvc` (standard 1C distribution).
3. **Confirm the backend service account has Read+Execute** on the `1cv8\<version>\bin\` directory. `Network Service` is sufficient on stock installs; locked-down custom accounts may need explicit ACL grants.

### Operator checklist after upgrade

1. Open the «Параметры» admin UI.
2. Set `OneC.RAS.ExePath` to the version-specific `rac.exe` path (e.g., `C:\Program Files\1cv8\8.5.1.1302\bin\rac.exe`). **There is no seeded default** — 1C 8.5 changed the path layout, so each operator supplies the version they have installed.
3. Verify `OneC.RAS.Endpoint` is `localhost:1545` (or whatever endpoint the local `ras.exe` listens on).
4. Verify `OneC.Cluster.AdminUser` and `OneC.Cluster.AdminPassword` are still set (they survived the migration and now authenticate rac.exe). For clusters with no registered administrators, both fields can be left empty — `rac.exe` runs anonymously.
5. Open the Dashboard. Within 30 seconds the RAS health card should flip from `Сбой` (or `Проверка…` if this is the first ping after backend startup) to `OK`. If it stays `Сбой`, click "детали ошибки" on the card to see the rac.exe stderr — the most common causes are wrong `OneC.RAS.ExePath`, `ras.exe` not running, or insufficient ACLs on the rac.exe path.

The Sessions Monitor will resume populating from the next reconciliation cycle (cold tier ≤ 30s after RAS comes up).

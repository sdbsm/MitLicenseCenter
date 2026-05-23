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

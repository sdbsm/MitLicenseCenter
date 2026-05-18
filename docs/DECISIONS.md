# Architecture Decision Records (ADR)

This document tracks important architectural decisions, why they were made, and what alternatives were rejected.

## 1. Frontend Framework
- **Decision:** React (Single Page Application) with TypeScript.
- **Rejected Alternatives:** Blazor WebAssembly, ASP.NET Core MVC, Vue.js.
- **Reason:** React provides strict API boundaries between the frontend and the .NET infrastructure backend. It ensures the UI is entirely decoupled from the server logic and avoids tight coupling common in MVC/Blazor Server setups.

## 2. 1C Cluster Integration Method
- **Decision:** Use 1C Cluster REST API (available in 1C Platform 8.3+).
- **Rejected Alternatives:** Spawning `rac.exe` processes, COM-connection (`V83.COMConnector`), direct database tampering.
- **Reason:** The REST API is natively supported, fast, and does not spawn heavy OS processes like `rac.exe` for every monitoring tick. COM-connections are slow and memory-leaky.

## 3. License Enforcement Approach
- **Decision:** Snapshot-based Background Reconciliation Loop (Polling every 15-30s).
- **Rejected Alternatives:** Real-time connection interception / Web server hooks.
- **Reason:** The 1C Platform does not provide a reliable native hook to reject a user connection based on global external multi-tenant rules. The background worker approach ensures the cluster is not blocked by custom external logic during login.

## 4. IIS Publication Updates (`default.vrd`)
- **Decision:** XML Parsing and surgical modification of `default.vrd`.
- **Rejected Alternatives:** Using the standard 1C `webinst` CLI utility for existing publications.
- **Reason:** `webinst` blindly overwrites `default.vrd`, destroying custom configurations for OData, HTTP services, and OpenID. Parsing the XML ensures we only update the `<wsisapi.dll>` platform path, guaranteeing idempotency and preserving customizations.

## 5. Architectural Style
- **Decision:** Modular Monolith (C# / .NET).
- **Rejected Alternatives:** Distributed Microservices (Kubernetes, Docker Swarm).
- **Reason:** The system operates tightly with a specific Windows Server infrastructure (IIS, 1C Server, MSSQL). Microservices introduce unnecessary network overhead and operational complexity for a localized hosting panel.

## 6. Reconciliation Loop Cadence (Two-Tier)
- **Decision:** Two-tier reconciliation loop:
  - **Hot loop** — tenants at or near their license limit (≥ 90% consumption) are polled every **3–5 seconds**.
  - **Cold loop** — full snapshot of all sessions across all tenants every **20–30 seconds**.
  - A tenant exits the hot tier after two consecutive cold cycles below the threshold.
- **Rejected Alternatives:** Single uniform 15–30s loop; per-second real-time polling for everyone.
- **Reason:** A single 30s loop leaves a window in which an over-quota user can log in, open a base, edit a document, and lose unsaved work when killed. Lowering the global cadence to 3–5s overloads the 1C cluster as the number of bases grows. The hot/cold split bounds the enforcement window to ≤ 5s for tenants who are actually trying to exceed quota, while keeping baseline cluster load low.

## 7. Admin Authentication
- **Decision:** ASP.NET Core Identity with local accounts stored in MSSQL, cookie authentication (HttpOnly, Secure, SameSite=Strict).
  - Two roles: `Admin` (full access incl. session kill and publication reconcile) and `Viewer` (read-only).
  - First admin account seeded by migration on first run with a random password written to the service log.
  - TOTP-based 2FA available but optional in v1 (Identity supports it natively, can be enabled later without schema migration).
- **Rejected Alternatives:** Windows Integrated Authentication (requires AD or local Windows accounts per admin — fragile when non-Windows-account staff need access); JWT bearer tokens (cookie auth is simpler and safer for a same-origin SPA); external identity providers (violates "no external systems" constraint).
- **Reason:** Fully local, no external dependencies, no AD required. Identity is the .NET-native solution and integrates with the same MSSQL the domain already uses.

## 8. Secret Management
- **Decision:** ASP.NET Core Data Protection API. On Windows this transparently uses DPAPI; key ring stored under `%ProgramData%\MitLicenseCenter\keys\`, scoped to the service account.
  - Secrets (MSSQL connection strings, 1C Cluster credentials, RAS credentials, IIS service-account creds) stored encrypted in a `Settings` table in MSSQL (or `appsettings.Production.json`), decrypted at runtime.
  - Data Protection keys are backed up alongside the database (see ADR-9) — without them, a restored backup is unreadable.
  - Development environments use .NET User Secrets.
- **Rejected Alternatives:** HashiCorp Vault, Azure Key Vault, dedicated secret services (all violate "no external systems"); plaintext config files.
- **Reason:** Built into .NET, machine- and account-scoped via DPAPI, zero external dependencies, integrates with the existing backup story.

## 9. Backup & Restore Automation
- **Decision:** Fully automated backups orchestrated by Hangfire jobs that invoke `BACKUP DATABASE` / `BACKUP LOG` via ADO.NET.
  - Full backup: nightly.
  - Differential backup: every 6 hours.
  - Transaction log backup: every 15 minutes.
  - Data Protection keys and `appsettings` files copied to the backup location daily.
  - Default destination: configurable local folder (e.g. `D:\Backups\MitLicenseCenter\`), optional secondary copy to a network share.
  - Retention: configurable (defaults: 30 days for full, 7 days for hourly).
  - **Verification job:** weekly automatic restore of the latest full backup to a `MitLicenseCenter_RestoreTest` database, asserts row counts, logs the result to `AuditLog` and surfaces it on the Dashboard.
  - **Restore** delivered as a standalone CLI tool (`MitLicenseCenter.Restore.exe`) documented in `OPERATIONS.md`.
- **Rejected Alternatives:** Manual DBA-driven backups; relying solely on SQL Server Agent (requires SQL Server Standard/Enterprise; we want it to work on Express too, and we want a unified job surface visible in the Hangfire dashboard).
- **Reason:** Admin requirement is "no manual steps." Co-locating backup orchestration with the rest of the background workload means one dashboard, one retry policy, one audit trail.

## 10. REST API Versioning
- **Decision:** URI-based versioning. All endpoints live under `/api/v1/...`, implemented with `Asp.Versioning.Mvc`. New breaking changes introduce `/api/v2/...`; the previous major version is supported for at least one release cycle.
  - OpenAPI/Swagger UI generated automatically via Swashbuckle, served at `/api/docs`.
  - TypeScript client for the frontend generated from the OpenAPI spec (`openapi-typescript` or `orval`).
- **Rejected Alternatives:** Header-based versioning only (harder to discover and test); no versioning at all (acceptable today, painful when the 1C platform jumps and adapter contracts change).
- **Reason:** URI versioning is the most discoverable and tooling-friendly approach. Since frontend and backend deploy together as one product, complex negotiation is unnecessary.

## 12. Backend Runtime = .NET 10 (LTS)
- **Decision:** Target `net10.0` for the backend. Released 11 November 2025, LTS until 14 November 2028.
- **Rejected Alternatives:** `net8.0` (older LTS, ~18 months less runway); `net9.0` (current but STS only, EOL May 2026 — short LTS window doesn't suit a production-grade hosting panel); .NET Framework 4.8 (legacy, no path forward).
- **Reason:** Maximum LTS runway (~2.5 years ahead as of project start), latest C# 14 language features, EF Core 10, ASP.NET Core 10. All planned libraries (Hangfire, Identity, `Asp.Versioning.Mvc`, Swashbuckle, `Microsoft.Web.Administration`, Polly) are compatible.

## 13. Repository Layout = Monorepo
- **Decision:** Single Git repository hosted on **GitHub** (private), structured as a monorepo:
  ```
  backend/      .NET solution (src/, tests/, MitLicenseCenter.sln)
  frontend/     Vite + React + TS
  docs/         project documentation (this folder)
  scripts/      PowerShell scripts for build/dev/deploy/db-reset
  .github/      CI workflows
  ```
- **Rejected Alternatives:** Two separate repos (frontend + backend). Polyrepo overhead (cross-repo coordination, version drift) is not justified for a one-product, one-team codebase. Single-developer at start makes monorepo unambiguously simpler.
- **Reason:** Atomic commits across backend/frontend/docs, single CI pipeline, one source of truth for issues and project state, lower onboarding cost. The OpenAPI-generated TypeScript client lives naturally next to the API that produces it.

## 14. CI/CD = GitHub Actions, CI Only (No CD in v1)
- **Decision:** GitHub Actions runs on every push to any branch and on every PR to `main`:
  - **Backend job:** `dotnet restore` → `dotnet build` → `dotnet test`. Coverage report informational.
  - **Frontend job:** `pnpm install` → `pnpm lint` (ESLint + Prettier) → `pnpm type-check` (`tsc --noEmit`) → `pnpm build`.
  - PRs to `main` are blocked from merge while either job is red.
  - **No CD (deployment) automation in v1.** Deployment is performed manually via `scripts/Deploy-MitLicenseCenter.ps1`, which can be invoked from a developer machine or from a manual GitHub Actions workflow_dispatch trigger when needed.
- **Rejected Alternatives:**
  - No CI at all (acceptable for a throwaway, unacceptable for a system that auto-kills user sessions and writes backups — broken tests must not reach `main`).
  - Full CD on tag (premature for a single-developer phase with no release cadence yet; revisit after the first production deployment).
  - Self-hosted runners (no need at this scale; GitHub-hosted free minutes are sufficient).
  - Jenkins / TeamCity / Azure DevOps (additional infrastructure to maintain; GitHub Actions is co-located with the repository).
- **Reason:** Minimum viable safety net for a single-developer project that will run automated destructive operations in production. CI from day one establishes the muscle memory; CD waits until the deployment story stabilizes.

## Tooling Constraints (binding alongside ADR-13/14)

- **Frontend package manager:** `pnpm` (not `npm` or `yarn`). Faster, disk-efficient, strict on phantom dependencies. Pinned via `packageManager` field in `package.json` and Corepack.
- **Pre-commit hooks:** `husky` + `lint-staged`. Linter and formatter run on staged files before commit. Backend: `dotnet format --verify-no-changes` on staged `.cs` files via husky shell hook.
- **Local dev scripts** (`scripts/`):
  - `build.ps1` — builds backend and frontend, runs tests and lint. Single command for "is the project healthy?".
  - `dev.ps1` — starts backend (`dotnet watch run`) and frontend (`pnpm dev`) in parallel for local development.
  - `db-reset.ps1` — drops the local MSSQL database, recreates it via EF migrations, seeds the initial admin user.
- **Deployment script** `Deploy-MitLicenseCenter.ps1` — added in the deployment-readiness phase (not v1 of v1).
- **Decision:** shadcn/ui (copy-paste components owned in our repo) on top of Radix UI primitives, styled with Tailwind CSS. Auxiliary stack: `lucide-react` (icons), `@tanstack/react-table` (data tables), `react-hook-form` + `zod` (forms), `sonner` (toasts), `recharts` (charts), `date-fns` + `date-fns/locale/ru` (date formatting).
- **Rejected Alternatives:** Material UI (heavier runtime, opinionated visual language farther from the "modern minimal admin" look the team prefers; bringing both MUI and Tailwind would bloat the bundle); Ant Design (excellent for admin density but visual style feels dated and the ecosystem is more China-centric); building a fully custom design system (overkill for a 5–20 user admin panel).
- **Reason:** shadcn/ui is owned in the repo (no vendor lock-in, no version upgrades that break visuals), built on accessible Radix primitives, themable via CSS variables (gives us light + dark out of the box), and is the dominant choice in 2025 for modern admin dashboards. Tailwind-only styling keeps the bundle small. Detailed visual language, status semantics, table patterns, destructive-action UX, freshness indicators, and Russian microcopy are codified in `06_UI_DESIGN.md`.

## Locked Operational Constraints (not full ADRs, but binding)

- **Kill priority:** when `Consumed > Limit`, kill sessions ordered by `StartedAt DESC` (newest first) until `Consumed == Limit`. Justification: simplest to explain to end users ("you just logged in and were dropped because the quota was already full") and avoids interrupting in-progress work of established sessions.
- **Idempotent kill protocol:** before issuing a kill, the 1C Cluster Adapter re-fetches the session by ID and verifies `(InfobaseId, AppID, StartedAt)` match the snapshot. Mismatch → skip and wait for the next cycle. A `404 / session not found` response from the cluster is treated as a successful kill (idempotency). Every kill writes an `AuditLog` entry with the reason (`LimitExceeded` / `ManualByAdmin`) and the snapshot context.
- **Drift detection:** a Hangfire job runs every 5 minutes, compares each `Publication`'s desired state against the actual `default.vrd` + IIS state, and writes `LastDriftStatus` / `LastDriftCheckAt`. An on-demand `POST /api/v1/publications/{id}/check-drift` is available from the UI. **Drift is never auto-corrected** — the admin must click "Reconcile" explicitly. Both detection and reconcile actions are audited.
- **REST → RAS failover:** the 1C Cluster Adapter uses a Polly-based circuit breaker. Three consecutive REST failures or timeouts open the circuit and route subsequent calls to the RAS adapter. A background probe retries REST every 60 seconds; on success the circuit closes. State transitions are written to `AuditLog`.
- **Deployment topology:** single-node — the .NET backend, IIS (publications), MSSQL, and the 1C cluster all run on the same Windows Server. No remoting, no WinRM, no cross-host adapters. If this assumption changes, all infrastructure adapters need a re-review.
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

- **.NET solution format:** `.slnx` (the XML solution format that became the SDK 10 default). All scripts and CI reference `backend/MitLicenseCenter.slnx`. The classic `.sln` is not used.
- **Frontend package manager:** `pnpm` (not `npm` or `yarn`). Faster, disk-efficient, strict on phantom dependencies. Pinned via the `packageManager` field in `frontend/package.json`. **Corepack is optional, not mandatory** — on Windows under a non-admin user it cannot write to `C:\Program Files\nodejs\`, so the supported install path is the standalone winget package (`winget install pnpm.pnpm`). Corepack works fine on Linux/macOS and on an elevated Windows shell, but we cannot rely on it as the only path.
- **Node.js minimum:** **Node 22.13+** (driven by pnpm 11 — older Node versions fail with `No such built-in module: node:sqlite`). Older Node versions are not supported.
- **Pre-commit hooks:** custom `.husky/pre-commit` shell script registered via `git config core.hooksPath .husky` (the `core.hooksPath` value is set idempotently by `frontend/scripts/install-git-hooks.mjs`, which runs from `pnpm install`'s `prepare`). **The `husky` npm package itself is intentionally not installed** — it offers no extra value once `core.hooksPath` is set, and removing it shortens the dependency surface. The hook runs `lint-staged` on staged JS/TS/JSON/CSS in `frontend/` and `dotnet format --include … --verify-no-changes` on staged `.cs`. On Windows the hook requires Git Bash (ships with Git for Windows).
- **PowerShell script encoding:** all `scripts/*.ps1` files must be saved as **UTF-8 with BOM**. Windows PowerShell 5.1 (still the default `powershell.exe` on Windows 10/11) reads BOM-less UTF-8 as cp1251 and corrupts Cyrillic strings inside scripts. PowerShell 7 (`pwsh`) handles BOM-less UTF-8 correctly, but the project supports both shells.
- **Local dev scripts** (`scripts/`):
  - `build.ps1` — builds backend and frontend, runs tests and lint. Single command for "is the project healthy?".
  - `dev.ps1` — starts backend (`dotnet watch run`) and frontend (`pnpm dev`) in parallel for local development.
  - `db-reset.ps1` — drops the local MSSQL database, recreates it via EF migrations, seeds the initial admin user.
- **Deployment script** `Deploy-MitLicenseCenter.ps1` — added in the deployment-readiness phase (not v1 of v1).
- **Decision:** shadcn/ui (copy-paste components owned in our repo) on top of Radix UI primitives, styled with Tailwind CSS. Auxiliary stack: `lucide-react` (icons), `@tanstack/react-table` (data tables), `react-hook-form` + `zod` (forms), `sonner` (toasts), `recharts` (charts), `date-fns` + `date-fns/locale/ru` (date formatting).
- **Rejected Alternatives:** Material UI (heavier runtime, opinionated visual language farther from the "modern minimal admin" look the team prefers; bringing both MUI and Tailwind would bloat the bundle); Ant Design (excellent for admin density but visual style feels dated and the ecosystem is more China-centric); building a fully custom design system (overkill for a 5–20 user admin panel).
- **Reason:** shadcn/ui is owned in the repo (no vendor lock-in, no version upgrades that break visuals), built on accessible Radix primitives, themable via CSS variables (gives us light + dark out of the box), and is the dominant choice in 2025 for modern admin dashboards. Tailwind-only styling keeps the bundle small. Detailed visual language, status semantics, table patterns, destructive-action UX, freshness indicators, and Russian microcopy are codified in `06_UI_DESIGN.md`.

## Stage 2 UI shell & auth additions (binding alongside ADR-7/ADR-11)

- **shadcn config (`frontend/components.json`):** `style: "new-york"`, `baseColor: "neutral"`, `iconLibrary: "lucide"`, `tsx: true`, `tailwind.cssVariables: true`, aliases `@/components`, `@/components/ui`, `@/lib`, `@/lib/utils`, `@/hooks`. The CSS-variable theme (`src/index.css`) is the single source for both shadcn structural tokens AND the project's `--status-*` semantic palette from `06_UI_DESIGN.md §3`. Adding a shadcn component must not overwrite the semantic block or introduce a competing colour system.
- **Layout shell composition:** the authenticated SPA is wrapped in `<AppShell>` = `<SidebarProvider>` → shadcn `<Sidebar collapsible="icon">` + `<SidebarInset>` containing the `<Topbar>` and `<main><Outlet /></main>`. `/login` is the only top-level route outside the shell; every other route is a child of the shell. Sidebar groups follow `06_UI_DESIGN.md §5`: **Operations** (Главная / Сеансы / Публикации), **Configuration** (Клиенты / Инфобазы), **System** (Аудит / Профиль). Pages that don't exist yet route to a shared `<ComingSoonPage titleKey>` placeholder rather than 404, so nav remains discoverable as Stage 2 fills in.
- **Sidebar nav primitive:** built on shadcn `<SidebarMenuButton asChild>` with a react-router `<Link>` child; active state derived from `useResolvedPath` + `useMatch` so the `data-active` attribute (and the icon-collapsed tooltip) stay in sync with the router. The custom helper lives at `frontend/src/components/layout/NavLinkItem.tsx` and is the only sanctioned way to add a sidebar entry — do not hand-roll new ones.
- **Self-service password change:** `POST /api/v1/auth/change-password` with `ChangePasswordRequest(CurrentPassword, NewPassword)`. Authenticated route only (no anonymous reset flow in v1). Inline validation enforces non-empty fields, `NewPassword.Length >= 12`, and `NewPassword != CurrentPassword`; `UserManager.ChangePasswordAsync` then handles the Identity policy. **No force-change-on-first-login.** The seed admin is encouraged to change the random seed password via `/profile`, but `AppUser.MustChangePassword` / middleware are explicitly deferred — they widened Stage 2 with no proportional safety gain because the seed password is already random and only printed to the warning log.
- **Identity error → ValidationProblem mapping:** `ChangePasswordAsync` translates Identity error codes (`PasswordMismatch`, `PasswordTooShort`, `PasswordRequiresDigit`, `PasswordRequiresUpper`, `PasswordRequiresLower`, `PasswordRequiresNonAlphanumeric`, `PasswordRequiresUniqueChars`) into a `ValidationProblem` keyed by the offending DTO field with **Russian** messages. Unknown codes fall through to the raw Identity description, also under `NewPassword`. This is the template for every future Identity-backed write (force-reset, invite flow, etc.).
- **Audit logging for auth actions is deferred to PR 2.2.** `AdminLoggedIn` / `AdminLoggedOut` / `AdminPasswordChanged` enum values exist in the plan but the writer (`IAuditLogger` + `AuditActionType` enum) ships in PR 2.2 along with the `AuditLog` schema refactor. PR 2.1 deliberately does not stub an audit call to avoid a dangling no-op.
- **Frontend ESLint scope:** shadcn-generated files (`src/components/ui/**`, `src/hooks/use-mobile.ts`) are ignored by ESLint. They are vendored library code; the repo owns the file but does not modify it, and project rules (e.g. `react-hooks/set-state-in-effect`) must not block CI on third-party copies.

## Stage 2 PR 2.2 — Tenants vertical + AuditLog writer (binding alongside ADR-10)

- **409 Conflict contract:** all conflict responses are `ProblemDetails`-compatible JSON with an extra **`code`** field carrying a machine-readable identifier (`NAME_DUPLICATE`, `TENANT_HAS_INFOBASES`, `NAME_DUPLICATE_IN_TENANT`, …). Codes live in `backend/src/MitLicenseCenter.Web/Endpoints/Problems.cs::ProblemCodes` and are the single source of truth for both backend builders and frontend handlers. Frontend reads `body.code` to localise the message and to highlight the offending form field (e.g. `NAME_DUPLICATE` → `setError("name", …)`). The human-readable `detail` is always Russian. New conflict situations MUST add a new `ProblemCodes.*` constant rather than reusing an existing one — frontend cannot disambiguate by string detail.
- **Domain enum serialisation:** `JsonStringEnumConverter` is registered globally in `Program.cs`. Domain enums (`AuditActionType`, `AuditReason`, future `InfobaseStatus`) serialise as **strings** in API payloads (e.g. `"TenantCreated"`, not `1`). The int values are an internal DB contract (`HasConversion<int>`) and are not exposed across the wire — frontend code receives enum names and looks up translations through `audit.actions.*` keys (added in PR 2.4).
- **Enum int stability:** explicit numeric assignments in `AuditActionType` / `AuditReason` are **frozen**. Re-using a number for a different action would corrupt historical AuditLog rows. Reserved gaps (`200`/`201`/`210`/`211`/`300`/`301`/`400`/`401`) are documented in the PR-2.2 plan and must not be filled with unrelated actions.
- **`IAuditLogger`:** interface in `MitLicenseCenter.Application/Auditing/`, implementation in `MitLicenseCenter.Infrastructure/Audit/` (`internal sealed`, exposed to test assembly via `InternalsVisibleTo`). Writer calls `AppDbContext.SaveChangesAsync` itself, so endpoint handlers don't need to coordinate transactions with audit. `TimeProvider` is taken from DI — singleton `TimeProvider.System` is registered in `Infrastructure.DependencyInjection`, tests inject a frozen clock. For mutations that **delete** their tenant, the audit row is written **before** the deletion so the FK reference is valid at write time (`SetNull` then handles the cleanup, see below).
- **`AuditLogs.TenantId` FK:** `ON DELETE SET NULL` (not `NoAction`, as the v0 plan suggested). Rationale: tenant deletion must succeed even when audit history references it; the description column preserves the human-readable name, while the FK link becomes `NULL`. Other entities that gain a `TenantId` FK in PR 2.3+ follow the same rule unless the entity is part of the tenant aggregate (`Infobases.TenantId` → `Restrict` because we want the 409 guard, not silent orphaning).
- **Tenant deletion guard:** PR 2.2 endpoint has a `TODO PR 2.3` placeholder for the `Infobases.AnyAsync` check that would emit `Problems.TenantHasInfobases()`. The check (and its test `TenantDeletionGuardTests`) lands in PR 2.3 once the `Infobases` table exists — until then DELETE is unconditional.
- **Frontend destructive confirmation:** delete dialogs use shadcn `AlertDialog` with `AlertDialogCancel` as the default focus target and require the user to **retype the entity name** into an `Input` before the destructive action enables. This matches `06_UI_DESIGN.md §7` "high-impact protection" and is the binding pattern for every future destructive UX (delete tenant, delete infobase, …).

## Stage 2 PR 2.3 — Infobases + Publications vertical (binding alongside ADR-2/ADR-4)

- **Aggregate boundary = (Infobase + Publication).** Создание инфобазы — единый `POST /api/v1/infobases` с обязательным вложенным `CreatePublicationRequest` (1-to-1 required). Удаление инфобазы — `DELETE /api/v1/infobases/{id}` без флагов: публикация уходит каскадом в той же транзакции. Прямого `POST /api/v1/publications` нет — публикация существует только как часть aggregate'а. Это упрощает Stage 2 (UI знает про один объект, а не два) и оставляет место Stage 3, где «unpublish from IIS» добавится отдельным шагом перед `db.SaveChanges`.
- **FK поведения:** `Infobase → Tenant = Restrict` (часть guard'а tenant-deletion, см. ниже), `Publication → Infobase = Cascade` (publication — часть aggregate'а инфобазы; SQL автоматически чистит хвост, отдельной guard'ы не нужно). EF InMemory-провайдер cascade не применяет — handler `InfobasesEndpoints.DeleteAsync` явно вызывает `db.Publications.Remove(publication)` до `db.Infobases.Remove(infobase)`, поэтому unit-тесты на InMemory ведут себя так же, как продакшен на MSSQL.
- **Tenant deletion guard активирован.** `TODO PR 2.3` из `TenantsEndpoints.DeleteAsync` закрыт: tenant с `Infobases.AnyAsync` возвращает `409 Conflict` с `code: TENANT_HAS_INFOBASES`. Guard срабатывает ДО записи в AuditLog — отказ удаления не оставляет следа в `AuditLogs`. Тест `TenantDeletionGuardTests` теперь полноценный.
- **Unique-per-tenant имя инфобазы:** EF индекс `IX_Infobases_TenantId_Name` уникальный по паре `(TenantId, Name)`. Два разных tenant'а могут иметь инфобазы с одинаковым именем («Бухгалтерия» у Acme и у Beta — норма), один tenant — нет (`409 Conflict`, `code: NAME_DUPLICATE_IN_TENANT`).
- **AuditLog для составных операций.** `IAuditLogger.LogAsync` из PR 2.2 сохраняет каждую запись отдельным `SaveChangesAsync`, поэтому aggregate-операции «инфобаза + публикация» пишут две audit-строки последовательно: например, при `CreateAsync` сначала `InfobaseCreated`, затем `PublicationCreated`, обе с одним `TenantId`. При `DeleteAsync` записи кладутся ДО `db.Remove`, чтобы FK был валиден на момент записи. **Отклонение от плана PR 2.3:** план просил «две записи одним SaveChanges»; в реальности — две последовательные SaveChanges. Менять API `IAuditLogger` ради «одной транзакции» в Stage 2 нет смысла — атомарность доменной операции уже обеспечена одним `SaveChanges` на `Infobase + Publication`, а аудит — линейный hot-path, у которого нет инвариантов «всё или ничего» внутри одной HTTP-операции.
- **AuditActionType: новые значения активированы.** Значения `InfobaseCreated=10/Updated=11/Deleted=12` и `PublicationCreated=20/Updated=21/Deleted=22`, зарезервированные в PR 2.2, теперь записываются writer'ом. Int-значения остаются заморожены (см. PR 2.2 binding).
- **Validation: VirtualPath + PlatformVersion.** `VirtualPath` обязан начинаться с `/` и не содержать пробелов; `PlatformVersion` соответствует regex `^\d+\.\d+\.\d{2}\.\d{4}$` (например, `8.3.23.1865`). Ошибки возвращаются под ключами `Publication.VirtualPath` / `Publication.PlatformVersion` — это позволяет frontend через react-hook-form ставить `setError` точечно. **`VrdCustomXml`** — опциональное `nvarchar(max)`, null/whitespace-only нормализуется в `null` при записи, чтобы не было «пустой строки vs null» дрейфа.
- **`PublicationsEndpoints` существует как side-API.** `GET`/`PUT /api/v1/publications/{id}` оставлен для точечного редактирования параметров публикации без открытия формы инфобазы (например, сменить `PlatformVersion` после апгрейда платформы). `POST`/`DELETE` намеренно отсутствуют — публикация создаётся и удаляется только через aggregate Infobase.
- **`InternalsVisibleTo MitLicenseCenter.Tests.Unit`** добавлен в `MitLicenseCenter.Web.csproj`. Handler'ы `TenantsEndpoints.DeleteAsync`, `InfobasesEndpoints.CreateAsync`, `InfobasesEndpoints.DeleteAsync`, а также regex-валидатор `InfobasesEndpoints.IsValidPlatformVersion` промоутнуты `private static` → `internal static`. Это снимает необходимость в WebApplicationFactory для проверки бизнес-логики (guard, conflict, cascade) — тестовый стек остаётся «handler + InMemory DbContext + fake HttpContext + capturing IAuditLogger», как у `AuditLoggerTests`.
- **UI: одна форма на aggregate.** `InfobaseFormDialog` — `max-w-2xl` shadcn `<Dialog>` с двумя секциями через `<Separator />`: «Инфобаза» (tenant selector, name, сервер БД, имя БД, cluster ID, статус) и «Публикация в IIS» (site, virtualPath, platformVersion, OData/HTTP флажки). Один submit на форму = один POST/PUT. **Tenant selector заблокирован в edit-mode** — перенос инфобазы между клиентами в Stage 2 не предусмотрен (потребует пересборки публикации и переноса VrdCustomXml; будет отдельная operation в Stage 3 при необходимости).
- **UI: status badge цвета.** `InfobaseStatus` → `06_UI_DESIGN.md §3` цветовая палитра: `Active` → success (emerald), `Maintenance` → warning (amber), `Suspended` → danger (rose). Закодировано напрямую в `InfobasesPage.statusBadgeClass` — отдельной обёртки `<StatusBadge>` пока нет (1 место использования; вынесем, когда появится второе).
- **EF migrations editorconfig:** в `Persistence/Migrations/.editorconfig` добавлено подавление `CA1861` — EF scaffold генерирует `new[] { "Col1", "Col2" }` в `CreateIndex(columns: …)`, что валит build при `TreatWarningsAsErrors`. Подавление точечное (только для миграций), общий код продолжает обязан использовать `static readonly` массивы.

## Stage 2 PR 2.4 — Audit page + Sessions snapshot stub (binding alongside ADR-3/ADR-6/ADR-10)

- **Audit endpoint = read-only с server-side pagination.** `GET /api/v1/audit?actionType=&tenantId=&from=&to=&page=&pageSize=` отдаёт `AuditPagedResponse(items, total, page, pageSize)`. Запрос всегда `AsNoTracking().OrderByDescending(x => x.Timestamp).ThenByDescending(x => x.Id)`, чтобы тай-брейк по Id давал стабильный порядок на записях с одинаковым timestamp (миграции PR 2.2 пишут `Timestamp.HasDefaultValueSql("SYSUTCDATETIME()")` — миллисекундный гранулярит достаточно, но не стопроцентен). `CountAsync` и `ToListAsync` идут последовательно — scoped `DbContext` не поддерживает параллельные операции.
- **`pageSize` whitelist `{25, 50, 100}`, default `50`.** Невалидное значение (включая `0`, отрицательное и «не из набора») молча падает на default — фронт не получает 400 за выбор размера, а лимиты остаются предсказуемыми для индексов `IX_AuditLogs_Timestamp` / `IX_AuditLogs_ActionType`. Сами размеры — константа `AllowedPageSizes` в `AuditEndpoints.cs`.
- **Валидация `actionType` и диапазона.** `actionType` парсится через `Enum.TryParse<AuditActionType>(value, ignoreCase: true, out _) && Enum.IsDefined(parsed)` — `IsDefined` блокирует «магические» числа в URL (`?actionType=999`). Несовместимый диапазон (`to < from`) → `400 ValidationProblem` с русским сообщением. Это единственные источники 400 на endpoint'е — всё остальное (пустые/невалидные `tenantId`, пустые `from/to`) трактуется как «без фильтра».
- **Sessions snapshot — заглушка для Stage 3.** `GET /api/v1/sessions/snapshot` возвращает `SessionsSnapshotResponse(items: [], capturedAt: UtcNow, tookMs: 0)`. Поля `SessionSnapshotEntry(SessionId, InfobaseId, TenantId, AppId, ConsumesLicense, StartedAt)` зафиксированы прямо сейчас — frontend строит UI против стабильного контракта до того, как Stage 3 подключит `ICluster1CClient`. Контракт идёт от ADR-3 (snapshot-based reconciliation) и ADR-6 (hot 3–5s / cold 20–30s polling cadence на стороне сервера; UI polling — отдельный 15s, см. `docs/05_UI_REQUIREMENTS.md`).
- **Frontend audit page — URL-state.** `AuditPage` хранит фильтры (`actionType`, `tenantId`, `from`, `to`, `page`, `pageSize`) в `?query=...` через `useSearchParams` react-router. Дефолтные значения опускаются из URL, чтобы shareable-link выглядел чисто. Изменение любого фильтра автоматически сбрасывает `page` в `1` — без этого вторая страница после смены `actionType` может оказаться пустой и пользователь «теряется».
- **Frontend filters → backend.** `<input type="date">` отдаёт `YYYY-MM-DD`; перед отправкой это превращается в полноценный ISO UTC: `from` → `T00:00:00Z`, `to` → `T23:59:59Z`. `useAuditLog` принимает уже преобразованные фильтры — это разделяет UI-состояние (URL) и wire-формат (backend), и оставляет место для полноценного DatePicker в Stage 4 (`docs/06_UI_DESIGN.md`) без переделки backend-контракта.
- **Frontend enum словарь.** `audit.actions.*` в `i18n/ru.json` содержит **полный набор** значений `AuditActionType` — backend отдаёт строковое имя (см. PR 2.2 binding), `t(`audit.actions.${entry.actionType}`)` даёт русский label. Если в Stage 3 enum пополнится (`SessionKilled`, `LimitChanged`, …), словарь и `AUDIT_ACTION_TYPES` константа должны обновляться синхронно.
- **Action-badge цвета** через локальный switch в `AuditPage.actionBadgeClass`: `*Created` → emerald (success), `*Updated` → sky (info), `*Deleted` → rose (danger), всё остальное → muted/neutral. Это соответствует семантике `06_UI_DESIGN.md §3` (5 status colors), и снова — без отдельного `<StatusBadge>` компонента до второго места использования.
- **Sessions page — Card-заглушка с подсветкой контракта.** UI-страница (`SessionsPage.tsx`) явно сообщает пользователю «реальный мониторинг — Stage 3», но **дёргает endpoint** через `useSessionsSnapshot` и показывает «Активных сеансов: {count}» + `capturedAt`. Это страховка: если кто-то сломает контракт `SessionsSnapshotResponse`, страница покажет ошибку загрузки или unexpected data, а не молча останется зелёной.
- **Sessions polling отключён.** `useSessionsSnapshot` использует `refetchInterval: false` — в Stage 2 endpoint статичен (всегда пустота), и крутить 15s-polling смысла нет. В Stage 3 polling включится одновременно с реальным adapter'ом (`docs/05_UI_REQUIREMENTS.md §4`).

## Stage 2 — closure

PR 2.4 закрывает Stage 2: layout shell + auth additions (PR 2.1), Tenants + AuditLog writer (PR 2.2), Infobases+Publications (PR 2.3), Audit page + Sessions stub (PR 2.4). Backend и frontend теперь покрывают всё, что можно сделать «всухую» — без реальных вызовов 1C/IIS. Stage 3 начинается с:
- `ICluster1CClient` (REST + Polly-circuit + RAS fallback) — заменяет stub `SessionsEndpoints.SnapshotAsync`.
- Snapshot writer + reconciliation Hangfire job (hot/cold двухтемповый цикл, ADR-3/ADR-6).
- `default.vrd` XML-patch service + drift-detection job + admin-driven Reconcile (ADR-4).
- Domain enum пополняется значениями `SessionKilled`/`LimitChanged`/`PublicationDriftDetected`/`PublicationReconciled`/`ClusterAdapterCircuitOpened`/`ClusterAdapterCircuitClosed`/`BackupCompleted`/`BackupVerified` — int-значения уже зарезервированы (PR 2.2), `i18n.ru.audit.actions.*` пополняется одновременно.
- `Publication` обзаводится drift-полями (`LastDriftStatus`, `LastDriftCheckAt`, `LastDriftDetails`) — отдельной миграцией.

## Stage 3 PR 3.1 — Settings + DPAPI (binding alongside ADR-8)

- **Каноническое хранение `dbo.Settings`.** Одна строка на ключ (`Key NVARCHAR(200) PK`). Plain payload идёт в `ValueText NVARCHAR(MAX) NULL`, секрет — в `Value VARBINARY(MAX) NULL` (DPAPI-зашифрованные UTF-8 байты). `IsSecret BIT NOT NULL` — write-side инвариант: при `IsSecret=true` write-side обязан положить пейлоад в `Value` и обнулить `ValueText`, иначе наоборот. Маскировка на read-side двойная: store вычищает `ValueText` для любого row с `IsSecret=true` даже если он туда случайно затесался (защита от drift). `Description NVARCHAR(500) NULL`, `UpdatedAt DATETIME2 NOT NULL`, `UpdatedBy NVARCHAR(256) NOT NULL` (имя инициатора или `"System"`).
- **DPAPI purpose-string `mlc.settings.v1`.** `IDataProtectionProvider.CreateProtector("mlc.settings.v1")`. Версия в purpose-string — намеренный hook: если когда-нибудь сменим encryption scheme (например добавим AEAD-обёртку), bump до `mlc.settings.v2` + миграция row'ов; старые key-ring файлы под `MitLicenseCenter/keys` остаются совместимы (ADR-8 unchanged).
- **`SettingDefinitions` catalog — единый источник правды.** Whitelist ключей живёт в `Application/Settings/SettingDefinitions.cs` как `IReadOnlyDictionary<string, SettingDefinition>`. Catalog диктует: имя ключа (через `SettingKey` const'ы), `IsSecret`, описание для UI, тип значения (`SettingValueKind = Text|Number|Url|HostPort|Path`), мин/макс для числовых, дефолт для plain. Endpoint валидирует против catalog'а; seeder сидит из него же; reflection над `SettingKey` сознательно не используем — catalog даёт богаче метаданные. **Добавить новый параметр = коммит в `SettingKey.cs` + запись в catalog**.
- **Validation rules:**
  - `Number` — `int.TryParse(..., InvariantCulture)` + диапазон.
  - `Url` — `Uri.TryCreate(..., UriKind.Absolute)` + scheme ∈ {http, https}.
  - `HostPort` — regex `^[^\s:]+:\d+$` + порт в `[1024, 65535]`.
  - `Path` / `Text` — non-empty (нормализация: whitespace → `null` = clear).
  - `null` / whitespace допустим для любого ключа — это «очистка значения», для секрета = «убрать пароль».
- **HTTP контракт.** `GET /api/v1/settings` (Admin) → `IReadOnlyList<SettingDescriptorResponse>`. Список включает **все** 14 ключей даже когда они не заданы (`isSet: false`). Секреты возвращаются с `value: null` независимо от наличия данных. `PUT /api/v1/settings/{key}` (Admin) с `{value: string|null}` → `200 Ok` при успехе, `404 NotFound` с `code: SETTING_UNKNOWN_KEY` если ключа нет в whitelist, `400 ValidationProblem` с `code: SETTING_INVALID_VALUE` и `errors.Value` под invalid value. Оба code'а живут в `ProblemCodes.cs`.
- **Audit description без plaintext.** `SettingChanged = 400` (был зарезервирован в PR 2.2, активирован сейчас). Для plain: `«Параметр {key} изменён.»`. Для секрета: `«Параметр {key} (секрет) обновлён.»`. **Никогда** не пишем значение в description — даже masked (это и регрессионный тест в `SettingsValidationTests`).
- **`ISettingsSnapshot` — singleton с TTL ≈ 30s, DI-port для hot-path читателей (PR 3.2/3.3/3.5).** В PR 3.1 не используется напрямую — endpoint и UI читают через `ISettingsStore`. Singleton тянет scoped `AppDbContext` через `IServiceScopeFactory` (паттерн ASP.NET Core docs). `ISettingsStore.SetAsync` вызывает `Invalidate()` после каждого write'а; всё равно TTL вычистил бы за ≤30s, но явный invalidate даёт детерминированную картину в smoke-сценариях. `GetString`/`GetInt` сами по себе — sync, blocking-call внутри `lock` при cache miss; на ~10 ключах это микросекунды, на hot-path-tick не больно.
- **Seeder идемпотентен.** `SettingsSeeder.EnsureSeededAsync` вызывается в `Program.cs` **после** `IdentitySeeder.EnsureSeededAsync` (миграции уже накатаны). Только инсёртит отсутствующие ключи, ничего не апдейтит. Plain с `DefaultValue` сеется со значением; секреты + plain без дефолта сеются «пустыми» (`Value=null, ValueText=null`, `IsSet=false`). При повторном запуске — лог `«Засеяно 0 новых параметров.»` (т.е. ничего, ENG сообщение из LoggerMessage не выводится). Стартовый лог при первом запуске — `«Засеяно 14 новых параметров.»`.
- **Frontend.** `<ProtectedRoute requireAdmin?: boolean>` генерализован (Viewer перенаправляется на `/`, не на `/login`). `/settings` route вложен в `<ProtectedRoute requireAdmin>`. Sidebar entry «Параметры» admin-gated через `useMe().roles.includes("Admin")`. `settings.*` namespace в `i18n/ru.json` (sections / labels / hints / actions / states / toasts / errors). `audit.actions.SettingChanged = "Параметр изменён"` + расширение `AUDIT_ACTION_TYPES` константы. Action-badge цвет `SettingChanged` — neutral muted (правило из PR 2.4: `*Created/Updated/Deleted` имеет цвет, остальное — muted; `SettingChanged` сюда попадает естественно).
- **Operational note.** Key-ring под `%ProgramData%\MitLicenseCenter\keys` критичен — без него `Value` байты в `dbo.Settings` нечитаемы. ADR-8 уже фиксирует включение этих ключей в daily backup (Stage 4 ADR-9 их и заберёт), но напоминание: удаление key-ring папки = безвозвратная потеря всех зашифрованных секретов (cluster admin password, RAS creds в будущем). Документировано в `docs/04_INFRASTRUCTURE.md`.
- **Не вошло в Stage 3 PR 3.1** (намеренно): любые backup-связанные ключи (вынесены в Stage 4 ADR-9), любые реальные читатели `ISettingsSnapshot` (PR 3.2/3.3/3.5), `Microsoft.Web.Administration` package (нужен в PR 3.5).

## Stage 3 PR 3.2 — 1С Cluster REST adapter + circuit breaker (binding alongside ADR-2/ADR-8)

### ADR-3.1 — 1С Cluster REST API endpoint contract

- **Source:** 1С:Предприятие Platform 8.3+ documentation, Remote Administration System (RAS) HTTP interface. Verified against platform versions 8.3.22–8.3.26 in development environment.
- **Base URL:** `http://{host}:{port}` where port is the Cluster Management HTTP port (default **1541**). Configured via `Settings.OneC.Cluster.RestApiUrl`. Example: `http://my-server:1541`.
- **Authentication:** HTTP Basic Authentication, credentials from `Settings.OneC.Cluster.AdminUser` / `Settings.OneC.Cluster.AdminPassword`.
- **Content-Type:** `application/json` (both request and response).

**Endpoint inventory:**

| Method | Path | Description |
|--------|------|-------------|
| `GET` | `/rm/cluster` | List all clusters. Used for connectivity ping and to resolve `clusterId` for subsequent calls. |
| `GET` | `/rm/cluster/{clusterId}/session` | List all active sessions in the cluster. |
| `DELETE` | `/rm/cluster/{clusterId}/session/{sessionId}` | Terminate a specific session. Returns `204 No Content` on success, `404 Not Found` if already gone. |

**Cluster list response (GET /rm/cluster):**
```json
[
  {
    "cluster": "9aba...",
    "host": "my-server",
    "port": 1541,
    "name": "Local cluster"
  }
]
```

**Session list response (GET /rm/cluster/{clusterId}/session):**
```json
[
  {
    "session": "d1e1ba1e-...",
    "infobase": "f45567c2-...",
    "user-name": "Иванов",
    "app-id": "1CV8C",
    "host": "WORKSTATION01",
    "started-at": "2024-01-15T08:30:00",
    "last-active-at": "2024-01-15T08:45:00",
    "hibernate": false,
    "license": { "present": true }
  }
]
```

**`app-id` values and license consumption:**
- `license.present == true` → `ConsumesLicense = true` (primary, server-authoritative).
- If `license` field absent: heuristic — `{"1CV8", "1CV8C", "WebClient", "Designer", "COMConnection"}` → `true`.

**Date format:** ISO 8601 without timezone offset (`"2024-01-15T08:30:00"`). Server stores local time; client treats it as UTC (single-node deployment assumption from Locked Operational Constraints). If deployment environment uses a non-UTC timezone, adjust parsing — flagged as a future-PR risk.

**Session kill strategy:** `OneCRestClusterClient.KillSessionAsync` calls `DELETE /rm/cluster/{clusterId}/session/{sessionId}`. Before calling, `ResilientClusterClient` delegates to `KillEnforcer` (PR 3.3), which re-fetches the snapshot and verifies `(ClusterInfobaseId, AppId, StartedAtUtc)` match before issuing the kill — idempotency per the Locked Operational Constraints.

**clusterId resolution:** `OneCRestClusterClient` calls `GET /rm/cluster` on every `ListActiveSessionsAsync` / `KillSessionAsync` call to resolve the first cluster ID. This is two HTTP calls per poll cycle but acceptable at the typical cadence (once per cold interval, once per hot interval). Single-cluster topology assumed — multi-cluster support deferred.

**Deviation from plan:** The plan mentioned "or `/hs/cluster/sessions`" as a possible alternative path. After consulting the 1C platform docs and community resources, the confirmed path prefix is `/rm/` (Remote Management namespace), not `/hs/` (HTTP Service namespace). `/hs/` is for user-defined HTTP services running inside infobases. Cluster management is in `/rm/`.

**Smoke test:** `Tests.Unit/Clusters/OneCRestClusterClientSmokeTests.cs` (Category=Smoke, CI-excluded) validates `PingAsync → Ok=true` against a real cluster URL from User Secrets.

### ADR-3.2 — Polly v8 circuit breaker configuration

- `FailureRatio = 1.0`, `MinimumThroughput = CircuitBreaker.FailureCount` (default 3), `SamplingDuration = 30s`, `BreakDuration = CircuitBreaker.ProbeIntervalSeconds` (default 60s).
- Values read once from `ISettingsSnapshot` when `ClusterCircuitState` singleton is constructed. Changes to settings require application restart to take effect.
- `OnOpened` callback writes `AuditActionType.ClusterAdapterCircuitOpened = 300` via `IServiceScopeFactory`-scoped `IAuditLogger`. `OnClosed` writes `301`. `OnHalfOpened` updates state only (no audit).
- `RemoveAllResilienceHandlers()` on `OneCRestClusterClient`'s `IHttpClientBuilder` removes the global `AddStandardResilienceHandler()` (Program.cs line 115) from this client — resilience policy is owned exclusively by `ResilientClusterClient` + `ClusterCircuitState`.

## Stage 3 PR 3.3 — Reconciliation + kill enforcer (binding alongside ADR-3/ADR-6)

### ADR-6.1 — Hot-tier polling lives in BackgroundService, not Hangfire

- **Decision:** Hot-tier polling (3–5s cadence for tenants at ≥90% license consumption) runs as a `BackgroundService` (`HotTierPollingService`), not as a Hangfire recurring job.
- **Rejected Alternative:** Hangfire recurring job with `"*/5 * * * * *"` (second-level cron). Not supported — Hangfire's minimum granularity is 1 minute.
- **Reason:** Hangfire CRON minimum = 1 minute. The hot tier requires 3–5 second cadence to minimize the enforcement window for near-limit tenants. Cold reconciliation remains in Hangfire (`"* * * * *"` with internal throttle to `ColdIntervalSeconds`) for dashboard visibility and durability. Kill enforcer runs only at the end of each cold cycle — hot polling updates the UI-facing snapshot but does not kill sessions.

### ADR-6.2 — SessionKilled=200 + AuditReason differentiator

- **Decision:** A single `AuditActionType.SessionKilled = 200` covers both automatic (limit enforcement) and manual (operator-initiated) kills. The distinction is in `AuditReason`: `LimitExceeded = 1` for automated kills by the enforcer, `ManualByAdmin = 2` for operator-initiated kills via `POST /sessions/{id}/kill`.
- **Rejected Alternative:** Separate `SessionKilledManual = 201` enum value. Rejected because it doubles the enum surface without adding filtering capability that `AuditReason` doesn't already provide.
- **Reason:** `LimitChanged = 201` is reserved for future tenant-limit-change audit, stabilizing the wire contract now. The description field in audit entries carries the human-readable context (operator reason, session details).

## Stage 3 PR 3.5 — Publication drift detection + IIS XML patcher (binding alongside ADR-4)

### ADR-4.1 — VRD surgical-patch and VrdCustomXml merge strategy

- **Decision.** `OneCIisPublishingService.ApplyDesiredStateAsync` mutates `default.vrd` via `XDocument` strictly in-place. The patch is the **only** allowed mutation on an existing file — `webinst` is never invoked on existing publications, the file is never overwritten wholesale (memory `infrastructure_integration.md`).
- **VRD path layout.** Resolved as `Path.Combine(Settings.IIS.DefaultVrdRoot, publication.SiteName, publication.VirtualPath.TrimStart('/'), "default.vrd")`. `IIS.DefaultVrdRoot` defaults to `C:\inetpub\1c-publications` and is operator-configurable from the Settings page. Per-publication overrides are **deferred to Stage 4** — if a test environment has a non-standard layout, the operator adjusts the IIS physical path or `IIS.DefaultVrdRoot`, not the schema.
- **Three surgical mutations performed by `VrdPatcher.Patch`:**
  1. **OData toggle.** `<standardOdata enable="true|false"/>` — attribute value flipped; if the node is missing it is created with only the `enable` attribute (operator can add OData-specific tuning via `VrdCustomXml`).
  2. **HTTP Services toggle.** `<httpServices publishByDefault="true|false"/>` — same in-place attribute mutation.
  3. **Platform version segment.** Any attribute value containing `wsisapi.dll` has its `\d+\.\d+\.\d+\.\d+` segment replaced with `publication.PlatformVersion`. The match is restricted to attribute values that literally contain `wsisapi.dll` so that no unrelated version-shaped numbers (e.g. comment text, custom version tags) get rewritten. If no `wsisapi.dll` path is present (newer 1C builds move the ISAPI handler to `web.config` next to `default.vrd`), the version patch is a silent no-op — the drift detector reads the platform version only if it could be parsed.
- **VrdCustomXml overlay strategy (the previously-open question from the plan).** Operator-supplied `VrdCustomXml` is wrapped in a transient pseudo-root inheriting the VRD namespace, then merged into the live document with a **replace-child-by-LocalName / append-if-missing** strategy:
  - Each child element of the overlay is matched against existing child elements of the VRD root by `XName.LocalName` (namespace-agnostic, because operators routinely write the overlay without an explicit `xmlns`).
  - **Replace** if a sibling with the same local name exists — the overlay element wins verbatim. Example: an overlay `<standardOdata enable="true" sessionMaxAge="60"/>` *replaces* the boolean-only toggle node, so operator tuning survives reconcile.
  - **Append** if no sibling matches — overlay content is added as a new child of the VRD root. Existing custom nodes that are *not* present in the overlay (e.g. `<openid>`, `<httpServicesPermissions>`) are **never** dropped. This is the cornerstone guarantee: reconcile cannot lose operator's custom configuration.
  - A malformed `VrdCustomXml` (parse failure) surfaces as `InvalidOperationException` from `VrdPatcher.Patch`, which the reconcile endpoint maps to `409 ProblemDetails` with `code: IIS_RECONCILE_FAILED` — no half-applied write.
- **Idempotency.** `Patch(xml, desired) == Patch(Patch(xml, desired), desired)`. The adapter compares the patched string with the original and writes the file only when it changed — preserving the file's mtime so operator diagnostics aren't polluted.
- **Atomic write.** Patched content is written to `default.vrd.mlc.tmp` first, then `File.Replace` swaps it into place. A crash mid-write cannot leave an empty or truncated VRD.
- **Drift audit semantics.** `DriftCheckJob` writes `AuditActionType.PublicationDriftDetected = 210` **only** on a status transition AND **only** when the new status is one of `{Drift, Missing, Error}`. `InSync → InSync` and `Drift → Drift` with identical details are silent. The `Drift → InSync` transition that results from a successful reconcile is audited by the reconcile endpoint as `PublicationReconciled = 211` — the drift job never writes `211`, the endpoint never writes `210`.
- **Test strategy.** The XML logic lives in `VrdPatcher` and `PublicationDriftDetector` as pure static helpers so unit tests exercise them without `ServerManager` or a filesystem. `OneCIisPublishingService` is `[SupportedOSPlatform("windows")]` and validated only by smoke tests against a real publication.

## Stage 3 PR 3.8 — Real RAS adapter (rac.exe wrapper)

### ADR-3.3 — 1С RAS `rac.exe` CLI contract

> **Numbering note.** The plan-file called this "ADR-3.2" but that slot was already taken by the Polly circuit breaker config (above). Renumbered to **ADR-3.3** under the new PR 3.8 section. Memory mirror in `decisions.md` carries the same number.

- **Source.** Live experiment against `rac.exe` v8.5.1.1302 on the local single-node test rig (1C:Enterprise 8.5 Server Agent x86-64 + manually-launched `ras.exe`). Verified against platform 8.5.1.1302; parser is intentionally permissive so 8.3.20–8.3.26 and minor 8.5.x bumps do not break it. The smoke test (`RacExecutableSmokeTests`) re-verifies on whatever platform the operator has installed.
- **Tool path.** Operator-configurable via `Settings.OneC.RAS.ExePath`. **Plan default `C:\Program Files\1cv8\common\rac.exe` was wrong** — 1C 8.5 keeps `rac.exe` inside the version-specific `bin\` directory (`C:\Program Files\1cv8\8.5.1.1302\bin\rac.exe`), and `1cv8\common\` no longer ships the utility. PR 3.8 therefore **drops the seeded default** from `SettingDefinitions.OneCRasExePath` (matches the no-default treatment of `OneC.RAS.Endpoint`) so a fresh install forces the operator to enter the version-specific path in the «Параметры» UI. Existing installs that already seeded the wrong path are unaffected by the schema change (seeder is idempotent) — the operator must override via the UI when the circuit first opens.
- **RAS endpoint.** Operator-configurable via `Settings.OneC.RAS.Endpoint` (default `localhost:1545`). Passed to `rac.exe` as the **first positional argument** (`rac.exe <host:port> <mode> <command> ...`). Omitting the positional defaults to `localhost:1545` inside `rac` itself; we always pass it explicitly so a misconfigured `Endpoint` surfaces as a connection error instead of silently hitting localhost.
- **Authentication.** Cluster admin creds (`Settings.OneC.Cluster.AdminUser` / `OneC.Cluster.AdminPassword`) are reused — `rac.exe` does not have a separate auth source. Passed as **two separate flags** `--cluster-user=<name>` `--cluster-pwd=<password>` (the plan's `--auth=<user>:<pwd>` syntax does not exist in `rac.exe`). When both are empty in settings (typical for a default cluster with no admin registered), the flags are omitted entirely — `rac` allows anonymous administration of clusters that have no registered administrators, exactly matching the live test rig's behaviour.
- **Encoding** (revised after smoke test on .NET `Process`). `rac.exe` writes stdout/stderr in the **parent process's active OEM code page**, NOT a fixed UTF-8 stream. The original belief — captured during the PowerShell-driven plan-mode experiment that showed clean UTF-8 bytes (`D0 A1 D0 B5 …` for «Сеанс») — was an artefact of PowerShell 7 / Windows Terminal silently activating `chcp 65001` for child processes. From a plain .NET test host or a Windows service running under `Network Service`, the active OEM CP on RU Windows is **866**, so the same stderr arrives as `91 A5 A0 AD E1 …` (CP866 for «Сеанс»). Setting `ProcessStartInfo.StandardOutputEncoding / StandardErrorEncoding = Encoding.UTF8` does **not** flip the child's output — it only tells the `StreamReader` how to interpret bytes that are already on the pipe, so the parent ends up with `U+FFFD` everywhere and the idempotent marker `«Сеанс с указанным идентификатором не найден»` fails to match. `SystemProcessRacRunner` therefore reads `process.StandardError.BaseStream` directly into a `MemoryStream` and decodes with `Encoding.GetEncoding(CultureInfo.CurrentCulture.TextInfo.OEMCodePage)` (registered via `CodePagesEncodingProvider.Instance` because .NET Core+ drops legacy code pages from the default provider). On RU Windows this yields CP866; on EN Windows it yields CP437; on locales where `OEMCodePage = 0` it falls back to UTF-8. **No `StandardOutputEncoding` / `StandardErrorEncoding` properties are set on `ProcessStartInfo`** — we own the decoding step end-to-end so the parent's `Console.OutputEncoding` cannot accidentally short-circuit our logic.
- **Output format = vertical key-value blocks**, NOT tabular. The plan's hoped-for `--result-format=tab` flag does **not** exist in any of the help screens. Real format:

  ```
  field-name-1                              : value
  field-name-2                              : "quoted value"
  field-name-3                              :
  field-name-4                              : 123
  ⏎  (blank line ⇒ record separator)
  field-name-1                              : <next record>
  ...
  ```

  Field names are kebab-case ASCII; the colon is preceded by spaces (right-padded for visual alignment) and followed by exactly one space then the value. String values may be **unquoted** (when they contain no whitespace) or wrapped in double quotes (when they contain spaces — e.g. `name : "Локальный кластер"`). Empty values are an empty string after the colon-space (with trailing whitespace ignored). Line terminators are CRLF (`\r\n`). Records are separated by **one or more** blank lines.

- **Parser strategy** (`RacOutputParser`, pure static helper). Defensive regex over the line shape `^\s*(?<key>[a-z0-9-]+)\s*:\s?(?<value>.*?)\s*$`. Records split on consecutive blank-only lines. Unknown keys ignored. Missing required keys (`session`, `infobase` for sessions; `cluster` for clusters) → record dropped + logged at Debug. Empty stdout → empty list (NOT an error). String values unquoted by stripping a matched pair of leading/trailing `"`; embedded quotes are not expected in 1C identifiers, so no escape handling. **Never throws** — parser failure on an unexpected line is a Debug-level skip, not an exception.

- **Commands used by the adapter.**

  | Adapter method | Invocation | Notes |
  |---|---|---|
  | `PingAsync` | `rac.exe <ras-endpoint> cluster list` | Exit 0 + at least one cluster record → `Ok=true`. Non-zero exit → `Ok=false`, `Error = stderr.Trim()`. |
  | `ListActiveSessionsAsync` | `rac.exe <ras-endpoint> session list --cluster=<uuid> [--cluster-user=<u> --cluster-pwd=<p>]` | `<cluster-uuid>` is resolved from a prior `cluster list` call in the same invocation cycle (cached for the duration of one `ListActiveSessionsAsync`, NOT across cycles — operator can recreate the cluster between cycles). Single-cluster topology assumed (mirrors REST adapter). |
  | `KillSessionAsync` | `rac.exe <ras-endpoint> session terminate --cluster=<uuid> --session=<session-uuid> [--cluster-user=<u> --cluster-pwd=<p>] --error-message="<reason>"` | The kill command is **`session terminate`**, not the plan's `session kill` (which does not exist). `--error-message` carries the operator's reason verbatim (`ManualByAdmin`) or a system tag (`LimitExceeded`) — 1C displays this string to the user being kicked. |

- **Field mapping (session record → `ClusterSession`).**

  | `ClusterSession` property | `rac` field | Notes |
  |---|---|---|
  | `SessionId` | `session` | UUID. This is the kill target. |
  | `ClusterInfobaseId` | `infobase` | UUID. Matches `Infobase.ClusterInfobaseId` in our schema (same as REST). |
  | `AppId` | `app-id` | String. Used for `ConsumesLicense` heuristic. |
  | `UserName` | `user-name` | String. May be quoted. |
  | `Host` | `host` | String. |
  | `StartedAtUtc` | `started-at` | ISO `YYYY-MM-DDTHH:MM:SS` (no timezone suffix). Same as REST: parsed with `DateTimeStyles.AssumeUniversal | AdjustToUniversal`. Single-node deployment runs the cluster in the same timezone as the backend; if the deployment ever splits across timezones, this flag-day becomes a known footgun (same caveat as REST adapter — see ADR-3.1). |
  | `ConsumesLicense` | derived | The default `session list` output does **NOT** include a `license-present` field (plan assumed it would). The `--licenses` flag changes the output shape entirely and is unsuitable for our parser. Fall back to the same `app-id` heuristic as the REST adapter: `{1CV8, 1CV8C, WebClient, Designer, COMConnection}` → `true`, anything else (e.g. `BackgroundJob`, `Job`, `SrvrConsole`) → `false`. Heuristic is shared with `OneCRestClusterClient.LicenseConsumingAppIds` via a single `static readonly HashSet`. |

- **Error semantics (exit codes verified empirically — `rac` returns **non-zero = 255** for every failure path):**

  | Scenario | stderr (Russian, UTF-8) | Adapter behaviour |
  |---|---|---|
  | success | (empty) | exit 0, parse stdout |
  | unreachable RAS host/port | `Ошибка соединения с сервером\nПодключение не установлено, т.к. конечный компьютер отверг запрос на подключение` | `ListActiveSessionsAsync` → empty list + Warning log. `PingAsync` → `Ok=false`, `Error=stderr trimmed`. Circuit-breaker (owned by REST side) keeps the circuit open. |
  | unknown cluster UUID | `Сервер <hostname> не является центральным для кластера <uuid>.` | Same as above — empty list + warning. Surface to operator via Settings page if circuit stays open >5min. |
  | terminate: session not found | `Сеанс с указанным идентификатором не найден` | `KillSessionAsync` → `Killed=false, AlreadyGone=true` (idempotent — matches REST `404` semantics). Detection via `stderr.Contains("Сеанс с указанным идентификатором не найден")` — case-sensitive substring match, fine for current 1C platform. |
  | terminate: malformed UUID arg | `Ошибка разбора параметра: session` | `KillSessionAsync` → `Killed=false, AlreadyGone=false` + Error log. Should never happen — `KillEnforcer` always passes a UUID it pulled from a parsed snapshot. |

- **Process lifecycle.** Adapter spawns `rac.exe` via `System.Diagnostics.Process` with `UseShellExecute=false`, `RedirectStandardOutput=true`, `RedirectStandardError=true`. Wraps the entire invocation in `CancellationToken.Register(() => process.Kill(entireProcessTree: true))`. **`entireProcessTree: true`** is intentional — `rac` may spawn a transient child for the gRPC dialog with RAS; killing only the parent leaves the child as an orphan that eventually times out. Settings.OneC.Cluster.RestApiTimeoutSeconds is *not* reused here — instead the adapter uses a fixed 30-second deadline per invocation (longer than REST because the rac → ras → ragent chain has more hops). If the deadline expires, the `CancellationToken` fires, the process tree is killed, and the call returns the same way as a non-zero exit.

- **Process abstraction = `IRacProcessRunner`** (`Infrastructure/Clusters/IRacProcessRunner.cs`). Hides `System.Diagnostics.Process` behind a `Task<RacInvocation>` API (`RacInvocation(int ExitCode, string Stdout, string Stderr)`) so `RacExecutableRasClusterClient` is unit-testable by substituting a fake runner. The real implementation (`SystemProcessRacRunner`) is the only thing that actually shells out. Unit tests pass canned `RacInvocation` instances; smoke tests exercise `SystemProcessRacRunner` directly against the live `rac.exe`.

- **Spawn cadence guard rail (binding).** Adapter spawns **at most one** `rac.exe` per `ListActiveSessionsAsync` and **at most one** per `KillSessionAsync`. The cluster-UUID resolution (`cluster list`) and the session list happen **inside a single process invocation** by chaining stdout — wait, no, they don't: `rac` is one command per process. So the adapter caches the resolved `clusterUuid` for the duration of the *current `ListActiveSessionsAsync` call only* and emits two processes per call (one `cluster list`, one `session list`). At the cold cadence of 20–30s this is six processes per minute worst-case (3 list cycles × 2 procs) — well below the "no rac.exe per polling tick" memory rule, which exists to forbid per-session spawning. Kill enforcer adds **one extra `rac.exe`** per session killed; capped at 20 kills/cycle (PR 3.3 `KillEnforcer`) → worst-case 26 invocations per minute under sustained over-quota conditions. Plan-strategy B (long-lived TCP socket on 1545) remains deferred to Stage 4 — see ROADMAP.

- **Source field propagation (decorator wiring sanity check).** `ResilientClusterClient` populates `SnapshotPayload.Source` indirectly via `ICircuitStatusReader.GetStatus().ActiveAdapter` in `ReconciliationJob`. `ActiveAdapter` already computes `"Ras"` when the circuit is `Open` and `"Rest"` otherwise (`ClusterCircuitState.GetStatus`). PR 3.8 does **not** introduce a new mechanism — it just makes the `"Ras"` branch produce real sessions instead of an empty stub. Dashboard cluster card and `/sessions/snapshot.source` therefore start showing `"Ras"` automatically once the circuit opens. Smoke test confirms the end-to-end loop.

- **`StubRasClusterClient` retained for tests.** Moved from `Infrastructure/Clusters/` to `Infrastructure/Clusters/Testing/` and kept `internal sealed`. Existing PR 3.2/3.3 unit tests (`CircuitBreakerTransitionTests`, `SessionsSnapshotProjectionTests`, etc.) that referenced it directly continue to compile thanks to `InternalsVisibleTo MitLicenseCenter.Tests.Unit` already in `MitLicenseCenter.Infrastructure.csproj`. The stub is **not** registered in DI any longer — production wiring is `services.AddScoped<IRasFallbackClusterClient, RacExecutableRasClusterClient>()`.

- **Test strategy.**
  - `Tests.Unit/Clusters/RacOutputParserTests.cs` — `[Theory]` with `[InlineData]` containing the captured stdout from this very experiment (single-record, multi-record, empty, malformed-line skipped, unknown-key tolerated, quoted-and-unquoted strings, idle infobase with no sessions). No `IRacProcessRunner`, no `Process` — pure string-in, list-out.
  - `Tests.Unit/Clusters/RacExecutableRasClusterClientTests.cs` — uses a fake `IRacProcessRunner` returning canned `RacInvocation`s. Covers: cluster-list-then-session-list happy path, `cluster list` failure short-circuits `ListActiveSessionsAsync` to empty, `session terminate` "session not found" stderr → `AlreadyGone=true`, cancellation token threading.
  - `Tests.Unit/Clusters/RacExecutableSmokeTests.cs` `[Trait("Category","Smoke")]` — talks to a real `rac.exe` against a real `ras.exe` on `localhost:1545`. Asserts `PingAsync.Ok == true` and `ListActiveSessionsAsync` returns at least one session when a `BackgroundJob` is running. CI excludes via `--filter Category!=Smoke`.

- **Service-account requirements (operational, mirrored in `docs/04_INFRASTRUCTURE.md`).** The backend service account needs (1) **execute** permission on the resolved `rac.exe` path, (2) **read** access to the `1cv8\<version>\bin\` directory so co-located DLLs (`backend.dll`, `nlsoft.dll`, etc.) load, (3) **network reach** to `Settings.OneC.RAS.Endpoint` (default `localhost:1545`). `Network Service` works out-of-the-box on a stock single-node install where `1cv8` is installed under `C:\Program Files\` — the default ACLs grant `Users` Read+Execute. Custom service accounts (locked-down domain users) may need explicit grant on `1cv8\<version>\bin\rac.exe`.

## Locked Operational Constraints (not full ADRs, but binding)

- **Kill priority:** when `Consumed > Limit`, kill sessions ordered by `StartedAt DESC` (newest first) until `Consumed == Limit`. Justification: simplest to explain to end users ("you just logged in and were dropped because the quota was already full") and avoids interrupting in-progress work of established sessions.
- **Idempotent kill protocol:** before issuing a kill, the 1C Cluster Adapter re-fetches the session by ID and verifies `(InfobaseId, AppID, StartedAt)` match the snapshot. Mismatch → skip and wait for the next cycle. A `404 / session not found` response from the cluster is treated as a successful kill (idempotency). Every kill writes an `AuditLog` entry with the reason (`LimitExceeded` / `ManualByAdmin`) and the snapshot context.
- **Drift detection:** a Hangfire job runs every 5 minutes, compares each `Publication`'s desired state against the actual `default.vrd` + IIS state, and writes `LastDriftStatus` / `LastDriftCheckAt`. An on-demand `POST /api/v1/publications/{id}/check-drift` is available from the UI. **Drift is never auto-corrected** — the admin must click "Reconcile" explicitly. Both detection and reconcile actions are audited.
- **REST → RAS failover:** the 1C Cluster Adapter uses a Polly-based circuit breaker. Three consecutive REST failures or timeouts open the circuit and route subsequent calls to the RAS adapter. A background probe retries REST every 60 seconds; on success the circuit closes. State transitions are written to `AuditLog`.
- **Deployment topology:** single-node — the .NET backend, IIS (publications), MSSQL, and the 1C cluster all run on the same Windows Server. No remoting, no WinRM, no cross-host adapters. If this assumption changes, all infrastructure adapters need a re-review.
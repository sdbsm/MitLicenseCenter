# UI & Frontend Requirements

This document outlines the architecture and key views for the web-based administrative Control Panel. 
The UI is strictly separated from the infrastructure logic and communicates ONLY with the Backend via REST API.

## Language & Localization

The Control Panel is delivered in **Russian** as the only shipping locale of v1 (target audience — Russian-speaking 1C hosting administrators; see `01_PROJECT_CONTEXT.md`).

- All visible strings — labels, buttons, table headers, tooltips, validation, toasts, dialogs, empty states, error pages — are in Russian.
- Strings are stored in a single `src/i18n/ru.json` and accessed via `react-i18next`. User-facing copy is routed through i18n rather than hardcoded in JSX, so a future locale can slot in without component edits. (One deliberate exception: the product name "MitLicense Center" is a literal in the topbar, not a translation key.)
- Locale: `ru-RU`. Absolute dates/times are formatted with **`date-fns`** + `date-fns/locale/ru` (see the stack table in `06_UI_DESIGN.md`); relative "N сек назад" freshness uses `Intl.RelativeTimeFormat('ru')`. Use these existing helpers — do not roll a custom formatter.
- Date input controls accept and display `ДД.ММ.ГГГГ`; under the hood always serialize as ISO-8601 UTC.
- The backend's user-facing error payloads are already in Russian (per `01_PROJECT_CONTEXT.md`) — the SPA does not translate them, just displays them.

## 1. Frontend Technology Stack
- **Framework:** React (Single Page Application).
- **Language:** TypeScript.
- **State & Data Fetching:** React Query (or similar tool) for caching, background refetching, and polling.
- **Component library:** **shadcn/ui** on top of **Radix UI**, styled with **Tailwind CSS** (see ADR-11). Mixing in other component libraries (MUI, Ant Design, etc.) is not allowed — if a control is missing, build it on top of Radix + Tailwind in the same style. Detailed visual language, status semantics, table patterns, and Russian microcopy are defined in `06_UI_DESIGN.md`.
- **Constraint:** Desktop applications, ASP.NET MVC, and Blazor MUST NOT be used.

## 2. UI Architecture & Communication
- The React application is completely decoupled from the .NET backend.
- It consumes a unified REST API provided by the Backend, versioned at `/api/v1/...` (see ADR-10). OpenAPI/Swagger UI is exposed at `/api/docs`; the TypeScript API types are hand-written in `frontend/src/features/<feature>/types.ts` and kept in sync with that spec by hand (see ADR-10.1) — there is no codegen step. On a few **critical response boundaries** (`/auth/me` + `/auth/login` — role gating; `/sessions/snapshot`; the paginated list envelopes for tenants and infobases) the response is additionally validated at runtime with a Zod schema, and the TS type is derived from that schema (`z.infer`) so there is a single source of truth; a contract mismatch there raises a controlled `ApiSchemaError` rather than a silent wrong type (MLC-016). All other endpoints keep the plain `api<T>()` cast.
- **Polling:** For live monitoring (like active sessions), the UI will poll specific "Snapshot" endpoints periodically (e.g., every 15 seconds) rather than maintaining complex WebSocket connections, keeping the architecture simple and scalable.
- **Authentication:** Cookie-based session auth via ASP.NET Core Identity (see ADR-7). The SPA hits `POST /api/v1/auth/login` with username/password, the server sets an HttpOnly cookie, and subsequent requests are authenticated automatically. Logout clears the cookie. Unauthenticated requests to protected endpoints return `401` and the SPA redirects to the login screen. The `Viewer` role hides destructive UI controls (kill, publish, change platform, edit) and the backend additionally enforces role checks server-side.

## 3. Key Views / Pages

### 3.1. Main Dashboard
- High-level overview of the hosting infrastructure.
- **Metrics:** Total Tenants, Total Active Sessions, Total Consumed Licenses vs Global Allowed Limits.
- **Health:** Server status, RAS adapter health card (rac.exe reachable + last error + consecutive-failures count, backed by `RasHealthProbingService`), latest snapshot freshness.

### 3.2. Tenants Management
- List of all Clients. Each row shows a **«Базы»** count (`InfobaseCount` from the list API); the client **name is a link** to the tenant detail page (`/tenants/:id`) — navigation is an explicit affordance, not a whole-row click (per the table convention in `06_UI_DESIGN.md`).
- Form to create/edit a Tenant (Name, `MaxConcurrentLicenses`, Status).
- **Tenant detail page (`/tenants/:id`)** — the per-client lens: header with client name, status and license limit, a **«Добавить базу»** button (opens the infobase form with the client pre-selected), and a table of that client's infobases (no «Клиент» column). Edit / delete / **«Перенести в другого клиента»** act per row.

### 3.3. Infobases & Publications
- List of all Infobases discovered in the 1C Cluster.
- **View modes:** a flat list (paginated, with a «Клиент» column) and a **«По клиенту»** grouped mode — collapsible sections headed by client name + base count. Toggle sits next to the tenant filter.
- Assignment interface: Attach an Infobase to a specific Tenant.
- **Reassignment:** the per-row **«Перенести в другого клиента»** action moves a base to another tenant (`POST /infobases/{id}/reassign`). The client field in the edit form stays locked — moving ownership is this dedicated action only. A name collision in the target client surfaces as a clear error (`409 INFOBASE_NAME_TAKEN_IN_TARGET`).
- **Add/Edit form is intentionally minimal.** The always-visible part has three fields only: **Клиент** (tenant), **База в кластере 1С** (the cluster infobase picked by name — its GUID is hidden, and picking it auto-fills the display name; the picker lists every cluster base and the form checks the picked one's availability up-front via `GET /api/v1/infobases/cluster-id-availability`, flagging «уже привязана к клиенту …» without loading the whole infobase list), and **Имя базы данных (SQL Server)**. Everything that is normally identical across all bases lives in a collapsed **«Дополнительно»** disclosure: display-name override, SQL server, IIS site, virtual path, platform version, physical-path override, status.
- The «Дополнительно» fields are pre-filled from the form-prefill settings (`Defaults.DatabaseServer`, `IIS.DefaultSiteName`, `OneC.DefaultPlatformVersion` — see `04_INFRASTRUCTURE.md`). The virtual path is derived from the database name as `"/" + slug(databaseName)`, and the physical path as `{IIS.DefaultVrdRoot}\{databaseName}` (default root `C:\inetpub\wwwroot`, e.g. `C:\inetpub\wwwroot\acme_bp`) — no IIS-site segment. Both are pre-filled as real values (not just placeholders) and stay editable; editing one stops it auto-updating. The disclosure auto-expands when a validation error lands on one of its fields.
- Inside «Дополнительно» the fields are grouped under subsystem sub-headings so the operator can see what each belongs to: **«Инфобаза»** (display name, status), **«СУБД (SQL Server)»** (database server), **«Публикация в IIS»** (site, virtual path, platform version, physical path). The always-visible «Имя базы данных (SQL Server)» field carries the same SQL-subsystem hint. The Settings page mirrors this: section titles read «Кластер 1С (RAS)» / «Публикации IIS», and the prefill-defaults labels name their subsystem.
- **Publication Settings:** View/Edit publication parameters (Site Name, Virtual Path, 1C Platform Version, physical-path override). OData / HTTP-services / custom VRD are **not** panel-managed (ADR-4) — configured in Designer / a `-descriptor` template.
- **Status:** Shows `LastCheckStatus` (`Published` / `NotPublished` / `Error` / `Unknown`), `LastCheckAt`, and `LastCheckDetails` (hover). A **«Источник»** column shows provenance (`webinst` / `Конфигуратор` / `—`). Updated by the background status-refresh job (every 5 min) plus an on-demand **«Проверить сейчас»** button (`POST /api/v1/publications/{id}/check`, read-only).
- **«Опубликовать»** — runs `webinst` (`POST /api/v1/publications/{id}/publish`). Confirmation dialog (type the site+path token); if the publication's source is not `Webinst` it warns that manual config will be overwritten. Audited as `PublicationPublished`.
- **«Сменить платформу»** — picks an installed version and rewrites the `wsisapi.dll` path in `web.config` (`POST /api/v1/publications/{id}/change-platform`, `default.vrd` untouched). Audited as `PublicationPlatformChanged`. Both actions are explicit, admin-only — nothing auto-corrects.

### 3.4. Active Sessions Monitor (The Kill Switch)
- A combined table displaying current `ActiveSessionSnapshot` data.
- **Columns:** Tenant Name, Infobase Name, Session ID, AppID, Duration, `ConsumesLicense` flag.
- **Actions:** Administrators can manually select a session and click "Terminate", which triggers an API call to the backend to kill the session via `rac.exe session terminate`.

### 3.5. Audit Logs
- A read-only, filterable table displaying the `AuditLog` entity data.
- Shows who (or what background job) killed a session, updated a publication, or changed a limit.
- Filters: by `ActionType`, `Initiator`, `TenantId`, date range. For `SessionKilled` rows, the `Reason` column distinguishes `LimitExceeded` from `ManualByAdmin`.

### 3.6. Administrators — **planned, not implemented in v1**
There is **no administrator-management screen, route, or sidebar entry in v1.** Account handling today: a single `Admin` user is seeded at startup (`IdentitySeeder`, password logged once), every user changes their own password via the Profile page (`/profile`), and the `Admin`/`Viewer` roles are assigned in the database directly. The design below is the intended future shape, to be promoted via its own backlog item before it is built:
- List of admin accounts (`Admin` and `Viewer` roles), last login timestamp, lockout status.
- Create / disable / reset-password actions (the latter generates a temporary password printed to the audit log; user must change it on next login).
- All administrator-management actions written to `AuditLog`.
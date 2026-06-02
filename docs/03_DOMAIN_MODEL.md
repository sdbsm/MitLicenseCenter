# Domain Model & Core Entities

This document defines the core domain entities, their attributes, and relationships. The data is persisted in a relational database (MSSQL), except for transient snapshot data.

## 1. Tenant (Client)
Represents a customer who rents the 1C infrastructure.
- `Id` (Guid, PK)
- `Name` (String)
- `MaxConcurrentLicenses` (Int): The strict upper limit of licenses this tenant is paying for.
- `IsActive` (Boolean)
- `CreatedAt` (DateTime)
- **Relationships:** Has Many `Infobases`. Has Many `AuditLogs`.

## 2. Infobase (1C Base)
Represents a specific 1C database assigned to a Tenant.
- `Id` (Guid, PK)
- `TenantId` (Guid, FK) — `ON DELETE RESTRICT`. Удаление tenant'а блокируется guard'ом endpoint'а (`409 Conflict`, `code: TENANT_HAS_INFOBASES`) до того, как SQL поднимет FK violation. Привязка задаётся при создании; сменить владельца можно только явным `POST /api/v1/infobases/{id}/reassign` (PUT/форма редактирования клиента не меняют).
- `Name` (String, ≤200): Friendly name for the UI. Unique **per tenant** — composite index `IX_Infobases_TenantId_Name`. Два разных клиента могут иметь одноимённые инфобазы; внутри клиента — нет.
- `ClusterInfobaseId` (Guid): The internal ID of this base inside the 1C Cluster. Globally **unique** — `IX_Infobases_ClusterInfobaseId`. Одна база кластера принадлежит ровно одному клиенту; повторная привязка (к тому же или другому клиенту) отклоняется `409 INFOBASE_ALREADY_ASSIGNED`. Форма добавления/редактирования показывает все базы кластера и проверяет занятость выбранной точечно через `GET /api/v1/infobases/cluster-id-availability` (см. API-контракты ниже) — без выгрузки всего списка инфобаз.
- `DatabaseServer` (String, ≤200): SQL Server instance name.
- `DatabaseName` (String, ≤200): SQL Database name.
- `Status` (Enum `InfobaseStatus`): `Active=0`, `Maintenance=1`, `Suspended=2`. Сохраняется как `int` (`HasConversion<int>`), на wire идёт строкой через `JsonStringEnumConverter`. Int-значения заморожены.
- `CreatedAt` (DateTimeUtc).
- `UpdatedAt` (DateTimeUtc, Nullable): обновляется в PUT-handler'е.
- **Relationships:** Belongs to `Tenant`. Has One required `Publication`. Создание/удаление инфобазы оперирует aggregate'ом — публикация создаётся в том же POST и каскадно удаляется при DELETE.

## 3. Publication (IIS Configuration)
Stores the *Desired State* of the IIS publication for a specific Infobase.
- `Id` (Guid, PK)
- `InfobaseId` (Guid, FK, **unique**) — `ON DELETE CASCADE`. 1-to-1 required: каждая инфобаза имеет ровно одну публикацию, удаление инфобазы каскадом сносит публикацию в БД.
- `SiteName` (String, ≤200): IIS Site (e.g., "Default Web Site").
- `VirtualPath` (String, ≤200): The URL path (e.g., "/tenant-base1"). Валидация: должен начинаться с `/`, не содержит пробелов.
- `PlatformVersion` (String, ≤50): e.g., "8.3.23.1865" или "8.5.1.1302". Used to locate `wsisapi.dll`. Валидация: regex `^\d+\.\d+\.\d+\.\d+$` — четыре числовых сегмента, длины не фиксируем (1С 8.5 ранние сборки имеют одноцифровой build).
- `EnableOData` (Boolean): Flag to manage standard OData interface.
- `EnableHttpServices` (Boolean).
- `VrdCustomXml` (`nvarchar(max)`, Nullable): Stores any custom XML fragments for `default.vrd` to ensure idempotency and prevent overwrite of custom configurations. Пустая строка / whitespace нормализуется в `NULL` при записи.
- `CreatedAt` (DateTimeUtc).
- `UpdatedAt` (DateTimeUtc, Nullable).
- `LastDriftStatus` (Enum): `InSync`, `Drift`, `Missing`, `Error`. Updated by the drift-detection job.
- `LastDriftCheckAt` (DateTimeUtc, Nullable).
- `LastDriftDetails` (String, Nullable).
- `PhysicalPathOverride` (`NVARCHAR(260)`, Nullable): override физической папки IIS-приложения. Если задан — `VrdPathResolver` использует `{PhysicalPathOverride}\default.vrd` вместо convention `{IIS.DefaultVrdRoot}/{siteName}/{virtualPath}/default.vrd`. NULL/empty → fallback на convention. Принимается только абсолютный путь (local `C:\...` или UNC `\\server\share\...`); relative paths отклоняются с 400.
- **Relationships:** Belongs to `Infobase`.

## 4. AuditLog
Immutable record of all critical system and administrator actions.
- `Id` (Guid, PK)
- `TenantId` (Guid, FK, Nullable): If the action relates to a specific tenant.
- `ActionType` (Enum): e.g., LimitChanged, PublicationUpdated, PublicationDriftDetected, PublicationReconciled, SessionKilled, InfobaseCreated, InfobaseReassigned (`=13`, пишется reassign-endpoint'ом с `tenantId` целевого клиента), AdminLoggedIn. _Reserved historical:_ `ClusterAdapterCircuitOpened=300`, `ClusterAdapterCircuitClosed=301` — enum values stay so old AuditLog rows render; new rows with these values are not written (the circuit breaker was removed, see ADR-16).
- `Reason` (Enum, Nullable): For `SessionKilled` only — `LimitExceeded` (automatic enforcer) or `ManualByAdmin` (operator via the kill endpoint). A `SessionKilled` row is written **only** when the kill actually succeeded — `KillSessionResult.Killed` or `.AlreadyGone` (idempotent "already terminated"). A failed kill (RAS down) writes **nothing**: the immutable log never records a kill that did not happen (see the binding "Manual session kill" contract below and `DECISIONS.md` «Idempotent kill protocol»).
- `Description` (String): Human-readable details, including snapshot context for kills.
- `Timestamp` (DateTimeUtc)
- `Initiator` (String): ID of the Admin, or "System" for background jobs.

**Retention.** Записи удаляются daily Hangfire job'ом старше `Settings.Audit.RetentionDays` (default 365, диапазон [30, 3650]). DELETE-only — никакого archival tier'а. Job пишет один `AuditLogsPurged=500` row на каждый non-empty purge (initiator="System"). Индекс `IX_AuditLogs_Timestamp` (single-column) обслуживает DELETE без дополнительных индексов.

## 5. ActiveSessionSnapshot (Transient State)
*Note: This entity is NOT permanently stored in MSSQL to avoid DB bloat. It represents the in-memory state captured during the Reconciliation Loop.*
- `SessionId` (Guid): The 1C cluster session ID.
- `InfobaseId` (Guid, FK)
- `TenantId` (Guid, FK)
- `AppID` (String): Type of client (e.g., WebClient, BackgroundJob, Designer).
- `ConsumesLicense` (Boolean): Calculated field. True if `AppID` type actively consumes a server license.
- `StartedAt` (DateTimeUtc)

## 6. AdminUser (ASP.NET Core Identity)
Local administrator account, managed via the ASP.NET Core Identity framework. Tables are created by Identity migrations under the `auth` schema and are not modified by hand.
- `Id` (Guid, PK)
- `UserName`, `NormalizedUserName`
- `PasswordHash` (Identity-managed; PBKDF2 by default)
- `Email`, `NormalizedEmail` (optional)
- `TwoFactorEnabled` (Boolean) — Identity-stock column; operationally inert per ADR-15.
- `LockoutEnd`, `AccessFailedCount` — Identity-managed brute-force protection
- **Role assignment:** `Admin` (full access, including manual session kill and publication reconcile) or `Viewer` (read-only). Implemented via the standard Identity role tables.
- The first admin account is seeded by the initial migration with a random password printed to the service log.

## 7. Setting (Encrypted Configuration)
Holds runtime configuration values that may include secrets. Values are encrypted at rest using the ASP.NET Core Data Protection API (DPAPI-backed on Windows).
- `Key` (String, PK): e.g., `OneC.RAS.Endpoint`, `OneC.Cluster.AdminPassword`, `IIS.DefaultVrdRoot`. Полный каталог — `04_INFRASTRUCTURE.md`.
- `Value` (String): Encrypted ciphertext for secret values; plaintext for non-secret values.
- `IsSecret` (Boolean): Determines whether the value is decrypted on read.
- `Description` (String, Nullable)
- `UpdatedAt` (DateTimeUtc)
- `UpdatedBy` (String): Admin ID or "System".
- Changes to settings are written to `AuditLog`. Secret values are never logged in plaintext.

## Aggregates and Business Rules
1. **License Calculation:** `Total Consumed Licenses for Tenant X` = Count of `ActiveSessionSnapshot` where `TenantId == X` AND `ConsumesLicense == true`.
2. **Kill Priority:** when `Consumed > Limit`, sessions are selected for termination ordered by `StartedAt DESC` (newest first) until `Consumed == Limit`. Locked by ADR-6 / operational constraints.
3. **Deletion Restrictions:** A `Tenant` cannot be deleted if they have active `Infobases`. An `Infobase` cannot be deleted from the system without first unpublishing it from IIS and detaching it from the 1C Cluster.
4. **Drift is observed, not auto-fixed:** the drift-detection job updates `Publication.LastDriftStatus` but never modifies IIS. Reconciliation is an explicit admin action.

## Persistence & API Contracts (binding)

These contracts are stable and must be preserved across changes.

- **Enum int-stability (frozen).** Numeric values of `AuditActionType` / `AuditReason` are part of the DB contract (`HasConversion<int>`) and are **frozen** — re-using a number for a different action would corrupt historical AuditLog rows. Reserved slots: `13` (`InfobaseReassigned`, in the 10–12 infobase group), `200` (`SessionKilled`), `201` (`LimitChanged`), `210` (`PublicationDriftDetected`), `211` (`PublicationReconciled`), `300/301` (`ClusterAdapterCircuit{Opened,Closed}` — reserved historical, never written), `400` (`SettingChanged`), `500` (`AuditLogsPurged`). On the wire enums serialise as **strings** via the globally-registered `JsonStringEnumConverter`; the int values are never exposed.
- **409 Conflict contract.** Conflict responses are `ProblemDetails` JSON with an extra machine-readable **`code`** field (`NAME_DUPLICATE`, `TENANT_HAS_INFOBASES`, `NAME_DUPLICATE_IN_TENANT`, `INFOBASE_ALREADY_ASSIGNED`, `INFOBASE_NAME_TAKEN_IN_TARGET`, `SETTING_UNKNOWN_KEY`, `SETTING_INVALID_VALUE`, `IIS_RECONCILE_FAILED`, `IIS_ACCESS_DENIED`, `SESSION_STALE`, …). The same `code` machinery also carries non-409 problems (`CLUSTER_UNAVAILABLE` is a `502`). Codes are the single source of truth in `MitLicenseCenter.Web/Endpoints/Problems.cs::ProblemCodes`; the human-readable `detail` is always Russian. A new conflict situation MUST add a new `ProblemCodes.*` constant — the frontend cannot disambiguate by `detail` string.
- **Global error envelope + uniqueness backstop.** The pipeline registers `AddProblemDetails()` and `UseExceptionHandler()` as the outermost middleware: every otherwise-unhandled exception leaves as an RFC 7807 `ProblemDetails` (never a bodiless 500), with a `traceId` extension; 5xx responses carry a neutral Russian title/detail and **never** leak the exception message or stack trace (the full exception is logged server-side, and Development still gets the developer exception page). On top of the happy-path `AnyAsync` checks, create/update/reassign `Infobase` and create/update `Tenant` wrap `SaveChanges` in a **uniqueness backstop**: a concurrent insert that races past the pre-check trips the unique index (`IX_Infobases_ClusterInfobaseId`, `IX_Infobases_TenantId_Name`, `IX_Tenants_Name`) and the resulting `DbUpdateException` is mapped to the *same* documented 409 as the pre-check (`INFOBASE_ALREADY_ASSIGNED`, `NAME_DUPLICATE_IN_TENANT` — or `INFOBASE_NAME_TAKEN_IN_TARGET` on the reassign path, same index, context-specific code — and `NAME_DUPLICATE`). The violated index is identified by its **name** (stable schema identifier present verbatim in the `SqlException`, gated by `SqlException.Number ∈ {2601, 2627}`), never by parsing localized text. Any other `DbUpdateException` propagates to the global handler as a 500.
- **Manual session kill.** `POST /api/v1/sessions/{id}/kill` (Admin) follows the idempotent kill protocol of `KillEnforcer`: `404` if the session is absent from the current snapshot; otherwise re-fetch from the cluster and verify `(ClusterInfobaseId, AppID, StartedAt)` against the live session — a mismatch (same `SessionId`, different descriptor) is `409 SESSION_STALE` (refresh and retry), no kill. The kill itself audits (`SessionKilled` / `ManualByAdmin`) and returns `204` **only** on `Killed` or `AlreadyGone`; an unreachable cluster (`rac.exe` failure, both flags `false`) returns `502 CLUSTER_UNAVAILABLE` and writes **no** audit row.
- **Foreign keys.** `Infobase → Tenant` = `Restrict` (deletion blocked by the tenant guard, `409 TENANT_HAS_INFOBASES`). `Publication → Infobase` = `Cascade`, unique on `InfobaseId` (1-to-1 required). `AuditLogs.TenantId` = `SetNull` (audit history survives tenant deletion; the description keeps the human-readable name). Infobase name is unique **per tenant** (`IX_Infobases_TenantId_Name`); `ClusterInfobaseId` is unique **globally** (`IX_Infobases_ClusterInfobaseId`) — one cluster base belongs to exactly one tenant.
- **Infobase + Publication aggregate.** An infobase is created/updated/deleted via a single `POST/PUT/DELETE /api/v1/infobases` carrying the nested publication; the publication is cascaded in the same transaction. `GET/PUT /api/v1/publications/{id}` exists as a side-API for editing publication parameters in isolation (e.g. bumping `PlatformVersion`); `POST/DELETE` on publications do not exist.
- **Infobase reassignment.** `POST /api/v1/infobases/{id}/reassign` (Admin) carries `{ targetTenantId }` and moves the base to another tenant — the publication (FK on `InfobaseId`) is untouched. Guards: `404` if the base or target tenant is missing; `409 INFOBASE_NAME_TAKEN_IN_TARGET` if the target tenant already has a base with the same `Name` (per-tenant uniqueness). Moving to the current tenant is a no-op `200`. Writes one `InfobaseReassigned` audit row with the target `tenantId`.
- **Tenant list counts.** `GET /api/v1/tenants` returns `InfobaseCount` per tenant (correlated `COUNT` over `Infobases`); single-tenant `GET/POST/PUT` responses leave it `0` (the field defaults). Drives the "Базы: N" column and the tenant drill-down lens.
- **List paging.** `GET /api/v1/tenants` and `GET /api/v1/infobases` are paged: `?page` (1-based, default 1) and `?pageSize` (default 50, capped at 200) return a `{ items, total, page, pageSize }` envelope, where `total` is the full row count for the current filter (not just the returned page). `GET /api/v1/infobases` additionally accepts `?tenantId` to scope to one client. Both are ordered by `Name` (infobases tie-break by `Id`) so paging is stable. The SPA lists (Clients, Infobases, the per-tenant infobase lens) drive **server-side** pagination off this envelope; dropdowns that need the full set request a single large page.
- **Cluster-id availability probe.** `GET /api/v1/infobases/cluster-id-availability?clusterInfobaseId={guid}[&excludeId={guid}]` (Admin) returns `{ taken, takenByTenantName? }` — whether that cluster base is already attached to a tenant, and to which (the owner's name, since `ClusterInfobaseId` is globally unique). `excludeId` omits one infobase from the check (the base being edited, so its own id is not reported as a conflict). It is a read-only UX aid for the add/edit form, letting it stop loading every infobase just to flag duplicates; the authoritative guard remains the `409 INFOBASE_ALREADY_ASSIGNED` contract on create/update plus the `IX_Infobases_ClusterInfobaseId` unique index.
- **DPAPI secret payloads.** Each `dbo.Settings` row stores either plaintext in `ValueText NVARCHAR(MAX)` (`IsSecret=false`) or DPAPI-encrypted UTF-8 bytes in `Value VARBINARY(MAX)` (`IsSecret=true`). Protector purpose-string is `mlc.settings.v1`. Audit descriptions for secret changes never contain the value (regression-tested).
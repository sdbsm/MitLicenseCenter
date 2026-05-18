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
- `TenantId` (Guid, FK) — `ON DELETE RESTRICT`. Удаление tenant'а блокируется guard'ом endpoint'а (`409 Conflict`, `code: TENANT_HAS_INFOBASES`) до того, как SQL поднимет FK violation.
- `Name` (String, ≤200): Friendly name for the UI. Unique **per tenant** — composite index `IX_Infobases_TenantId_Name`. Два разных клиента могут иметь одноимённые инфобазы; внутри клиента — нет.
- `ClusterInfobaseId` (Guid): The internal ID of this base inside the 1C Cluster.
- `DatabaseServer` (String, ≤200): SQL Server instance name.
- `DatabaseName` (String, ≤200): SQL Database name.
- `Status` (Enum `InfobaseStatus`): `Active=0`, `Maintenance=1`, `Suspended=2`. Сохраняется как `int` (`HasConversion<int>`), на wire идёт строкой через `JsonStringEnumConverter`. Int-значения заморожены.
- `CreatedAt` (DateTimeUtc).
- `UpdatedAt` (DateTimeUtc, Nullable): обновляется в PUT-handler'е.
- **Relationships:** Belongs to `Tenant`. Has One required `Publication`. Создание/удаление инфобазы оперирует aggregate'ом — публикация создаётся в том же POST и каскадно удаляется при DELETE.

## 3. Publication (IIS Configuration)
Stores the *Desired State* of the IIS publication for a specific Infobase.
- `Id` (Guid, PK)
- `InfobaseId` (Guid, FK, **unique**) — `ON DELETE CASCADE`. 1-to-1 required: каждая инфобаза имеет ровно одну публикацию, удаление инфобазы каскадом сносит публикацию в БД (IIS-unpublish — Stage 3).
- `SiteName` (String, ≤200): IIS Site (e.g., "Default Web Site").
- `VirtualPath` (String, ≤200): The URL path (e.g., "/tenant-base1"). Валидация: должен начинаться с `/`, не содержит пробелов.
- `PlatformVersion` (String, ≤50): e.g., "8.3.23.1865". Used to locate `wsisapi.dll`. Валидация: regex `^\d+\.\d+\.\d{2}\.\d{4}$`.
- `EnableOData` (Boolean): Flag to manage standard OData interface.
- `EnableHttpServices` (Boolean).
- `VrdCustomXml` (`nvarchar(max)`, Nullable): Stores any custom XML fragments for `default.vrd` to ensure idempotency and prevent overwrite of custom configurations. Пустая строка / whitespace нормализуется в `NULL` при записи.
- `CreatedAt` (DateTimeUtc).
- `UpdatedAt` (DateTimeUtc, Nullable).
- `LastDriftStatus` (Enum): `InSync`, `Drift`, `Missing`, `Error`. **Поле появится в Stage 3** вместе с drift-detection job — в Stage 2 не присутствует ни в БД, ни в API.
- `LastDriftCheckAt` (DateTimeUtc, Nullable): **Stage 3.**
- `LastDriftDetails` (String, Nullable): **Stage 3.**
- **Relationships:** Belongs to `Infobase`.

## 4. AuditLog
Immutable record of all critical system and administrator actions.
- `Id` (Guid, PK)
- `TenantId` (Guid, FK, Nullable): If the action relates to a specific tenant.
- `ActionType` (Enum): e.g., LimitChanged, PublicationUpdated, PublicationDriftDetected, PublicationReconciled, SessionKilled, InfobaseCreated, ClusterAdapterCircuitOpened, ClusterAdapterCircuitClosed, BackupCompleted, BackupVerified, AdminLoggedIn.
- `Reason` (Enum, Nullable): For `SessionKilled` only — `LimitExceeded` or `ManualByAdmin`.
- `Description` (String): Human-readable details, including snapshot context for kills.
- `Timestamp` (DateTimeUtc)
- `Initiator` (String): ID of the Admin, or "System" for background jobs.

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
- `TwoFactorEnabled` (Boolean) — TOTP, off by default in v1
- `LockoutEnd`, `AccessFailedCount` — Identity-managed brute-force protection
- **Role assignment:** `Admin` (full access, including manual session kill and publication reconcile) or `Viewer` (read-only). Implemented via the standard Identity role tables.
- The first admin account is seeded by the initial migration with a random password printed to the service log.

## 7. Setting (Encrypted Configuration)
Holds runtime configuration values that may include secrets. Values are encrypted at rest using the ASP.NET Core Data Protection API (DPAPI-backed on Windows).
- `Key` (String, PK): e.g., `OneC.Cluster.RestApiUrl`, `OneC.Cluster.AdminPassword`, `Backup.Destination.Primary`.
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
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
- `TenantId` (Guid, FK)
- `Name` (String): Friendly name for the UI.
- `ClusterInfobaseId` (Guid): The internal ID of this base inside the 1C Cluster.
- `DatabaseServer` (String): SQL Server instance name.
- `DatabaseName` (String): SQL Database name.
- `Status` (Enum): Active, Maintenance, Suspended.
- **Relationships:** Belongs to `Tenant`. Has One `Publication`.

## 3. Publication (IIS Configuration)
Stores the *Desired State* of the IIS publication for a specific Infobase.
- `Id` (Guid, PK)
- `InfobaseId` (Guid, FK)
- `SiteName` (String): IIS Site (e.g., "Default Web Site").
- `VirtualPath` (String): The URL path (e.g., "/tenant-base1").
- `PlatformVersion` (String): e.g., "8.3.23.1865". Used to locate `wsisapi.dll`.
- `EnableOData` (Boolean): Flag to manage standard OData interface.
- `EnableHttpServices` (Boolean)
- `VrdCustomXml` (String): Stores any custom XML fragments for `default.vrd` to ensure idempotency and prevent overwrite of custom configurations.
- `LastDriftStatus` (Enum): `InSync`, `Drift`, `Missing`, `Error`. Updated by the drift-detection job.
- `LastDriftCheckAt` (DateTimeUtc, Nullable): Timestamp of the most recent drift check.
- `LastDriftDetails` (String, Nullable): Free-form description of detected differences (which nodes drifted, which file is missing, etc.).
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
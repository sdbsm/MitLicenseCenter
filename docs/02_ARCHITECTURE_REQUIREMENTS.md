# Architectural Style

The system should be designed as a **Modular Monolith**. 
Given the deployment environment (Windows Server, IIS, MSSQL), a full microservices architecture introduces unnecessary operational complexity. However, strict logical boundaries between modules must be enforced to allow future extraction of background workers if horizontal scaling is required.

# Core Architectural Principles

1. **Separation of Concerns:** 
   - Web API / UI layer must NEVER interact directly with 1C or IIS.
   - All infrastructure interactions (1C Cluster REST API, RAS, IIS Administration) must be abstracted behind Infrastructure Adapters.
2. **Desired State Configuration (DSC) Mindset:**
   - The primary database stores the *Desired State* (e.g., "Client A has a limit of 10 licenses").
   - The background workers observe the *Actual State* from the infrastructure.
   - The system automatically triggers actions to reconcile the actual state with the desired state (e.g., "Kill 1 session").
3. **Idempotency:** 
   - All infrastructure operations (updating IIS, creating publications, killing sessions) must be idempotent. Repeating a command should not crash the system or duplicate resources.

# Module Boundaries

The monolith should be logically divided into the following bounded contexts:

- **Identity & Tenant Management:** Manages Clients, roles, and global license limits.
- **Infobase Management:** Maps logical bases to physical 1C cluster bases. Manages assignment to Clients.
- **Session & License Enforcer:** The core monitoring engine. Consumes snapshots, calculates consumption per Client, and issues commands to terminate exceeding sessions.
- **Infrastructure Adapters (Anti-Corruption Layer):**
  - *1C Cluster Adapter:* Communicates with 1C Platform (8.3-8.5) via REST API (primary) or RAS (fallback).
  - *IIS Adapter:* Modifies IIS Application Pools, parses and safely updates `default.vrd` (XML) without overwriting custom nodes.

# The Reconciliation Loop (Snapshot & Enforce)

To avoid overloading the 1C Cluster, the system uses a Background Worker pattern based on a Control Loop:

1. **Observe (Snapshot):** Background jobs run on a two-tier schedule (see ADR-6):
   - **Cold loop** — full snapshot of every tenant's sessions across the cluster, every **20–30 seconds**.
   - **Hot loop** — focused snapshot for tenants at or near their limit (≥ 90% consumption), every **3–5 seconds**. A tenant returns to cold tier after two consecutive cold cycles below the threshold.
2. **Analyze (Diff):** The Session Enforcer groups active sessions by Client. It counts only sessions that consume a license (ignoring specific background jobs if they don't consume licenses). It compares the count to the Client's Limit.
3. **Act (Reconcile):** If a Client exceeds the limit, the system selects sessions ordered by `StartedAt DESC` (newest first) and sends "Kill" commands to the 1C Cluster API until `Consumed == Limit`. Before each kill the adapter re-fetches the target session and verifies `(InfobaseId, AppID, StartedAt)` match the snapshot — a mismatch causes the kill to be skipped. A `404 / session not found` response is treated as a successful (idempotent) kill. Every kill writes an immutable `AuditLog` entry with reason and snapshot context.

The hot/cold split keeps the enforcement window ≤ 5 seconds for tenants who are actually trying to exceed quota, while keeping baseline cluster load low.

# Concurrency & Background Processing

- **Task Queues:** Operations that take time (e.g., parsing/updating IIS configurations across 50 bases after a platform update) must be queued and processed asynchronously.
- **Concurrency Control:** Background workers monitoring sessions must use distributed locks (or database locks) to ensure only one enforcement loop runs at a time to prevent race conditions (e.g., killing too many sessions at once).

# Data & State Management

- **Domain Database (MSSQL):** Stores Clients, Limits, Infobase mappings, Audit Logs, and Publication desired configurations.
- **Transient State (In-Memory/Cache):** The latest "Snapshot" of active sessions should be kept in memory (or a fast key-value store) for quick UI rendering, rather than persisting every single second's state to MSSQL.
- **Audit Logging:** Every automated infrastructure change (e.g., automatic session termination, IIS update) MUST be written to an immutable Audit Log table.

# Suggested Tech Stack Constraints

- **Backend:** .NET (C#) is highly recommended due to native integration with Windows Server, IIS (Microsoft.Web.Administration), and MSSQL.
- **Frontend:** Single Page Application (React/Vue/Blazor WebAssembly) interacting with the backend via REST APIs.
- **Task Runner:** Hangfire or Quartz.NET for scheduled jobs and retry mechanisms.
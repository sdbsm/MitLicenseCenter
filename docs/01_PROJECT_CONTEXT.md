# Project Overview

The project is a centralized management and monitoring platform for a multi-tenant 1C:Enterprise hosting infrastructure. 
The system is intended to manage clients, 1C infobases, IIS publications, session limits, and infrastructure monitoring in a production hosting environment.
The platform operates as a web-based control panel for administrators.
The system is NOT a desktop application.

## Target Audience & Language

The product targets a **Russian-speaking administrator audience** (1C hosting market in Russia / CIS). This constraint is binding for the user-facing surface:

- All UI strings, button labels, validation messages, toasts, error messages, and empty-state copy are in **Russian**.
- User-facing error responses from the backend (`ProblemDetails.Detail`, `Error.Message`) are in Russian.
- `AuditLog.Description` is written in Russian so administrators can search the audit history by natural-language text.
- Locale: `ru-RU`. Dates `ДД.ММ.ГГГГ`, time 24-hour `ЧЧ:ММ:СС`, numbers `1 234,56` (space thousands separator, comma decimal).
- The SPA uses an i18n library (e.g. `react-i18next`) with a single `ru.json` resource file from day one — strings are never hardcoded in JSX. This keeps the door open for future locales without rewriting components, even though only Russian ships in v1.
- Database / code identifiers / commit messages / OpenAPI schemas remain in **English** (international engineering norm). Translation lives in the UI layer only.
- String columns that store user-visible text in MSSQL use `NVARCHAR` (Unicode) to handle Cyrillic without collation surprises.

# Terminology & Glossary

- **Client (Tenant):** A customer renting 1C infrastructure who pays for a specific limit of concurrent licenses.
- **Base (Infobase):** A 1C database assigned to a specific Client.
- **Session:** An active 1C user connection.
- **License (Consumed License):** Any client session that has successfully grabbed a license on the 1C server. Not all background jobs consume licenses, so the system must track actual license consumption.
- **Limit:** The maximum allowed concurrent licenses (sessions) paid for by the Client across all their assigned bases.
- **Publication:** An IIS web publication (`default.vrd`) for a base.
- **Snapshot:** A periodic capture of the infrastructure state (active sessions, health, limits).

# Business Context

The infrastructure owner provides hosted 1C environments to multiple clients.
Each client:
- uses one or more 1C infobases;
- works through IIS-published web clients;
- pays for a strictly limited number of concurrent licenses.

The platform acts as an orchestration and monitoring layer above:
- 1C Server Cluster
- IIS (Internet Information Services)
- MSSQL Server
- Windows Server infrastructure

# Main Goals

The system must allow administrators to:
- manage clients and assign infobases to them;
- monitor active 1C sessions and actual license consumption;
- configure and rigidly enforce concurrent session limits per client;
- automatically discover infobases from the 1C cluster infrastructure;
- manage IIS publications without destroying custom configurations;
- automate infrastructure maintenance tasks;
- maintain audit logs of all administrative and automated actions.

# License & Session Enforcement (Crucial)

The system must track license consumption autonomously. 
If a Client exceeds their allowed license limit, the system must **forcefully and automatically disconnect the offending user(s)**.
- Enforcement approach: Background Reconciliation Loop.
- The system evaluates active consumed licenses against the limit.
- If `Consumed Licenses > Allowed Limit`, the system issues a "Kill Session" command via the 1C Cluster API to drop the excess sessions.

# Infrastructure Environment

Current infrastructure stack:
- **1C Platform:** Versions **8.3 - 8.5** (This allows leveraging the modern 1C Cluster REST API, falling back to RAS if necessary).
- **OS:** Windows Server.
- **DB:** MSSQL.
- **Web Server:** IIS.

# Multi-Tenant Model

The platform must support true multi-tenant operation.
Each client has isolated limits, owns one or more infobases, and shares the underlying server resources safely. 
The platform provides a centralized administrative view across all tenants.

# 1C Cluster Integration

The system must integrate with the 1C cluster administration APIs (REST API for newer platforms, or RAS/RAC).
Infobases should be discovered automatically from the cluster infrastructure. Manual registration of existing infobases should be minimized.

# IIS Publication Management

Initial publication creation may use Designer publication or `webinst`.
**Crucial requirement for updates:** Existing publications must NOT be fully recreated during routine maintenance or platform updates.
When the 1C platform version is updated:
- The system must parse the `default.vrd` XML file.
- Only necessary IIS/web.config/vrd related updates (like the path to the new `wsisapi.dll` platform version) should be applied.
- Existing custom publication configurations (OData, HTTP services, OpenID, etc.) must be strictly preserved.

# Monitoring Model & Background Processing

The platform uses a **snapshot-based monitoring architecture**.
- Avoid aggressive real-time polling (e.g., every second).
- Use background workers/services for:
  - Session and license collection (e.g., every 15-30 seconds).
  - Enforcing session limits (Kill Sessions).
  - Health checks and infrastructure scanning.

# What Should NOT Be Done (Constraints)

- **DO NOT** use a desktop UI (must be web-based).
- **DO NOT** fully republish or overwrite existing IIS publications (`default.vrd`) blindly.
- **DO NOT** rely solely on IIS to store infrastructure state.
- **DO NOT** use aggressive per-second polling that could overload the 1C cluster.
- **DO NOT** build monolithic "god" services; separate domain logic from infrastructure adapters.
- **DO NOT** tightly couple UI code with infrastructure/1C-interaction code.
- **DO NOT** act as a full billing system (only limit enforcement).

# Architecture Expectations

The platform must be designed as:
- a production-grade system;
- modular and service-oriented;
- infrastructure as code mindset (Desired State + Reconciliation approach).
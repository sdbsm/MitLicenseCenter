# MitLicense Center — Roadmap

Forward-looking status. The full system spec lives in `01`–`06`, `DECISIONS.md`, and `OPERATIONS.md`.

## Current status — v1 delivered

The application is a working control plane for single-node multi-tenant 1C hosting:

- **Tenants / Infobases / Publications** CRUD with the FK and uniqueness contracts in `03_DOMAIN_MODEL.md`.
- **Session & license enforcement** — two-tier reconciliation loop (hot 3–5s / cold 20–30s), newest-first idempotent kill, manual kill from the Sessions Monitor.
- **1C cluster adapter** — RAS via `rac.exe` only (ADR-16 / ADR-3.3), with a 30s RAS health probe surfaced on the Dashboard.
- **IIS publications** — surgical `default.vrd` XML patching + 5-minute drift detection + explicit admin reconcile (ADR-4.1).
- **Settings** — encrypted `dbo.Settings` (DPAPI) with the 14-key catalog in `04_INFRASTRUCTURE.md`.
- **Audit** — immutable log with server-side paging/filtering and a daily retention purge.
- **Frontend** — React + TS SPA on shadcn/ui, Russian-only locale, Vitest test foundation.
- **CI** — GitHub Actions (build + test + lint), no CD (manual deploy).

Operator concerns (backup, network-edge auth) are documented in `OPERATIONS.md`.

## Backlog / deferred

- **RAS Strategy B** — replace `rac.exe`-per-cycle with a long-lived TCP socket on 1545, dropping the ~26 procs/min worst case. Gated on real-world latency measurement.
- **Multi-cluster / multi-node topology** — every adapter currently assumes single-node; opening this up requires re-reviewing each adapter and the single-node operational constraint.

## Permanently out of scope (ADR-15)

Backup orchestration and in-app 2FA. Re-introducing either requires explicitly revoking ADR-15 first.

# Operations Manual

This document covers tasks that live **outside** the MitLicense Center application — concerns the operator handles via platform tooling rather than in-app features. The application surface itself is documented in `docs/05_UI_REQUIREMENTS.md` and `docs/06_UI_DESIGN.md`.

## Startup is fail-fast — the host never serves a half-initialized state

Bootstrap runs **synchronously before the host accepts traffic** ([ADR-18](DECISIONS.md#18-fail-fast-bootstrap)): EF Core migrations are applied, then roles and the first admin are seeded (`IdentitySeeder`), then the `dbo.Settings` catalog is seeded (`SettingsSeeder`). All of this happens before `app.Run()` binds the listener. If any step throws — unreachable / mis-configured database, a failed migration, an Identity error — the host logs a `Critical` line and the process **exits with a non-zero code without ever opening a port**. There is no "started but unusable" state: a running, listening process is a fully migrated and seeded one.

Operationally this means:

- **A crash on startup is the expected signal of a bad database or config**, not a defect. Read the `Critical` log line: the stack trace names the failing step (`IdentitySeeder` → migrations, `SettingsSeeder` → settings catalog).
- **The most common cause is the connection string.** `ConnectionStrings:Default` (domain + Identity + settings) and `ConnectionStrings:Hangfire` are both required and must point at a reachable SQL Server; a dead or wrong instance aborts startup. Hangfire's recurring-job registration also runs at startup and fails fast on the same dead connection.
- **Run the host under a supervisor that surfaces the exit code** (Windows Service / IIS hosting / `sc.exe` / NSSM). A fail-fast exit should be visible and alert the operator, not be silently restarted into the same broken state.
- **First-run admin password:** on the very first successful start against an empty database, `IdentitySeeder` creates the `admin` account with a random password and writes it to the service log at `Warning` level (same in Development and Production). Capture it from the log on first boot and change it at first sign-in.

## Deployment is manual — there is no deploy script

Per [ADR-14](DECISIONS.md#14-cicd--github-actions-ci-only-no-cd-in-v1), v1 has CI only: **no deployment automation and no deploy script** ships in `scripts/` (only `build.ps1` / `db-reset.ps1` / `dev.ps1` / `shadcn-add.ps1`). Releasing a new version onto the single-node host (topology in `04_INFRASTRUCTURE.md`) is a manual operator procedure:

1. **Build & verify** on a build machine — `scripts/build.ps1` (Release) restores, builds, tests and lints both backend and frontend. A red build is not deployed.
2. **Publish the backend** — `dotnet publish backend/src/MitLicenseCenter.Web/MitLicenseCenter.Web.csproj -c Release -o <publish-dir>`, a framework-dependent publish against the .NET 10 runtime installed on the host (ADR-12).
3. **Build the frontend** — `pnpm --dir frontend build` emits the static SPA in `frontend/dist/`. It is served at the panel origin (IIS static site or the host's static-file middleware) and calls the backend same-origin at `/api/v1/...`.
4. **Stop the running backend** under its supervisor (Windows Service / IIS hosting / NSSM — whatever wraps the host; see the fail-fast startup section above).
5. **Copy artefacts** — replace the backend binaries with the new publish output and the SPA with the new `dist/`. **Never overwrite** `appsettings.Production.json` or the Data Protection key ring under `%ProgramData%\MitLicenseCenter\keys\` (losing the key ring makes every secret in `dbo.Settings` unreadable — see below). The publish output now **contains a template `appsettings.Production.json`** (placeholder `Server=YOUR-SQL-HOST`, hardened `Encrypt=True` / Swagger / `AllowedHosts` defaults — see "Transport hardening" below); on a redeploy **exclude it from the copy** so the operator-edited live file survives. On the very first deploy, copy it once and fill it in.
6. **Back up first, then start.** Bootstrap is fail-fast: EF Core migrations and seeding run synchronously before the listener opens, so a schema upgrade is applied automatically on first start of the new version. **Take a database + key-ring backup before starting a version that carries migrations** — migrations are forward-only, so rollback of a schema change means restoring the pre-deploy backup. If migrations or seeding fail the process exits non-zero without serving; read the `Critical` log line, fix, and restart.
7. **Smoke-check** — sign in, confirm the Dashboard RAS health card and the Sessions Monitor populate within ~30s.

Rolling back a release = redeploy the previous published artefacts; if the new version applied a migration, also restore the pre-deploy database backup (the app ships no down-migrations). Automating this procedure into a real `scripts/Deploy-MitLicenseCenter.ps1` is a deliberate future step tracked in the backlog, not a current artefact — CD intentionally waits until the deployment story stabilizes (ADR-14).

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

Per [ADR-15](DECISIONS.md#15-backup-and-two-factor-authentication-scope-boundary) and [ADR-7](DECISIONS.md#7-admin-authentication), the application performs username + password + HttpOnly cookie session authentication and nothing more. Secondary factor protection is the responsibility of the deployment environment:

- **Network reachability:** LAN-only or VPN. The application's HTTPS endpoint must not be exposed to the public internet.
- **Perimeter:** firewall rules limiting source IPs to the operator network / VPN concentrator.
- **Identity:** AD / SSO authentication at the network edge if the environment uses it.
- **Physical:** restricted physical access to the operator workstations from which the panel is reached.

If the deployment cannot satisfy these network-level controls, the operator should re-evaluate whether the panel is being used in its intended environment (internal 1C-hosting operator station) before requesting in-app 2FA — ADR-15 makes the latter explicitly off-limits without a deliberate revocation.

## Transport hardening — HTTPS, SQL encryption, Swagger, AllowedHosts

The shipped defaults in `appsettings.json` are tuned for **local development** (plain HTTP, a local SQL instance without a TLS certificate, browsable Swagger). Production hardening lives in **`appsettings.Production.json`** — the environment-specific file ASP.NET Core loads when `ASPNETCORE_ENVIRONMENT=Production`. It ships as a **template you edit on first deploy and never overwrite on redeploy** (same rule as the deployment section above). Every knob can also be supplied via environment variables (double-underscore form, e.g. `Security__EnforceHttps=true`, `ConnectionStrings__Default=…`), which override the file. The settings are config-gated rather than always-on by design ([ADR-22](DECISIONS.md#22-transport-hardening-is-config-gated)).

### HTTPS redirect + HSTS — `Security:EnforceHttps` (default `false`)

The app forces HTTPS (redirect `http→https` + an HSTS header) **only** when `Security:EnforceHttps=true`, and **never in Development**. Which value is correct depends on **who terminates TLS** — pick by your topology:

- **Behind a terminating TLS reverse proxy** (IIS with ARR / Nginx / Caddy in front): the proxy holds the certificate, speaks HTTPS to the browser, and forwards plain HTTP to the app on `localhost`. The **proxy** does the redirect and HSTS. **Leave `EnforceHttps=false`** — if the app also redirects it double-works and can loop (the app on `localhost` only ever sees HTTP and cannot tell TLS was already terminated upstream).
- **No proxy — Kestrel terminates TLS itself** (e.g. a direct port-forward to the service): first configure an **HTTPS endpoint with a certificate on Kestrel** (`Kestrel:Endpoints` / `ASPNETCORE_URLS=https://…` + a cert), **then** set `EnforceHttps=true` so the app redirects and emits HSTS. **Do not** set it `true` without a configured HTTPS listener — the redirect target would not answer and the panel becomes unreachable.

HSTS is **sticky**: once a browser sees the header it refuses plain HTTP for the max-age window even if HTTPS later breaks. Enable it only once HTTPS is solidly in place. The auth cookie is already `Secure=Always` outside Development (ADR-7) independently of this flag.

> Reminder (ADR-15, "Authentication is delegated to the network edge" above): this panel is meant for a **LAN/VPN** perimeter and must not be exposed to the public internet. A raw port-forward to the internet is the wrong topology — prefer VPN. `EnforceHttps` hardens the transport; it does not make public exposure acceptable.

### SQL channel encryption — `Encrypt=True` in production

The base `appsettings.json` connection strings carry `Encrypt=False` so **local development against a local SQL instance with no TLS certificate keeps working**. The `appsettings.Production.json` template flips both `ConnectionStrings:Default` and `ConnectionStrings:Hangfire` to **`Encrypt=True`**, encrypting the client↔SQL channel. `TrustServerCertificate=True` is kept because on the single-node host SQL typically presents a self-signed certificate — this **encrypts the channel but skips certificate-chain validation**, which is acceptable when SQL is reachable only at `localhost`. To validate the chain too, install a trusted certificate on the SQL instance and set `TrustServerCertificate=False`. Fill the placeholder `Server=YOUR-SQL-HOST` (and switch `Trusted_Connection` to a SQL login if the service account is not used) before first Production start — a wrong/unreachable connection string aborts the fail-fast bootstrap (see the startup section).

### Swagger UI — `Security:EnableSwagger` (default `false` outside Development)

Swagger UI at `/api/docs` exposes the full API map. It is served **always in Development** (it is the browsable reference contract the hand-written TS types are synced against — [ADR-10.1](DECISIONS.md#adr-101--hand-written-typescript-api-types-no-openapi-codegen)) and **closed in Production by default**. Set `Security:EnableSwagger=true` to reopen it on the internal admin-only perimeter if you need it for debugging.

### `AllowedHosts` — narrow it in production

The base default `*` (accept any `Host` header) is fine for dev. The production template narrows it to the panel's hostname (`panel.example.local`) — set it to the real FQDN(s) the panel answers on (comma-separated for several). This is a defense-in-depth Host-header filter on top of the network perimeter.

## RAS adapter setup

The 1C cluster is administered exclusively through RAS via `rac.exe` ([ADR-16](DECISIONS.md#16-ras-as-sole-1c-cluster-adapter)). To bring the adapter online:

1. **Confirm `rac.exe` is installed.** On 1C 8.5 it lives at `C:\Program Files\1cv8\<version>\bin\rac.exe` (the legacy `1cv8\common\` path no longer ships it).
2. **Confirm `ras.exe` is running as a Windows service** (`localhost:1545` by default); install via `racsvc.exe -instsrvc` if needed.
3. **Grant the backend service account Read+Execute** on `1cv8\<version>\bin\`. `Network Service` is sufficient on stock installs; locked-down accounts may need an explicit ACL grant.
4. In the «Параметры» admin UI set `OneC.RAS.ExePath` to the version-specific `rac.exe` path (no seeded default), verify `OneC.RAS.Endpoint` (`localhost:1545`), and set `OneC.Cluster.AdminUser` / `OneC.Cluster.AdminPassword` (leave both empty for a cluster with no registered administrators — `rac.exe` runs anonymously).
5. On the Dashboard the RAS health card should flip to `OK` within 30s. If it stays `Сбой`, open "детали ошибки" for the `rac.exe` stderr — usually a wrong `OneC.RAS.ExePath`, `ras.exe` not running, or missing ACLs. The Sessions Monitor resumes from the next reconciliation cycle (≤ 30s).

> **Session time zone.** `rac.exe` reports `started-at` in the **server's local time** (no offset). The backend interprets it in the host's local time zone and converts to UTC, so session durations are correct only when the backend and the 1C/RAS cluster share one time zone — the v1 single-Windows-Server assumption. A backend host in a different zone than the cluster would skew durations by the offset.

## IIS publishing — required permissions

The panel reads IIS state through `Microsoft.Web.Administration` (`ServerManager`) for the read-only status refresh (Hangfire cron `*/5 * * * *`) and the on-demand «Проверить сейчас». It also **writes** publication files for two explicit actions: «Опубликовать» runs `webinst.exe` (per platform version), and «Сменить платформу» rewrites `web.config`.

`new ServerManager()` reads `%windir%\system32\inetsrv\config\applicationHost.config` and `redirection.config`, both ACL'd to `Administrators` / `SYSTEM`. The panel's process identity therefore **must** be privileged:

- **Development** — run the backend from an **elevated** PowerShell. `scripts/dev.ps1` now launches the backend window with a UAC prompt by default (pass `-NoElevate` to skip when you are not touching IIS).
- **Production** — the service account / IIS app-pool identity hosting the panel must be a member of local `Administrators`, **or** be granted explicit Read on `%windir%\system32\inetsrv\config\`, plus Read/Write on the publication folders (holding `default.vrd` / `web.config`, required for platform change) and **Execute** on `…\1cv8\<version>\bin\webinst.exe` for every published platform version. Set the cluster address for `webinst -connstr` via `Settings.OneC.Cluster.Server` (falls back to the `OneC.RAS.Endpoint` host).

**Symptom of insufficient permissions:** every publication shows status «Ошибка проверки» (`Error`), and `dbo.Publications.LastCheckDetails` (visible on hover over the status badge on the «Публикации» page) reads `Не удалось проверить публикацию: … redirection.config … отсутствия необходимых разрешений`.

### IIS lifecycle management — additional permissions (MLC-047)

The «Управление IIS» block (recycle/start/stop a pool, start/stop/restart a site, full `iisreset`) needs more than read access to the metabase:

- **Pool / site commands** (`ServerManager.ApplicationPool.Recycle/Start/Stop`, `Site.Start/Stop`) require the same IIS-admin rights as the metabase reads above (local `Administrators`, or the equivalent IIS configuration write rights) — the local `Administrators` membership already covers them.
- **`iisreset`** (restart / `/stop` / `/start`) runs `…\System32\iisreset.exe`, which **stops and starts the `W3SVC` and `WAS` Windows services** — this requires service-control rights on those services. Local `Administrators` has them; a narrowly-scoped service account may not, in which case grant start/stop on `W3SVC`/`WAS` explicitly.
- **Blast radius.** `iisreset` (and stopping a shared site/pool) interrupts **every** site on the server, not just 1C publications. If the panel itself is hosted on the same IIS, an `iisreset` will briefly take the panel offline too — the confirmation dialog warns about this. Operations are serialized server-side (one at a time).

### Bulk publish / change-platform (MLC-046)

The «Публикации» page lets an admin select several publications and publish or change-platform them as a batch. Operationally:

- **It is the same single operations, repeated.** A batch fires N idempotent calls to the per-publication endpoints; there is no separate bulk path. Audit, the overwrite gate, and status refresh behave exactly as for a single action.
- **Throughput.** `webinst` spawns are capped at 3 concurrent server-side (`IWebinstConcurrencyGate`), and the UI runs the batch through a matching small pool. A batch of ~100 publications therefore takes on the order of tens of minutes (≈ 60s per `webinst`, three at a time); change-platform is far faster (a `web.config` edit). The progress dialog shows live per-publication status and a final «успешно / с ошибкой / пропущено» summary.
- **The run is bound to the open browser tab** (like watching a deploy). Closing the tab or losing the network stops scheduling new items; the server keeps finishing any in-flight `webinst`. Because re-publish is idempotent and a now-`Webinst` publication no longer trips the overwrite gate, a partial run is safely finished by re-selecting the still-failed/unprocessed rows and running again — the dialog leaves successful items deselected for exactly this.
- **Unattended / scheduled mass-publish is not supported** by design (would need the deferred Hangfire-job model, ADR-4). For now, run bulk operations interactively.

## Сбор истории использования лицензий (`MLC-048`, ADR-25)

Фундамент раздела «Отчёты»: панель копит time-series потребления лицензий клиентами.

- **Каденция съёма ≈25 с.** Замер делается внутри cold-цикла `ReconciliationJob`
  (троттл `Polling.ColdIntervalSeconds`, деф. 25 с) — переиспользует уже посчитанное
  потребление цикла, **нового спавна `rac.exe` не добавляет** (бюджет ADR-3.3 не растёт).
  Это ~36 замеров на 15-минутный бакет; в БД (`dbo.LicenseUsageSnapshots`) пишется один
  агрегат на клиента за бакет (min/max/avg + лимит), не каждый замер.
- **Данные появляются только после релиза сбора.** История не реконструируется задним
  числом — график наполняется с момента, когда заработал cold-цикл с этой версией; на
  свежей БД первые строки появятся после пересечения двух 15-минутных границ.
- **Потеря текущего частичного бакета при рестарте — норма.** Накопление открытого бакета
  живёт в памяти процесса; рестарт/редеплой теряет ещё не закрытый бакет (флаша на
  graceful shutdown нет). Для телеметрии это допустимо (best-effort) — закрытые бакеты уже
  в БД, теряется максимум последние <15 мин.
- **Ретеншен** — `Settings.LicenseUsage.RetentionDays` (деф. 365, диапазон 30–3650),
  настраивается оператором на странице «Параметры». Ночная джоба `license-usage-retention`
  (03:30 UTC, фиксировано) удаляет замеры старше окна батчами; в аудит не пишет
  (housekeeping). Смещена от `audit-retention` (03:00), чтобы ночные чистки не пересекались.
- **Чтение** — собранный ряд отдаётся read-API `GET /api/v1/reports/license-usage[/{tenantId}]`
  (Viewer): сводка по всем клиентам (сумма по тенантам на бакет, осиротевшие замеры
  включены) и drill-down одного клиента. Диапазон `from`/`to` дефолтит на последние 7 дней,
  кламп ширины — 31 день. Контракт — `03_DOMAIN_MODEL.md` §«Persistence & API Contracts».

## Проверки готовности — liveness vs readiness (`MLC-040` / PERF-04)

Два анонимных эндпоинта с разной семантикой и ценой:

| Эндпоинт | Назначение | Что проверяет | Коды |
| --- | --- | --- | --- |
| `GET /api/v1/health` | **liveness** — процесс жив | ничего (дёшев, без зависимостей) | всегда `200` `{status, version, utcNow}` |
| `GET /api/v1/health/ready` | **readiness** — зависимости готовы | БД, RAS, Hangfire-сторадж | `200` (`ready`/`degraded`) · `503` (`not_ready`) |

Тело readiness санитизировано (анонимный вызов — без путей/имён серверов/текстов исключений,
[ADR-4.1](DECISIONS.md) / MLC-009; полные детали сбоя пишутся в журнал сервера):

```json
{ "status": "ready|degraded|not_ready",
  "utcNow": "…",
  "checks": { "database": "ok|down", "ras": "ok|degraded|unknown", "hangfire": "ok|down" } }
```

- **БД** (`CanConnectAsync`, под таймаутом 2с) — единственная зависимость, гейтящая
  `not_ready`/`503`. Используйте код ответа в LB/мониторинге.
- **RAS** — читается готовый снапшот `IRasHealthReader` (тот же 30с-пробер, что и карточка
  Dashboard): `ok` / `degraded` (Сбой) / `unknown` (первые 30с). Readiness **не** делает новый
  спавн `rac.exe` — счётчик `rac.exe.spawns` от health-запросов **не растёт** (растёт только от
  фонового пробера). Это проверяется в DoD через `dotnet-counters` (см. ниже).
- **Hangfire-сторадж** (`GetStatistics()`, под таймаутом 2с) — `ok` / `down`.
- RAS-`Сбой` и Hangfire-`down` дают `degraded`, но остаются на `200`: single-node не имеет
  смысла снимать из ротации из-за RAS — это уронит и сам Dashboard, где оператор видит ошибку.

## Наблюдаемость перфа — метрики горячего пути (`dotnet-counters`)

Горячий путь (спавны `rac.exe` и цикл согласования) инструментирован встроенным
`System.Diagnostics.Metrics` (MLC-037 / PERF-01). Никаких внешних телеметрических систем
([ADR-15](DECISIONS.md#15-backup-and-two-factor-authentication-scope-boundary)) — метрики
снимаются локально утилитой **`dotnet-counters`**. Без активного слушателя метрики имеют
near-zero overhead (инструменты no-op, теги не вычисляются), поэтому их можно держать в проде
всегда — а снимать только когда нужно подтвердить спавн-бюджет
([ADR-3.3](DECISIONS.md#33-rac-cli-spawn-contract)) или латентность цикла.

### Инструменты

**Meter `MitLicenseCenter.Rac`** (единственная точка спавна — `SystemProcessRacRunner`):

| Инструмент | Тип | Единица | Теги | Что меряет |
| --- | --- | --- | --- | --- |
| `rac.exe.spawns` | Counter | `{spawn}` | `command` | каждый спавн процесса `rac.exe` (полный спавн-бюджет, включая health-ping) |
| `rac.exe.invocation.duration` | Histogram | `ms` | `command`, `outcome` | длительность одного вызова `rac.exe` |

- `command` ∈ `cluster.list` / `session.list` / `session.terminate` / `infobase.summary.list` / `other`.
- `outcome` ∈ `ok` (exit 0) / `failed` (exit ≠ 0) / `timeout` (локальный 30s-дедлайн).

**Meter `MitLicenseCenter.Reconciliation`** (цикл согласования):

| Инструмент | Тип | Единица | Что меряет |
| --- | --- | --- | --- |
| `reconciliation.cold.duration` | Histogram | `ms` | длительность успешного cold-цикла (`ReconciliationJob`) |
| `reconciliation.hot.duration` | Histogram | `ms` | длительность hot-поллинга RAS-fetch (`HotTierPollingService`) |
| `reconciliation.kills` | Counter | `{session}` | число завершённых сессий за цикл enforcement (rate ≈ kills/мин) |
| `reconciliation.hot_tenants` | ObservableGauge | `{tenant}` | текущее число hot-тенантов |

### Процедура снятия baseline

1. Установить утилиту (однократно): `dotnet tool install --global dotnet-counters`.
2. Запустить backend (под нагрузкой PERF-03 либо локальным прогоном цикла) и снять метрики по
   имени Meter'ов:

   ```powershell
   dotnet-counters monitor -n MitLicenseCenter.Web --counters MitLicenseCenter.Rac,MitLicenseCenter.Reconciliation
   ```

   (или `dotnet-counters collect …` для дампа в CSV/JSON). Гистограммы рендерятся перцентилями
   (P50/P95/P99) через `MetricsEventSource` — без внешних систем. По `-n MitLicenseCenter.Web`
   утилита находит процесс по имени; при нескольких — указать `-p <PID>`.
3. **Кросс-проверка спавнов.** Каждый неуспешный вызов `rac.exe` логируется на `Warning` с именем
   команды (`rac.exe {Command} вернул exit=…`); успешные cold/hot-циклы — на `Debug`
   (`LogColdSnapshot`/`LogHotOverlay`, поднять уровень `MitLicenseCenter.Infrastructure` до `Debug`).
   Сумма `rac.exe.spawns` за интервал обязана совпасть с ручным подсчётом этих строк в логе.

### Пример baseline

Снят локально (idle-кластер: `OneC.RAS.ExePath` указывал на заглушку с ненулевым exit, чтобы
вызвать спавны без живого RAS), окно ~104 c, `--refresh-interval 1`:

```
[MitLicenseCenter.Rac]
    rac.exe.spawns ({spawn} / 1 sec)
        command=cluster.list                              (см. ниже: 6 спавнов за окно)
    rac.exe.invocation.duration (ms)
        command=cluster.list  outcome=failed  P50/P95/P99  ≈ 63–66

[MitLicenseCenter.Reconciliation]
    reconciliation.cold.duration (ms)  P50/P95/P99         ≈ 64–65
    reconciliation.hot_tenants ({tenant})                  0
```

Декомпозиция 6 спавнов за окно (счётчик rac.exe.spawns рендерится как rate/1s; сумма ненулевых
отсчётов): **4 health-ping** (`RasHealthProbingService`, каждые 30 c) + **2 cold-цикла**
(`ReconciliationJob`, ~каждые 60 c). Это совпадает с заложенной каденцией.

**Кросс-проверка со логом.** Cold-спавны (через `ResolveClusterUuidAsync`) логируются на `Warning`
(`rac.exe cluster list вернул exit=…`) и параллельно дают записи `reconciliation.cold.duration` —
оба источника подтверждают cold-компоненту. Health-ping (`PingAsync`) **намеренно не логирует**
неуспех, поэтому счётчик `rac.exe.spawns` — это **полный** спавн-бюджет (надмножество логов) и
единственный надёжный источник для проверки бюджета ADR-3.3. На idle-кластере hot/kill-инструменты
(`reconciliation.hot.duration`, `reconciliation.kills`, теги `session.list`/`session.terminate`)
ожидаемо нулевые — они проявляются под нагрузкой с over-quota сессиями (seed-харнесс PERF-03).

## Нагрузочный seed-харнесс — проверка роста (`MLC-039` / PERF-03)

**Только для dev/test.** Воспроизводимо создаёт «рост» (много клиентов/баз/аудита/сессий), чтобы
под синтетической нагрузкой снять метрики выше и ответить замером «держит ли рост». Реализован
консольным проектом `backend/tools/MitLicenseCenter.Tools.PerfHarness` — **dev-only артефакт**: Web
на него не ссылается, в `dotnet publish` Web он не попадает; прод-поведение 1:1 (прод: `OneC.RAS.ExePath`
= реальный `rac.exe`). Никакого нового кластерного адаптера (ADR-16): заглушка стоит за существующим
`SystemProcessRacRunner` как внешний субпроцесс, поэтому метрики `rac.exe.spawns` снимаются реально.

Два режима одного бинаря:

- **seed** (`PerfHarness seed …`) — засевает dev-БД через реальный `AppDbContext` (FK, уникальные
  индексы `IX Tenants.Name` / `IX_Infobases_TenantId_Name` / `IX_Infobases_ClusterInfobaseId`,
  1:1 публикация — соблюдаются самой моделью; миграции не трогаются) и пишет `scenario.json`.
- **rac-stub** (любые иные аргументы) — фейковый `rac.exe`: отвечает на `cluster list` / `session list`
  / `session terminate` / `infobase summary list` синтетикой из `scenario.json`. Заглушка **stateless**
  (те же сессии на каждый вызов) → over-limit тенанты остаются over-limit → устойчивый kill-поток.

### Ростовые точки

Панель — 5–20 пользователей. Дефолты = baseline (ожидаемый масштаб); ×10 = ростовая точка. «до→после»
= baseline-прогон → ×10-прогон.

| Параметр | Baseline | Ростовая точка (×10) | CLI-флаг |
| --- | --- | --- | --- |
| Клиенты | 20 | 200 | `--tenants` |
| Инфобазы + публикации | 50 | 500 | `--infobases` |
| Строки `AuditLogs` | 100 000 | 1 000 000 | `--audit` |
| Активные сессии | 500 | 5 000 | `--sessions` |
| Доля over-limit | ~30% | ~30% | `--over-limit-fraction` |

Доп. флаги: `--audit-days` (горизонт давности аудита, дефолт 365), `--usage-days` (бэкфилл истории
`/reports`, дефолт 0 в перф-режиме), `--realistic` (реалистичная демо-БД — см. подсекцию ниже).

### Процедура прогона

1. Пересоздать dev-БД: `scripts\db-reset.ps1` (миграции + admin/настройки). Рекомендуется отдельная
   БД (напр. `MitLicenseCenter_Perf`), чтобы не смешивать с рабочими данными:
   `scripts\db-reset.ps1 -DatabaseName MitLicenseCenter_Perf -Force`.
2. Засеять (ростовая точка): `scripts\perf-seed.ps1 -Tenants 200 -Infobases 500 -Audit 1000000 -Sessions 5000 -ConnectionString '…;Database=MitLicenseCenter_Perf'`
   → пишет `scenario.json` (по умолчанию `%LOCALAPPDATA%\MitLicenseCenter\perf\scenario.json`).
3. Указать заглушку как адаптер: в «Параметры» (или прямо в `dbo.Settings`) задать
   `OneC.RAS.ExePath` = путь к собранному `MitLicenseCenter.Tools.PerfHarness.exe`,
   `OneC.RAS.Endpoint` оставить пустым. Если `scenario.json` лежит не в дефолтном пути — выставить
   env `MLC_PERF_SCENARIO` в окружении backend (субпроцесс-заглушка наследует env).
4. Запустить backend (`scripts\dev.ps1`/`dotnet run` на той же `ConnectionStrings__Default`).
   Cold-цикл (Hangfire, троттл 25 c) и hot-поллинг (4 c при наличии hot-тенанта) запускаются сами;
   ускорить — уменьшить `Polling.Cold.IntervalSeconds` или триггернуть `cold-snapshot` из Hangfire-дэшборда.
5. Снять метрики: `scripts\perf-counters.ps1` (или напрямую `dotnet-counters` — см. раздел выше).
   Зафиксировать спавны/мин по тегам `cluster.list`/`session.list`/`session.terminate`,
   `reconciliation.cold.duration`/`hot.duration` (P50/P95/P99), `reconciliation.kills`, `hot_tenants`.
6. Повторить п.2/5 на baseline-параметрах — это и есть «до→после».

Под нагрузкой (в отличие от idle-кластера) hot/kill-инструменты становятся ненулевыми: over-limit
тенанты промоутятся в hot (gauge `hot_tenants` > 0), hot-поллинг даёт `session.list`-спавны каждые 4 c,
а `KillEnforcer` — `session.terminate`-спавны и `reconciliation.kills` (≤ 20/цикл, `MaxKillsPerCycle`).
Рост числа сессий/баз отражается в длительностях цикла и в спавн-бюджете — это и есть демонстрация,
что харнесс позволяет мерить рост.

### Реалистичная демо-БД (флаг `-Realistic`)

Тот же `seed`-режим с флагом `--realistic` (PS `scripts\perf-seed.ps1 -Realistic`) даёт **правдоподобную
демо-БД «как будто системой пользовались»** вместо синтетической перф-нагрузки. Отличия от перф-режима:

- **Лимиты клиентов** — СМБ-распределение 5..150 (≈60% мелких 5–20, 30% средних 25–60, 10% крупных
  75–150) вместо перф-крайностей 1/1 000 000; правдоподобные названия (`ООО «…»`).
- **История использования лицензий** (`dbo.LicenseUsageSnapshots`, источник графиков `/reports`)
  бэкфилится за `--usage-days` (дефолт 365) 15-минутными бакетами с суточным/недельным профилем;
  `snapshot.Limit = MaxConcurrentLicenses` клиента (coupled, как в проде `ReconciliationJob`).
  Запись — `SqlBulkCopy` (миллионы строк).
- **Доля over-limit** — `--over-limit-fraction` (дефолт в realistic **0.10**); часть таких клиентов
  превышает лимит постоянно, часть — только в пики.
- **Живые сессии** `scenario.json` = текущему потреблению каждого клиента (правый край `/reports`
  совпадает с live-снимком дашборда), а не round-robin по `--sessions`.
- **Аудит** размазан за `--audit-days` (дефолт 365).

Данные детерминированы (`--seed`, дефолт 1039) → повтор команды даёт идентичную БД. Горизонты
`--usage-days`/`--audit-days` по умолчанию совпадают с ретеншеном (`LicenseUsage.RetentionDays` /
`Audit.RetentionDays` = 365), чтобы ночные retention-джобы не подчистили засеянное.

Рецепт: `scripts\db-reset.ps1 -Force` → `scripts\perf-seed.ps1 -Realistic -Tenants 100 -Infobases 300`
→ перезапустить backend. Для живых `/sessions` и `/dashboard` (сейчас) — выставить `OneC.RAS.ExePath`
на собранный `PerfHarness.exe` (как в перф-процедуре выше); для истории `/reports` это не требуется.

**Инвариант:** без `--realistic` режим перф 1:1 (лимиты 1/1e6, сессии round-robin по `--sessions`,
история `LicenseUsageSnapshots` не бэкфилится) — замеры роста выше остаются валидны.

## Профиль EF-запросов — baseline (`MLC-038` / PERF-02)

**Только для диагностики, по умолчанию выключен.** Опт-ин профиль логирует сгенерированный EF SQL с
таймингами для четырёх ключевых эндпоинтов, чтобы оптимизации (PERF-06 индекс аудита, PERF-07 batch в
`DriftCheckJob`) опирались на план запроса, а не на догадку. Прод-поведение и прод-логи при выключенном
флаге 1:1 (`Microsoft.EntityFrameworkCore.Database.Command` остаётся `Warning`).

### Как включить

| Флаг (config-ключ / env) | Дефолт | Что делает |
| --- | --- | --- |
| `Diagnostics:EfQueryProfiling` (`Diagnostics__EfQueryProfiling`) | `false` | навешивает `DbContextOptionsBuilder.LogTo` на событие `CommandExecuted` (Information) — «Executed DbCommand (Xms) … SQL» в файл-приёмник + Console |
| `Diagnostics:EfSensitiveDataLogging` (`Diagnostics__EfSensitiveDataLogging`) | `false` | `EnableSensitiveDataLogging` — значения параметров в открытом виде. **Невозможен без включённого профиля** (gated); включается только явным opt-in, иначе секреты/значения не пишутся |
| `Diagnostics:EfQueryProfilingLogPath` | `%LOCALAPPDATA%\MitLicenseCenter\perf\ef-profile.log` | путь файла-приёмника (создаётся лениво при первой записи) |

Гейт — в `Infrastructure/DependencyInjection.cs` (лямбда `AddDbContext`); чистые предикаты —
`Infrastructure/Diagnostics/EfQueryProfiling.cs` (юнит-тест `EfQueryProfilingTests`). Свой приёмник
`LogTo` не зависит от секции `Logging` в appsettings — единственный гейт это флаг.

### Процедура снятия baseline

1. Засеять Perf-БД харнессом (см. раздел выше): baseline `--audit 100000` и ростовая точка
   `--audit 1000000` (`db-reset.ps1 -DatabaseName MitLicenseCenter_Perf -Force` перед каждым засевом).
2. Запустить backend с профилем: env `ConnectionStrings__Default=…MitLicenseCenter_Perf`,
   `Diagnostics__EfQueryProfiling=true`, `Diagnostics__EfSensitiveDataLogging=true` (dev — чтобы видеть
   параметры), `dotnet run`.
3. Залогиниться (пароль admin печатается в лог при первом старте) и дёрнуть эндпоинты; усечь
   `ef-profile.log` перед каждым вызовом, чтобы изолировать его SQL от фонового опроса.
4. План запроса аудита — напрямую по Perf-БД (независимо от кэша EF): `SET STATISTICS XML ON` (Actual
   Execution Plan: операторы, `ActualLogicalReads`, `MissingIndexes`) и/или `SET STATISTICS IO, TIME ON`.

### Сгенерированный SQL (идентичен на baseline и ×10)

- **`GET /tenants`** — `SELECT COUNT(*) FROM [Tenants]` + страница `… (SELECT COUNT(*) FROM [Infobases]
  WHERE [TenantId]=[t].[Id]) … ORDER BY [Name] OFFSET/FETCH`. Коррелированный COUNT транслируется в
  **один** SQL-стейтмент (подтверждено — НЕ N round-trips).
- **`GET /infobases`** — `SELECT COUNT(*) FROM [Infobases] [WHERE TenantId=@tid]` + страница с **двумя
  INNER JOIN** (`Tenants`, `Publications`) поверх пагинированного подзапроса, `ORDER BY [Name],[Id]`.
- **`GET /audit`** — `SELECT COUNT(*) FROM [AuditLogs] [фильтры]` + страница `… WHERE [ActionType]=@action
  AND [TenantId]=@tid AND [Timestamp] BETWEEN @from AND @to ORDER BY [Timestamp] DESC,[Id] DESC
  OFFSET/FETCH`. Каждый фильтр опционален.
- **`GET /sessions/snapshot`** — **0 EF-команд**: читает in-memory `IActiveSessionSnapshotStore.Current()`,
  БД не трогает (подтверждённый отрицательный результат — оптимизации SQL тут неприменимы; рост — это
  память снапшота, watch-item PERF-09).

### Тайминги (warm EF DbCommand, мс: COUNT / страница)

| Эндпоинт | baseline (100k аудита) | ×10 (1M аудита) |
| --- | --- | --- |
| `GET /tenants` | 0 / 0 | 0 / 0 |
| `GET /infobases` | 0 / 0 | 0 / 1 |
| `GET /infobases?tenantId=` | 0 / 0 | 0 / 0 |
| `GET /audit` (без фильтра) | **5** / 0 | **43** / 0 |
| `GET /audit?actionType=&tenantId=&from=&to=` (селективно) | 0 / 0 (cold first-touch COUNT ≈ 107) | 0 / 0 |
| `GET /audit?tenantId=` (широкий фильтр) | 0 / **10** | 0 / **8** |
| `GET /sessions/snapshot` | — (0 EF) | — (0 EF) |

На обоих объёмах данные помещаются в buffer cache (1M строк аудита ≈ десятки МБ), поэтому warm-тайминги
горячего пути ничтожны — кэшонезависимая метрика роста это **logical reads** из плана запроса (ниже).

### План запроса `AuditLogs` (Actual Execution Plan; вход в PERF-06)

Индексы на `dbo.AuditLogs`: три **одноколоночных** (`IX_AuditLogs_Timestamp`, `_ActionType`, `_TenantId`)
+ кластерный `PK_AuditLogs (Id)`. Составного индекса под «фильтр + `ORDER BY Timestamp DESC, Id DESC`» нет.

| Запрос | baseline (100k), logical reads | ×10 (1M), logical reads |
| --- | --- | --- |
| Страница **без фильтра** (`ORDER BY Timestamp DESC`) | Top + Index Scan `IX_Timestamp` + key lookup — **130** | то же — **129** (не растёт: индекс даёт порядок, `Top 50` обрывает рано) |
| Страница **`TenantId`-only** | Clustered Index Scan + **Sort** — **2241** | `IX_TenantId` Seek + **Sort** + key lookup — **7480** + **Missing Index, impact 69.3%** |
| Страница **`ActionType`+`TenantId`+range** (селективно) | Index Seek — **12** | Index Seek — **6** |
| `COUNT(*)` **без фильтра** | Index Scan — **499** | Index Scan — **4461** (растёт ~линейно) |
| `COUNT(*)` **`TenantId`-only** | — | `IX_TenantId` Seek — **22** |

Missing-Index, который SQL Server выдал сам на `TenantId`-only ×10: ключ `TenantId`, include
`Timestamp, ActionType, Initiator, Description, Reason` (impact 69.3%).

### Вывод — какие запросы дорогие и почему

- **Дорогой и растущий — фильтрованный список аудита по неселективному предикату** (`TenantId`) +
  `ORDER BY Timestamp DESC, Id DESC`: вынуждает **Sort + key lookup**, logical reads растут
  **2241 → 7480** при росте таблицы (при том же объёме на тенант), и оптимизатор сам просит индекс
  (impact 69%). **Прямой вход в PERF-06:** составной индекс `(TenantId, Timestamp DESC, Id DESC)` (и по
  аналогии `(ActionType, Timestamp DESC, Id DESC)`) убирает и Sort, и lookup. Авто-подсказка SQL держит
  `Timestamp` в include (Sort не уберёт) — осознанный дизайн ключует `Timestamp DESC, Id DESC`.
- **`COUNT(*)` без фильтра растёт линейно** (499 → 4461 reads, 5 → 43 мс) — это присуще offset-пагинации
  (нужен полный счёт); составной индекс его не уберёт. Watch-item, не PERF-06.
- **Список аудита без фильтра уже эффективен** (едет по `IX_Timestamp`, `Top` обрывает рано; ~130 reads,
  не растёт) — действий не требует.
- **Коррелированный COUNT в `/tenants`** — один SQL, sub-ms даже на ×10: подтверждено, что это **не N+1**
  (watch-item, не задача).
- **`/infobases`** (два JOIN) и **`/sessions/snapshot`** (0 EF) — на горячем пути не дорогие.
- **PERF-07** (`DriftCheckJob` N round-trips) — **закрыт (MLC-043).** Это фоновый джоб, не один из
  четырёх эндпоинтов; его проход раньше грузил публикации по схеме N+1 (1 запрос `Id` + по одному
  `SELECT` на публикацию). Теперь `RunAllAsync` грузит все публикации **одним** проекционным
  `AsNoTracking`-запросом (только поля проверки дрейфа, без тяжёлого `VrdCustomXml`), результат пишет
  targeted-`UPDATE` по `Id` — поведение проверки 1:1. Замер реальными SQL round-trip'ами на
  relational-провайдере (тот же план, что у MSSQL): на 25 публикациях **загрузочных SELECT/проход
  26 → 1** (`DriftCheckBatchQueryTests`, регресс-гард).


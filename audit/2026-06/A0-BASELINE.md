# A0 — BASELINE: сборка, тесты, инвентаризация

Этап А0 пред-релизного аудита. Дата снятия: **2026-06-12**, build-машина (Windows 11 Pro),
репозиторий `F:\dev\MitLicense Center`, ветка `main` (актуальна с `origin/main`), HEAD `e6b1317`.
Отчёт фиксирует только проверяемые факты; источник каждого факта — команда или файл. Оценочных
суждений нет (для А0 допустимые findings — только «не собирается / тесты падают / линт красный»;
таких **не обнаружено**).

---

## 1. Сборка и тесты

### 1.1 `.\scripts\build.ps1` — полный локальный CI-прогон

- **Итог: OK** («Все шаги пройдены успешно», exit code 0).
- Время: ~55 с (по таймстампам лога: старт 02:03:33, финиш 02:04:28; машина с прогретыми кэшами restore/pnpm).
- Конфигурация: Release.
- Пройденные шаги (по логу): Backend `dotnet restore` → `dotnet build` → `dotnet test` → `dotnet format`; Frontend `pnpm install` → `pnpm lint` → `pnpm type-check` → `pnpm test` → `pnpm build` (vite build: 3610 модулей, dist собран за 408 ms).
- Backend-тесты внутри прогона: **636 пройдено, 0 не пройдено, 0 пропущено** (Release).

### 1.2 `dotnet test` в `backend\` (отдельный прогон, Debug)

- **Итог: OK**, exit code 0, ~10.3 с полный прогон (сами тесты ~3 с).
- **Всего 636: пройдено 636, не пройдено 0, пропущено 0.**
- Один тестовый контейнер: `MitLicenseCenter.Tests.Unit.dll` (net10.0).
- Побочный факт из вывода сборки: помимо 4 src-проектов и тестов собирается `backend\tools\MitLicenseCenter.Tools.PerfHarness` (см. §2.1).

### 1.3 Frontend (`frontend\`)

| Команда | Итог | Числа | Время |
|---|---|---|---|
| `pnpm test` (vitest run v4.1.7) | OK, exit 0 | **Test Files 69 passed (69), Tests 355 passed (355)** | 13.9 с |
| `pnpm type-check` (`tsc -b --noEmit`) | OK, exit 0 | ошибок 0 | ~4 с |
| `pnpm lint` (`eslint .`) | OK, exit 0 | ошибок/предупреждений в выводе 0 | ~4 с |

---

## 2. Инвентаризация

### 2.1 Структура

**Backend** (источник: содержимое `backend\src`, `backend\tests`, вывод сборки `dotnet test`):

- Solution: `MitLicenseCenter.slnx` (корень репо).
- `backend\src` — 4 проекта: `MitLicenseCenter.Domain`, `MitLicenseCenter.Application`, `MitLicenseCenter.Infrastructure`, `MitLicenseCenter.Web`.
- `backend\tests` — 1 проект: `MitLicenseCenter.Tests.Unit`.
- `backend\tools` — 1 проект: `MitLicenseCenter.Tools.PerfHarness` (виден в графе сборки `dotnet test`).
- **.cs-файлов в `backend\` (без bin/obj): 317** (команда: `Get-ChildItem -Filter *.cs -Recurse` с исключением `\bin\|\obj\`). Ожидание из постановки («~305») — расхождение факт↔ожидание зафиксировано, источник истины — подсчёт.

**Frontend** (источник: `Get-ChildItem` по `frontend\src` без node_modules):

- **.ts: 113, .tsx: 158, итого 271.**
- `frontend\src\features` — **14 фич**: `audit`, `auth`, `backups`, `dashboard`, `discovery`, `infobases`, `performance`, `profile`, `publications`, `reports`, `sessions`, `settings`, `tenants`, `users`.

### 2.2 HTTP-эндпоинты `/api/v1` по коду

Источник: файлы `backend\src\MitLicenseCenter.Web\Endpoints\**\*Endpoints.cs` + `Program.cs`.
Контекст авторизации (Program.cs:99–100): политика **`Admin`** = `RequireRole("Admin")`;
политика **`Viewer`** = `RequireRole("Admin","Viewer")` (любая из двух ролей). Глобального
`FallbackPolicy` и глобального `RequireAuthorization` на корневой группе **нет** — требования
заданы на эндпоинтах либо на группе (отмечено).

**Итого: 71 эндпоинт `/api/v1`** (сверено независимым подсчётом: `git grep -E "\.Map(Get|Post|Put|Delete|Patch)\(" -- backend/src/MitLicenseCenter.Web/Endpoints` = 71, пофайловая разбивка совпала с таблицей).

Разбивка по требованию авторизации: `AllowAnonymous` — 3; `RequireAuthorization()` без политики (любой аутентифицированный) — 3; политика `Viewer` (роли Admin|Viewer) — 23; политика `Admin` — 42 (из них 9 — групповым `RequireAuthorization` на `/infobases/unassigned` и `/discovery`).

#### Auth — `AuthEndpoints.cs`
| Метод | Путь | Авторизация | Источник |
|---|---|---|---|
| POST | /api/v1/auth/login | AllowAnonymous | AuthEndpoints.cs:25 |
| POST | /api/v1/auth/logout | RequireAuthorization() — аутентифицирован, без роли | AuthEndpoints.cs:26 |
| GET | /api/v1/auth/me | RequireAuthorization() — аутентифицирован, без роли | AuthEndpoints.cs:27 |
| POST | /api/v1/auth/change-password | RequireAuthorization() — аутентифицирован, без роли | AuthEndpoints.cs:28 |

#### Health — `HealthEndpoints.cs`
| Метод | Путь | Авторизация | Источник |
|---|---|---|---|
| GET | /api/v1/health | AllowAnonymous | HealthEndpoints.cs:38 |
| GET | /api/v1/health/ready | AllowAnonymous | HealthEndpoints.cs:46–48 |

#### Users — `UsersEndpoints.cs`
| Метод | Путь | Авторизация | Источник |
|---|---|---|---|
| GET | /api/v1/users/ | Viewer | UsersEndpoints.cs:31 |
| POST | /api/v1/users/ | Admin | UsersEndpoints.cs:32 |
| POST | /api/v1/users/{id:guid}/reset-password | Admin | UsersEndpoints.cs:33 |
| POST | /api/v1/users/{id:guid}/disable | Admin | UsersEndpoints.cs:34 |
| POST | /api/v1/users/{id:guid}/enable | Admin | UsersEndpoints.cs:35 |
| POST | /api/v1/users/{id:guid}/role | Admin | UsersEndpoints.cs:36 |

#### Tenants — `TenantsEndpoints.cs`
| Метод | Путь | Авторизация | Источник |
|---|---|---|---|
| GET | /api/v1/tenants/ | Viewer | TenantsEndpoints.cs:29 |
| GET | /api/v1/tenants/{id:guid} | Viewer | TenantsEndpoints.cs:30 |
| POST | /api/v1/tenants/ | Admin | TenantsEndpoints.cs:31 |
| PUT | /api/v1/tenants/{id:guid} | Admin | TenantsEndpoints.cs:32 |
| DELETE | /api/v1/tenants/{id:guid} | Admin | TenantsEndpoints.cs:33 |

#### Infobases — `InfobasesEndpoints.cs`
| Метод | Путь | Авторизация | Источник |
|---|---|---|---|
| GET | /api/v1/infobases/ | Viewer | InfobasesEndpoints.cs:28 |
| GET | /api/v1/infobases/cluster-id-availability | Admin | InfobasesEndpoints.cs:29 |
| GET | /api/v1/infobases/{id:guid} | Viewer | InfobasesEndpoints.cs:30 |
| POST | /api/v1/infobases/ | Admin | InfobasesEndpoints.cs:31 |
| PUT | /api/v1/infobases/{id:guid} | Admin | InfobasesEndpoints.cs:32 |
| POST | /api/v1/infobases/{id:guid}/reassign | Admin | InfobasesEndpoints.cs:33 |
| DELETE | /api/v1/infobases/{id:guid} | Admin | InfobasesEndpoints.cs:34 |

#### Infobases / Unassigned — `UnassignedInfobasesEndpoints.cs` (авторизация на группе: Admin, строка 29)
| Метод | Путь | Авторизация | Источник |
|---|---|---|---|
| GET | /api/v1/infobases/unassigned/ | Admin (группа) | UnassignedInfobasesEndpoints.cs:31 |
| POST | /api/v1/infobases/unassigned/{clusterInfobaseId:guid}/hide | Admin (группа) | UnassignedInfobasesEndpoints.cs:32 |
| DELETE | /api/v1/infobases/unassigned/{clusterInfobaseId:guid}/hide | Admin (группа) | UnassignedInfobasesEndpoints.cs:33 |

#### Publications — `PublicationsEndpoints.cs`
| Метод | Путь | Авторизация | Источник |
|---|---|---|---|
| GET | /api/v1/publications/{id:guid} | Viewer | PublicationsEndpoints.cs:34 |
| PUT | /api/v1/publications/{id:guid} | Admin | PublicationsEndpoints.cs:35 |
| POST | /api/v1/publications/{id:guid}/check | Admin | PublicationsEndpoints.cs:38 |
| POST | /api/v1/publications/{id:guid}/publish | Admin | PublicationsEndpoints.cs:39 |
| POST | /api/v1/publications/{id:guid}/change-platform | Admin | PublicationsEndpoints.cs:40 |

#### IIS — `IisEndpoints.cs`
| Метод | Путь | Авторизация | Источник |
|---|---|---|---|
| GET | /api/v1/iis/server | Viewer | IisEndpoints.cs:34 |
| GET | /api/v1/iis/application-pools | Viewer | IisEndpoints.cs:35 |
| GET | /api/v1/iis/sites | Viewer | IisEndpoints.cs:36 |
| POST | /api/v1/iis/application-pools/recycle | Admin | IisEndpoints.cs:38 |
| POST | /api/v1/iis/application-pools/start | Admin | IisEndpoints.cs:39 |
| POST | /api/v1/iis/application-pools/stop | Admin | IisEndpoints.cs:40 |
| POST | /api/v1/iis/sites/start | Admin | IisEndpoints.cs:42 |
| POST | /api/v1/iis/sites/stop | Admin | IisEndpoints.cs:43 |
| POST | /api/v1/iis/sites/restart | Admin | IisEndpoints.cs:44 |
| POST | /api/v1/iis/reset | Admin | IisEndpoints.cs:46 |
| POST | /api/v1/iis/stop | Admin | IisEndpoints.cs:47 |
| POST | /api/v1/iis/start | Admin | IisEndpoints.cs:48 |

#### Audit — `AuditEndpoints.cs`
| Метод | Путь | Авторизация | Источник |
|---|---|---|---|
| GET | /api/v1/audit/ | Viewer | AuditEndpoints.cs:28 |
| GET | /api/v1/audit/retention | Viewer | AuditEndpoints.cs:29 |

#### Sessions — `SessionsEndpoints.cs`
| Метод | Путь | Авторизация | Источник |
|---|---|---|---|
| GET | /api/v1/sessions/snapshot | Viewer | SessionsEndpoints.cs:23 |
| POST | /api/v1/sessions/{id:guid}/kill | Admin | SessionsEndpoints.cs:24 |

#### Settings — `SettingsEndpoints.cs`
| Метод | Путь | Авторизация | Источник |
|---|---|---|---|
| GET | /api/v1/settings/ | Admin | SettingsEndpoints.cs:30 |
| PUT | /api/v1/settings/{key} | Admin | SettingsEndpoints.cs:31 |

#### Dashboard — `DashboardEndpoints.cs`
| Метод | Путь | Авторизация | Источник |
|---|---|---|---|
| GET | /api/v1/dashboard/summary | Viewer | DashboardEndpoints.cs:26 |

#### Reports — `ReportsEndpoints.cs`
| Метод | Путь | Авторизация | Источник |
|---|---|---|---|
| GET | /api/v1/reports/license-usage | Viewer | ReportsEndpoints.cs:28 |
| GET | /api/v1/reports/license-usage/{tenantId:guid} | Viewer | ReportsEndpoints.cs:29 |

#### Performance — `PerformanceEndpoints.cs`
| Метод | Путь | Авторизация | Источник |
|---|---|---|---|
| GET | /api/v1/performance/host | Viewer | PerformanceEndpoints.cs:30 |
| GET | /api/v1/performance/onec-sessions | Viewer | PerformanceEndpoints.cs:31 |
| GET | /api/v1/performance/sql | Viewer | PerformanceEndpoints.cs:32 |
| GET | /api/v1/performance/recordings | Viewer | PerformanceEndpoints.cs:36 |
| GET | /api/v1/performance/recordings/{id:guid} | Viewer | PerformanceEndpoints.cs:37 |
| POST | /api/v1/performance/recordings | Admin | PerformanceEndpoints.cs:38 |
| POST | /api/v1/performance/recordings/{id:guid}/stop | Admin | PerformanceEndpoints.cs:39 |
| DELETE | /api/v1/performance/recordings/{id:guid} | Admin | PerformanceEndpoints.cs:40 |

#### Backups — `BackupsEndpoints.cs`
| Метод | Путь | Авторизация | Источник |
|---|---|---|---|
| GET | /api/v1/backups/ | Viewer | BackupsEndpoints.cs:32 |
| GET | /api/v1/backups/{id:guid} | Viewer | BackupsEndpoints.cs:33 |
| POST | /api/v1/backups/ | Viewer (запуск бэкапа доступен обеим ролям; в коде — комментарий со ссылкой на ADR-27) | BackupsEndpoints.cs:34 |
| DELETE | /api/v1/backups/{id:guid} | Admin | BackupsEndpoints.cs:35 |

#### Discovery — `DiscoveryEndpoints.cs` (авторизация на группе: Admin, строка 28)
| Метод | Путь | Авторизация | Источник |
|---|---|---|---|
| GET | /api/v1/discovery/cluster-infobases | Admin (группа) | DiscoveryEndpoints.cs:30 |
| GET | /api/v1/discovery/databases | Admin (группа) | DiscoveryEndpoints.cs:31 |
| GET | /api/v1/discovery/iis-sites | Admin (группа) | DiscoveryEndpoints.cs:32 |
| GET | /api/v1/discovery/rac-paths | Admin (группа) | DiscoveryEndpoints.cs:33 |
| GET | /api/v1/discovery/platform-versions | Admin (группа) | DiscoveryEndpoints.cs:34 |
| GET | /api/v1/discovery/sql-instances | Admin (группа) | DiscoveryEndpoints.cs:35 |

#### Маршруты вне `/api/v1`
| Маршрут | Механизм | Авторизация по коду | Источник |
|---|---|---|---|
| /hangfire | `UseHangfireDashboard` | `AdminOnlyDashboardAuthorizationFilter`: `IsAuthenticated && IsInRole("Admin")` | Program.cs:255–260; Hangfire\AdminOnlyDashboardAuthorizationFilter.cs:10–13 |
| /api/docs (Swagger UI) | `UseSwaggerUI` | без авторизации middleware; включается только при `Security:EnableSwagger=true` или Development | Program.cs:246–253 |
| /api/docs/v1/swagger.json | `UseSwagger` | без авторизации middleware; тот же флаг | Program.cs:246 |
| статика (`/assets/*` и пр.) | `UseStaticFiles` | без авторизации | Program.cs:211 |
| `/*` SPA-fallback | `MapFallback` | AllowAnonymous (явно); для `/api/*` и `/hangfire/*` возвращает 404 | Program.cs:267–286 |

### 2.3 Hangfire-джобы

Источник: `backend\src\MitLicenseCenter.Web\Program.cs` (все регистрации — recurring; `BackgroundJob.Enqueue`/`Schedule`/`IBackgroundJobClient` — grep по `backend\` дал 0 совпадений).

| Id джобы | Интерфейс / метод | Cron | Человекочитаемо | Источник |
|---|---|---|---|---|
| `cold-snapshot` | `IReconciliationJob.RunColdAsync` | `* * * * *` | каждую минуту | Program.cs:288–291 |
| `publication-status-refresh` | `IPublicationStatusJob.RefreshAllAsync` | `*/5 * * * *` | каждые 5 минут | Program.cs:296–299 |
| `audit-retention` | `IAuditRetentionJob.RunAsync` | `0 3 * * *` | ежедневно 03:00 | Program.cs:305–308 |
| `license-usage-retention` | `ILicenseUsageRetentionJob.RunAsync` | `30 3 * * *` | ежедневно 03:30 | Program.cs:313–316 |
| `backup-retention` | `IBackupRetentionJob.RunAsync` | `15 3 * * *` | ежедневно 03:15 | Program.cs:321–324 |

Сопутствующие факты: `RecurringJob.RemoveIfExists("drift-check")` — удаление устаревшей джобы (Program.cs:300); на `IReconciliationJob` — `[DisableConcurrentExecution(180)]` (IReconciliationJob.cs:21); фильтр `JobRetentionStateFilter` ставит `JobExpirationTimeout = 2 дня` для Succeeded/Deleted (Web\Hangfire\JobRetentionStateFilter.cs).

### 2.4 EF-миграции

Источник: `backend\src\MitLicenseCenter.Infrastructure\Persistence\Migrations`.

- **Количество: 19** (файлы `*_Имя.cs` без `.Designer.cs` и snapshot).
- Первая: `20260518010940_InitialCreate`. **Последняя: `20260610212042_MLC092HiddenClusterInfobases`.**

### 2.5 Зависимости

**Backend — `backend\Directory.Packages.props`** (central package management, полный список):

| Пакет | Версия |
|---|---|
| Microsoft.EntityFrameworkCore (+ SqlServer, Design, Tools, InMemory, Sqlite) | 10.0.8 |
| Microsoft.AspNetCore.Identity.EntityFrameworkCore | 10.0.8 |
| Microsoft.AspNetCore.DataProtection (+ .EntityFrameworkCore) | 10.0.8 |
| Microsoft.AspNetCore.OpenApi | 10.0.8 |
| System.Security.Cryptography.Xml | 10.0.8 |
| Asp.Versioning.Http / Asp.Versioning.Mvc.ApiExplorer | 8.1.0 |
| Swashbuckle.AspNetCore | 7.2.0 |
| Hangfire.Core / Hangfire.AspNetCore / Hangfire.SqlServer | 1.8.18 |
| Newtonsoft.Json (транзитивный pin) | 13.0.3 |
| Microsoft.Web.Administration | 11.1.0 |
| System.ServiceProcess.ServiceController | 10.0.8 |
| Microsoft.Extensions.Hosting.WindowsServices | 10.0.8 |
| System.Management | 10.0.8 |
| Microsoft.NET.Test.Sdk | 17.12.0 |
| xunit | 2.9.2 |
| xunit.runner.visualstudio | 2.8.2 |
| FluentAssertions | 6.12.2 |
| NSubstitute | 5.3.0 |
| coverlet.collector | 6.0.2 |
| NetArchTest.Rules | 1.3.2 |

**Frontend — `frontend\package.json`:**

dependencies (22): `@hookform/resolvers` ^5.2.2 · `@tanstack/react-query` ^5.100.10 · `@tanstack/react-table` ^8.21.3 · `chart.js` ^4.5.1 · `class-variance-authority` ^0.7.1 · `clsx` ^2.1.1 · `date-fns` ^4.1.0 · `i18next` ^26.2.0 · `jspdf` ^4.2.1 · `lucide-react` ^1.16.0 · `next-themes` ^0.4.6 · `radix-ui` ^1.4.3 · `react` ^19.2.6 · `react-dom` ^19.2.6 · `react-hook-form` ^7.76.0 · `react-i18next` ^17.0.8 · `react-router` ^7.15.1 · `recharts` ^3.8.1 · `sonner` ^2.0.7 · `tailwind-merge` ^3.6.0 · `xlsx` ^0.18.5 · `zod` ^4.4.3

devDependencies (22): `@eslint/js` ^10.0.1 · `@tailwindcss/vite` ^4.3.0 · `@testing-library/jest-dom` ^6.9.1 · `@testing-library/react` ^16.3.2 · `@testing-library/user-event` ^14.6.1 · `@types/node` ^22.10.5 · `@types/react` ^19.2.14 · `@types/react-dom` ^19.2.3 · `@vitejs/plugin-react` ^6.0.2 · `eslint` ^10.4.0 · `eslint-plugin-react-hooks` ^7.1.1 · `eslint-plugin-react-refresh` ^0.5.2 · `globals` ^17.6.0 · `jsdom` ^27.4.0 · `lint-staged` ^17.0.5 · `prettier` ^3.8.3 · `prettier-plugin-tailwindcss` ^0.8.0 · `tailwindcss` ^4.3.0 · `typescript` ^6.0.3 · `typescript-eslint` ^8.59.3 · `vite` ^8.0.13 · `vitest` ^4.1.7

### 2.6 Тулчейн

| Что | Заявлено (файл) | Фактически на машине (команда) |
|---|---|---|
| .NET SDK | `10.0.100` (`backend\global.json`) | сборка/тесты идут на net10.0 (вывод dotnet) |
| Node | `engines.node: >=22.13` (`frontend\package.json`) | `node --version` → **v24.15.0** |
| pnpm | `packageManager: pnpm@11.0.8` (`frontend\package.json`) | `pnpm --version` → **11.0.8** |

---

## 3. Git-гигиена

- **`git status`: чисто.** `git status --porcelain` — пустой вывод; ветка `main`, up to date с `origin/main`; «nothing to commit, working tree clean» (снято до создания этого отчёта; сам отчёт `audit/` — новый нетрекаемый артефакт аудита).
- **.gitignore покрывает артефакты сборки** (проверено `git check-ignore`): `backend\**\bin` / `obj` → ignored; `frontend\node_modules` → ignored; `frontend\dist` → ignored; `artifacts\` → ignored; `.claude\plans\` → ignored; также в .gitignore: `TestResults/`, `wwwroot/` Web-проекта, `keys/` (DPAPI key ring), `.env*`, `appsettings.*.local.json`, `secrets.json`, `*.pfx`, `*.pem`. Папка `audit\` в .gitignore **не** входит (check-ignore: NOT ignored) — отчёты аудита трекаемы.
- **Секреты в трекаемых конфигах**: grep `password|connectionstring|secret` по трекаемым `*.json, *.config, *.xml, *.props, *.iss, *.ps1`:
  - Трекаемые `appsettings` (3 шт.: `appsettings.json`, `appsettings.Development.json`, `appsettings.Production.json`) — connection strings только с `Trusted_Connection=True` (Windows-аутентификация) и плейсхолдером `Server=YOUR-SQL-HOST`; **паролей/SQL-логинов в значениях нет** (файлы прочитаны полностью).
  - Остальные хиты — i18n-строки UI (`frontend\src\i18n\ru.json`), код мастера установки (`installer\MitLicenseCenter.iss`: переменные/функции для ввода пароля оператором в рантайме установки) и параметры скриптов (`scripts\db-reset.ps1`, `perf-seed.ps1`, `reset-admin.ps1` — дефолтные строки подключения с `Trusted_Connection=True`, без паролей).
  - **Закоммиченных значений секретов на верхнем уровне не обнаружено** (проверка — быстрый grep по факту наличия, не полный secret-scan истории git).

---

## Итог

**Сборка: OK** (`build.ps1`, ~55 с, exit 0) · **Тесты BE: 636/636** (0 failed, 0 skipped) · **Тесты FE: 355/355** (69 файлов; type-check OK, lint OK).

**Blocker-findings уровня А0: нет** (всё собирается, все тесты зелёные, линт чистый).

**Что снять не удалось / ограничения снятия:**
- CI на GitHub Actions не прогонялся (джобы не стартуют из-за биллинга аккаунта — известное состояние); базовая линия снята только локальными прогонами, что для текущего процесса и является штатным гейтом.
- Точное время `build.ps1` измерено по таймстампам файла лога (±1–2 с), скрипт сам суммарное время не печатает.
- Glob/grep-подсчёты (.cs/.ts/.tsx) — снимок на HEAD `e6b1317`; ожидание постановки «~305 .cs» с фактом 317 расходится — взят факт.
- Скан секретов — только трекаемые файлы на HEAD; история git и нетрекаемые локальные файлы не сканировались (вне объёма А0).
- Итог «72 эндпоинта» из первичной инвентаризации субагента не подтвердился: независимый пересчёт по `git grep` даёт **71**, пофайловая разбивка совпадает с таблицей §2.2 — в отчёте зафиксирован 71.

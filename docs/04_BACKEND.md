# MitLicense Center — Backend

Гайд по архитектуре бэкенда: структура проектов, конвенции эндпоинтов и ошибок,
валидация, SettingDefinitions, аудит-лог, EF Core и миграции, фоновые задания.

Платформа: .NET 10, ASP.NET Core minimal API, SQL Server, Windows Service.

---

## 1. Структура решения

Solution `MitLicenseCenter.slnx` в каталоге `backend/`.

### 1.1 Четыре src-проекта (`backend/src/`)

| Проект | Назначение |
|---|---|
| `MitLicenseCenter.Domain` | Доменные сущности, enum'ы, value-объекты. Нет зависимостей на другие слои. |
| `MitLicenseCenter.Application` | Интерфейсы сервисов и джобов, use-case-контракты, валидационные хелперы, SettingDefinitions. Знает только Domain. |
| `MitLicenseCenter.Infrastructure` | Реализации: EF Core (`AppDbContext`, миграции), ASP.NET Core Identity, адаптеры 1С/IIS, Settings/Audit, Hangfire-джобы. |
| `MitLicenseCenter.Web` | Minimal API: маппинг эндпоинтов, Program.cs, авторизационные политики, SPA-fallback. |

Дополнительно:
- `backend/tests/MitLicenseCenter.Tests.Unit` — 636 unit-тестов (xUnit, FluentAssertions, NSubstitute, NetArchTest).
- `backend/tools/MitLicenseCenter.Tools.PerfHarness` — утилита нагрузочного тестирования (не является production-сервисом).

### 1.2 Правило зависимостей (ADR-20)

Направление: `Web → Infrastructure → Application → Domain`.

Domain не зависит ни от чего выше. Application не знает об Infrastructure и Web.
Web обращается к инфраструктуре 1С/IIS **только** через Application-интерфейсы
(`IClusterClient`, `IIisPublishingService`, `I*Discovery`, `I*Job` и т.д.) —
напрямую использовать `RacExecutableRasClusterClient`, `Microsoft.Web.Administration`
или `System.Diagnostics.Process` из Web запрещено.

Исключения-вертикали внутри Web (легитимны по ADR-20): прямой доступ к
`Infrastructure.Persistence` (AppDbContext), `Infrastructure.Identity` (AppUser/AppRole/Roles),
`Infrastructure.Audit` (AuditLog через `db.AuditLogs`) и `Infrastructure.Settings`
(SettingsSeeder в fail-fast bootstrap).

Граница автоматически проверяется тестом `LayerBoundaryTests` (NetArchTest/Mono.Cecil —
анализ IL, а не только сигнатур).

---

## 2. Конвенция minimal API эндпоинтов

### 2.1 Организация и версионирование

Каждая предметная область оформлена как отдельный статический класс `*Endpoints.cs`
в папке `backend/src/MitLicenseCenter.Web/Endpoints/<Область>/`.
Текущие области: Auth, Health, Users, Tenants, Infobases, Publications, IIS,
Audit, Sessions, Settings, Dashboard, Reports, Performance, Backups, Discovery,
плюс Shared (общие хелперы).

Все эндпоинты регистрируются вызовом `Map*Endpoints(versionSet)` из `Program.cs`.
Версия задаётся через `Asp.Versioning.Http` (UrlSegment-стратегия); все текущие
маршруты принадлежат версии `v1` с префиксом `/api/v1/`.

### 2.2 Авторизационные политики

Политики объявлены в `Program.cs`:

```
AddPolicy("Admin",  p => p.RequireRole("Admin"))
AddPolicy("Viewer", p => p.RequireRole("Admin", "Viewer"))
```

- **Admin** — полный доступ: CRUD, разрушительные операции, параметры, пользователи.
- **Viewer** — только чтение: покрывает роли Admin и Viewer (Admin может всё, что Viewer).
- `RequireAuthorization()` без политики — любой аутентифицированный: logout, /me, change-password.
- `AllowAnonymous` — только login и оба health-check (/api/v1/health, /api/v1/health/ready).

Авторизация выставляется либо на конкретном эндпоинте (`RequireAuthorization(Roles.Admin)`),
либо на группе (Discovery и UnassignedInfobases — Admin на всю группу).

Особый случай: `POST /api/v1/backups/` (запуск бэкапа) требует `Viewer`
(доступен обеим ролям — ADR-27).

Hangfire-дашборд (`/hangfire`) защищён собственным `AdminOnlyDashboardAuthorizationFilter`:
`IsAuthenticated && IsInRole("Admin")`.

### 2.3 Типизированные результаты

Все обработчики возвращают `TypedResults.*` (например `TypedResults.Ok(...)`,
`TypedResults.ValidationProblem(...)`, `TypedResults.Conflict(...)`), что даёт
точные union-типы результата и автоматический OpenAPI-вывод.

---

## 3. Контракт ошибок

### 3.1 Формат (ProblemDetails, RFC 7807)

Все ошибки возвращаются в формате `ProblemDetails` (зарегистрирован через `AddProblemDetails`).
Непойманные исключения обрабатывает `UseExceptionHandler()` — он же логирует полное
исключение на сервере; **во внешний ответ стек-трейс и текст исключения не попадают никогда**.
Для 5xx наружу уходит нейтральный русский текст: «Произошла непредвиденная ошибка».
Все ответы дополнены полем `traceId` (из `Activity.Current?.Id`).

### 3.2 Коды HTTP

| Код | Ситуация |
|---|---|
| 400 (ValidationProblem) | Провал ручной валидации входных полей (пустое имя, неверный формат и т.д.) |
| 401 | Не аутентифицирован (login не прошёл, нет куки) |
| 403 | Не хватает роли (403 вместо 302-редиректа за счёт переопределения OnRedirectToAccessDenied) |
| 404 | Объект не найден (+ неизвестный ключ параметра `SETTING_UNKNOWN_KEY`) |
| 409 (Conflict) | Бизнес-конфликт |
| 502 | Кластер 1С недоступен при ручном kill сеанса |

### 3.3 Machine-readable коды конфликтов

Все 409-ответы формируются через `Problems.*` (`backend/src/MitLicenseCenter.Web/Endpoints/Shared/Problems.cs`).
Каждый содержит поле `extensions.code` — строковую константу из `ProblemCodes`.
Frontend опирается на эти коды для локализованных сообщений и подсветки полей формы.

Примеры кодов: `NAME_DUPLICATE`, `TENANT_HAS_INFOBASES`, `NAME_DUPLICATE_IN_TENANT`,
`INFOBASE_ALREADY_ASSIGNED`, `SETTING_UNKNOWN_KEY`, `SETTING_INVALID_VALUE`,
`PUBLISH_FAILED`, `PUBLISH_CONFIRM_REQUIRED`, `UNPUBLISH_FAILED`,
`IIS_RECONCILE_FAILED`, `IIS_ACCESS_DENIED`, `IIS_OPERATION_FAILED`, `IIS_CONFIRM_REQUIRED`,
`SESSION_STALE`, `CLUSTER_UNAVAILABLE`,
`USER_USERNAME_DUPLICATE`, `USER_NOT_FOUND`, `USER_CANNOT_DISABLE_SELF`, `USER_LAST_ACTIVE`, `USER_CANNOT_CHANGE_OWN_ROLE`,
`RECORDING_ACTIVE`, `BACKUP_ACTIVE`, `BACKUP_FOLDER_NOT_CONFIGURED`, `BACKUP_DELETE_FAILED`,
`SQL_SERVER_NOT_CONFIGURED`, `UNASSIGNED_ALREADY_ASSIGNED`, `UNASSIGNED_ALREADY_HIDDEN`,
`TENANT_CONCURRENCY_CONFLICT`.

Технические детали (пути, имена серверов, текст COM/IO-исключений) в `detail` не попадают —
только санитизированные русскоязычные сообщения. Исключение `CLUSTER_UNAVAILABLE` возвращает
502 вместо 409.

### 3.4 Оптимистическая блокировка `Tenant`

`Tenant` несёт rowversion-токен (`RowVersion byte[]?`, SQL Server `rowversion`,
`IsRowVersion()` в `AppDbContext`). `PUT /tenants/{id}` принимает опциональный `RowVersion`:
если он задан, endpoint выставляет его как ожидаемую версию
(`db.Entry(tenant).Property(t => t.RowVersion).OriginalValue = …`) перед `SaveChanges`.
SQL Server добавляет к UPDATE условие `WHERE RowVersion = @original`; при затронутых 0 строках
(строку успели изменить) EF бросает `DbUpdateConcurrencyException`, которую endpoint ловит
**отдельным** `try/catch` вокруг `SaveWithUniquenessBackstopAsync` и мапит в **409**
`TENANT_CONCURRENCY_CONFLICT`. Concurrency-исключение — подкласс `DbUpdateException`, но
uniqueness-backstop его не проглатывает: `DbUniqueViolation.Identify` вернёт `None` (нет имени
индекса) и пробросит дальше, где его перехватывает concurrency-catch. Пустой `RowVersion`
(старый клиент / без проверки версии) сохраняет прежнее поведение и оставляет существующие
тесты зелёными. `Infobase`/`Publication` пока без токена (follow-up).

### 3.5 Контракт discovery-ответов

Discovery- и IIS-листинг-эндпоинты при сбое инфраструктуры возвращают **не ошибку HTTP, а
`200 OK`** с телом, в котором флаг `available` установлен в `false`. Это намеренное отступление
от стандартного контракта ошибок (§3.1–3.4): «недоступность источника» — штатная ситуация для
форм настройки, а не сбой протокола. Фронт по `available: false` переходит в режим ручного
ввода вместо отображения ошибки.

#### Общий тип ответа

Большинство listing-эндпоинтов использует обобщённую запись (объявлена в
`DiscoveryEndpoints.cs`):

```csharp
public sealed record DiscoveryResponse<T>(IReadOnlyList<T> Items, bool Available, string? Error);
```

Поля на проводе (camelCase, политика §3.4): `items`, `available`, `error`.
При успехе: `available: true`, `error: null` (поле отсутствует в ответе —
`DefaultIgnoreCondition = WhenWritingNull`), `items` — непустой или пустой список.
При сбое инфраструктуры: `available: false`, `error: "<санитизированное сообщение>"`, `items: []`.

Эндпоинт `/iis/server` использует собственную запись (`IisContracts.cs`):

```csharp
public sealed record IisServerStatusResponse(string State, bool Available, string? Error);
```

Поля: `state`, `available`, `error`. При сбое `state` принимает значение `"Unknown"`.

#### Санитизация сообщений

Поле `error` содержит фиксированный русский текст, вшитый в обработчик. Полное исключение
(стек-трейс, имена серверов, SQL-детали, COM-сообщения) логируется на сервере через
source-gen `[LoggerMessage]` и наружу **никогда не попадает**. Примеры:
«Не удалось получить список баз данных. Проверьте доступность SQL-сервера и права доступа
или введите имя базы вручную.»,
«Не удалось получить список пулов приложений IIS. Проверьте доступность веб-сервера и права службы.».

#### OperationCanceledException

`OperationCanceledException` **не перехватывается** — он не входит в `catch`-фильтр
`when (ex is not OperationCanceledException)` и пробрасывается выше (к middleware отмены
запроса). Это касается всех async-эндпоинтов Discovery и IIS-листинга.
Исключение: `GetSqlInstances` и `GetRacPaths` выполняются синхронно (нет `await`) — фильтр
там не нужен; реестровый вызов в `GetSqlInstances` не бросает `OperationCanceledException`.

#### Эндпоинты, использующие паттерн

| Маршрут | Тип ответа |
|---|---|
| `GET /api/v1/discovery/databases` | `DiscoveryResponse<string>` |
| `GET /api/v1/discovery/iis-sites` | `DiscoveryResponse<IisSiteDto>` |
| `GET /api/v1/discovery/platform-versions` | `DiscoveryResponse<PlatformVersionDto>` |
| `GET /api/v1/discovery/sql-instances` | `DiscoveryResponse<string>` |
| `GET /api/v1/discovery/cluster-infobases` | `DiscoveryResponse<ClusterInfobaseDto>` |
| `GET /api/v1/discovery/rac-paths` | `DiscoveryResponse<string>` |
| `GET /api/v1/iis/application-pools` | `DiscoveryResponse<IisAppPoolDto>` |
| `GET /api/v1/iis/sites` | `DiscoveryResponse<IisSiteStateDto>` |
| `GET /api/v1/iis/server` | `IisServerStatusResponse` (собственная запись) |

Все девять маршрутов подтверждены непосредственно по коду `DiscoveryEndpoints.cs` и
`IisEndpoints.cs`.

### 3.4 Wire-контракт (ADR-10.1)

JSON на проводе: `PropertyNamingPolicy = CamelCase`, `DefaultIgnoreCondition = WhenWritingNull`.
Следствие для фронтенда: поля с `null`-значением не присутствуют в ответе — Zod-схемы FE
оформляют такие поля как `.nullish()` или `.optional()`.
Enum'ы сериализуются по имени (`JsonStringEnumConverter`).
Все `DateTime` выдаются с суффиксом `Z` (UTC) за счёт `UtcDateTimeConverter` в `AppDbContext`.

---

## 4. Валидация

### 4.1 Parity-конвенция BE↔FE

DataAnnotations (`[Required]`, `[Range]`, `[MaxLength]`) на DTO-классах в minimal API
**не прогоняются в рантайме** — они служат только документацией Swagger. Поэтому вся
рантайм-валидация выполняется **явным кодом в обработчиках** эндпоинтов.

Правила для полей Infobase и Publication вынесены в единый источник:

- Backend: `InfobaseValidationRules.cs`
  (`backend/src/MitLicenseCenter.Web/Endpoints/Shared/InfobaseValidationRules.cs`)
- Frontend: `validation.ts`
  (`frontend/src/features/infobases/validation.ts`)

Оба файла содержат идентичные константы максимальных длин и регулярное выражение формата
версии платформы. Соответствие закреплено parity-тестами:
`InfobasesValidationTests.cs` (BE) и `validation.test.ts` (FE).
Изменение правила требует синхронного обновления обоих файлов и тестов.

### 4.2 Максимальные длины полей

| Поле | Максимум | Источник |
|---|---|---|
| `Infobase.Name`, `Tenant.Name` | 200 символов | `InfobaseValidationRules.NameMaxLength`; `HasMaxLength(200)` в `AppDbContext` |
| `Infobase.DatabaseName` | 200 символов | `InfobaseValidationRules.DatabaseNameMaxLength`; `HasMaxLength(200)` в `AppDbContext` |
| `Publication.SiteName` | 200 символов | `InfobaseValidationRules.SiteNameMaxLength`; `HasMaxLength(200)` |
| `Publication.VirtualPath` | 200 символов | `InfobaseValidationRules.VirtualPathMaxLength`; `HasMaxLength(200)` |
| `Publication.PlatformVersion` | 50 символов | `InfobaseValidationRules.PlatformVersionMaxLength`; `HasMaxLength(50)` |
| `Publication.PhysicalPathOverride` | 260 символов | `InfobaseValidationRules.PhysicalPathMaxLength`; `HasMaxLength(260)` (MAX_PATH) |

### 4.3 Гоча: DataAnnotations и максимальные длины

`[MaxLength]` / `[StringLength]` / `[Range]` в minimal API не валидируются автоматически —
фреймворк их игнорирует при биндинге. Длины строк, которые не проверяются явно в коде
обработчика, отсекаются только ограничением `nvarchar` в SQL Server (ошибка 8152 от БД
→ `DbUpdateException` → 500 через глобальный `UseExceptionHandler`).

Конвенция проекта — **«валидация на входе»**: добавляя новое поле, явно добавлять
проверку длины в обработчик (как в `InfobaseValidationRules.AppendPublicationFieldErrors`),
а не полагаться на DB-уровень. Для числовых полей аналогичная ситуация: `[Range]` не
отрабатывает — проверка пишется вручную (пример: `ValidateTenant` в `TenantsEndpoints.cs`
проверяет диапазон `MaxConcurrentLicenses` 0–100 000 явно, потому что без неё лимит `≤ 0`
молча отключает контроль квот в `ReconciliationJob`).

---

## 5. SettingDefinitions и хранилище параметров

### 5.1 Whitelist ключей

`SettingKey` (`backend/src/MitLicenseCenter.Domain/Settings/SettingKey.cs`) — единый реестр
строковых ключей-констант. Имена ключей — часть wire-контракта API и аудита; после релиза
переименование требует миграции существующих строк БД.

`SettingDefinitions.All` (`backend/src/MitLicenseCenter.Application/Settings/SettingDefinitions.cs`) —
словарь всех параметров (ключ → `SettingDefinition`). `SettingDefinition` содержит:
`Key`, `IsSecret`, `Description`, `Kind` (`Text/Number/Url/HostPort/Path`), `DefaultValue`, `Min`, `Max`.

Ключ `PUT /api/v1/settings/{key}` проверяется против `SettingDefinitions.All`: если ключ
не входит в словарь — возвращается 404 с кодом `SETTING_UNKNOWN_KEY`. Тем же словарём
`SettingsSeeder` сидирует дефолтные значения при первом запуске.

### 5.2 IsSecret и шифрование

Параметры с `IsSecret = true` (например `OneC.Cluster.AdminPassword`) хранятся
в колонке `Value (varbinary(max))` таблицы `dbo.Settings`. Значение шифруется через
ASP.NET Data Protection перед записью и расшифровывается при чтении.

Protector создаётся с purpose-строкой `mlc.settings.v1` (константа в `SettingsStore`).
Key ring хранится в файловой системе:
- Development: `%LOCALAPPDATA%\MitLicenseCenter\keys`
- Production: `%PROGRAMDATA%\MitLicenseCenter\keys`

Key ring и база данных — единый бэкап-юнит: восстановление БД без парного key ring
делает секретные настройки нечитаемыми. В этом случае нужно восстановить key ring из
резервной копии либо заново задать секреты через «Параметры».

Plain (не-секретные) значения хранятся в колонке `ValueText (nvarchar(max))`.
Оба столбца никогда не заполнены одновременно; на read-side `ListAsync` маскирует секреты
(возвращает `ValueText = null` для `IsSecret = true`).

### 5.3 Runtime-перечитывание

`SettingsSnapshot` (`ISettingsSnapshot`) — singleton in-memory кэш с TTL 30 секунд.
Горячие пути (адаптеры, Hangfire-джобы) читают конфигурацию через `GetString`/`GetInt`
без обращения к БД.

После записи нового значения `SettingsStore.SetAsync` вызывает `_snapshot.Invalidate()`,
сбрасывая кэш. Следующий читатель перезагружает весь словарь одним SQL-запросом
(single-flight — только один загрузчик, остальные ждут тот же `Task`). Изменение
параметра вступает в силу в течение 30 секунд без перезапуска приложения.

---

## 6. Аудит-лог

### 6.1 Запись

Интерфейс: `IAuditLogger` (`backend/src/MitLicenseCenter.Application/Auditing/IAuditLogger.cs`).
Реализация: `AuditLogger` (`Infrastructure/Audit/AuditLogger.cs`).

Сигнатура:
```csharp
Task LogAsync(
    AuditActionType action,
    string initiator,
    string description,
    Guid? tenantId = null,
    AuditReason? reason = null,
    CancellationToken ct = default)
```

Каждый вызов записывает одну строку в `dbo.AuditLogs` через `AppDbContext`.
`Timestamp` заполняется `TimeProvider.GetUtcNow()` (UTC).
Запись аудита происходит непосредственно в обработчиках эндпоинтов через хелпер
`httpContext.AuditAsync(...)` (`EndpointHelpers.cs`).

### 6.2 Что фиксируется

Все значимые операции системы: создание/изменение/удаление клиентов, инфобаз,
публикаций; управление пулами и сайтами IIS; успешный вход и выход;
**неудачные попытки входа** (включая блокировку учётки) — действие `LoginFailed = 108`,
initiator — введённое имя, пароль в описание не попадает никогда;
ручной и автоматический kill сеансов; изменение лимита лицензий;
смена роли, сброс пароля, отключение/включение учётки;
изменение параметров; on-demand бэкапы (запрос/успех/ошибка/удаление/ночная чистка);
ночные джобы удаления (аудит пишется только при фактическом удалении).

### 6.3 Структура записи

```
AuditLog { Id, Timestamp, ActionType, Reason?, Initiator, Description, TenantId? }
```

- `ActionType` — `AuditActionType` (enum, хранится как `int` в БД — frozen-int rule).
- `Reason` — `AuditReason?` (`LimitExceeded = 1`, `ManualByAdmin = 2`).
- `TenantId` — nullable; для server-scope операций (пользователи, IIS, параметры) не пишется.
  При удалении клиента ссылка обнуляется (`SetNull`), но запись остаётся — история неизменяема.

### 6.4 Неизменяемость

Нет эндпоинта удаления отдельных записей. Очистка только по retention-окну (`AuditRetentionDays`),
ночным джобом `audit-retention` (03:00 UTC). Сам факт очистки пишется в аудит (действие
`AuditLogsPurged = 500`).

Список типов действий (`AuditActionType`) с frozen-int значениями — в `docs/03_DOMAIN_MODEL.md`.

### 6.5 Frozen-int enum'ы (HasConversion&lt;int&gt;)

Следующие enum'ы хранятся в БД через `HasConversion<int>` — int-значения заморожены: не
переназначать, не переиспользовать, новые члены добавлять только в конец с явным числом.
На проводе все они идут строкой (`JsonStringEnumConverter`).

| Enum | Файл | Члены (int) |
|---|---|---|
| `AuditActionType` | `Domain/Audit/AuditActionType.cs` | полный список — `docs/03_DOMAIN_MODEL.md` |
| `AuditReason` | `Domain/Audit/AuditReason.cs` | `LimitExceeded = 1`, `ManualByAdmin = 2` |
| `BackupStatus` | `Application/Backups/BackupModels.cs` | `Queued = 0`, `Running = 1`, `Succeeded = 2`, `Failed = 3` |
| `BackupFailureReason` | `Application/Backups/BackupModels.cs` | `None = 0` … `TimedOut = 6` |
| `PerfRecordingStatus` | `Application/Performance/PerfRecordingModels.cs` | `Active = 0`, `Stopped = 1`, `Interrupted = 2` |
| `PerfRecordingStopReason` | `Application/Performance/PerfRecordingModels.cs` | `Manual = 0`, `TimeLimit = 1`, `SampleLimit = 2` |

Freeze-тесты: `AuditLogEnumMappingTests` (BE-14), `BackupModelsTests` (MLC-076),
`PerfRecordingEnumFreezeTests` (MLC-135/BE-13).

---

## 7. EF Core и миграции

### 7.1 DbContext

`AppDbContext` (`Infrastructure/Persistence/AppDbContext.cs`) наследует
`IdentityDbContext<AppUser, AppRole, Guid>`. DbSet'ы:
`Tenants`, `Infobases`, `Publications`, `AuditLogs`, `Settings`,
`LicenseUsageSnapshots`, `PerfRecordings`, `PerfRecordingSamples`,
`DatabaseBackups`, `HiddenClusterInfobases` — плюс Identity-таблицы.

Все Identity-таблицы переведены в схему `auth`; доменные — в схему `dbo`.
История `__EFMigrationsHistory` ведётся в схеме `dbo` (`MigrationsHistoryTable("__EFMigrationsHistory", "dbo")`).

EF настроен с `EnableRetryOnFailure(maxRetryCount: 3)`.

Соглашение `UtcDateTimeConverter` автоматически помечает все материализованные `DateTime`
как `DateTimeKind.Utc` — JSON-сериализатор добавляет суффикс `Z`, браузер парсит как UTC.

### 7.2 Добавление миграции

```powershell
dotnet ef migrations add <Name> `
  --project backend\src\MitLicenseCenter.Infrastructure `
  --startup-project backend\src\MitLicenseCenter.Web
```

### 7.3 Гоча нормализации миграций (UTF-8 без BOM + LF)

Scaffolded-файлы миграций EF Core на Windows по умолчанию создаются с BOM и CRLF.
В репозитории принята кодировка UTF-8 без BOM и окончания строк LF.
Нормализация обеспечивается двумя механизмами:

1. `.gitattributes` в корне: `*.cs text eol=lf` — git при checkout/commit конвертирует
   переводы строк.
2. `.editorconfig` в корне репозитория: секция `[*]` задаёт `charset = utf-8`,
   `end_of_line = lf` — IDE и форматтер нормализуют файл при сохранении (правила
   каскадируются и на папку `Migrations/`; локальный `Migrations/.editorconfig`
   переопределяет только стиль namespace и набор диагностик для scaffold-файлов).

Если миграция создана без применения этих конвенций — перед коммитом её нужно сохранить
в правильной кодировке вручную или через `dotnet format`.

### 7.4 Количество миграций

На момент написания: **19 миграций** (от `20260518010940_InitialCreate` до
`20260610212042_MLC092HiddenClusterInfobases`).

### 7.5 Запуск миграций на старте

При запуске приложение выполняет **fail-fast bootstrap** в `Program.cs`:

1. `DatabaseBootstrapper.EnsureDatabaseCreatedAsync` — создаёт базу данных, если она
   не существует (один `CREATE DATABASE` к `master`; существующую базу не трогает).
2. `IdentitySeeder.EnsureSeededAsync` — применяет EF-миграции (`MigrateAsync`) и сидирует
   первого администратора и роли.
3. `SettingsSeeder.EnsureSeededAsync` — сидирует дефолтные значения параметров из
   `SettingDefinitions.All`.

Любая ошибка на этих шагах — `LogCritical` и бросок исключения из `Main`; приложение
**не начинает принимать запросы** в частично инициализированном состоянии.

---

## 8. Фоновые задания

### 8.1 Hangfire recurring-джобы

Все периодические джобы зарегистрированы как Hangfire recurring jobs в `Program.cs`.
Хранилище — схема `hangfire` в той же SQL Server БД (или отдельная строка `ConnectionStrings:Hangfire`).
`JobRetentionStateFilter` ограничивает хранение истории выполнения джоб двумя днями.

| Id | Интерфейс / метод | Cron | Расписание |
|---|---|---|---|
| `cold-snapshot` | `IReconciliationJob.RunColdAsync` | `* * * * *` | каждую минуту |
| `publication-status-refresh` | `IPublicationStatusJob.RefreshAllAsync` | `*/5 * * * *` | каждые 5 минут |
| `audit-retention` | `IAuditRetentionJob.RunAsync` | `0 3 * * *` | ежедневно 03:00 UTC |
| `backup-retention` | `IBackupRetentionJob.RunAsync` | `15 3 * * *` | ежедневно 03:15 UTC |
| `license-usage-retention` | `ILicenseUsageRetentionJob.RunAsync` | `30 3 * * *` | ежедневно 03:30 UTC |

На джобе `cold-snapshot` установлен атрибут `[DisableConcurrentExecution(180)]` — предотвращает
параллельный запуск если предыдущий тик не завершился.

`cold-snapshot` содержит внутренний throttle (`ColdThrottleState`): Hangfire тикает каждую
минуту, но реальный опрос сеансов происходит с частотой `Polling.ColdIntervalSeconds` из
настроек (по умолчанию 25 с).

### 8.2 BackgroundService горячего цикла

Следующие `IHostedService` работают вне Hangfire и зарегистрированы в `DependencyInjection.cs`:

| Сервис | Назначение |
|---|---|
| `HotTierPollingService` | Sub-minute опрос сеансов 1С для клиентов «у лимита» (hot-tier, 3–5 с). Hangfire CRON не подходит (минимум 1 мин). |
| `RasHealthProbingService` | Независимый 30-секундный ping-loop: публикует `IRasHealthReader`-снимок для дашборда. |
| `PerfRecordingSamplingService` | Тикает по таймеру при активной записи быстродействия, вызывает `PerfRecordingService.SampleOnceAsync`. |
| `BackupPumpService` | Оркестратор очереди on-demand бэкапов; тикает по wake-сигналу или таймауту. |

Подробности поведения горячего цикла и enforcement — в `docs/02_ARCHITECTURE.md`.

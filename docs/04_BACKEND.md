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
`TENANT_CONCURRENCY_CONFLICT`, `INFOBASE_CONCURRENCY_CONFLICT`, `PUBLICATION_CONCURRENCY_CONFLICT`.

Технические детали (пути, имена серверов, текст COM/IO-исключений) в `detail` не попадают —
только санитизированные русскоязычные сообщения. Исключение `CLUSTER_UNAVAILABLE` возвращает
502 вместо 409.

### 3.4 Оптимистическая блокировка `Tenant`, `Infobase`, `Publication`

`Tenant`, `Infobase` и `Publication` несут rowversion-токен (`RowVersion byte[]?`, SQL Server
`rowversion`, `IsRowVersion()` в `AppDbContext`). Update-эндпоинт принимает опциональный
`RowVersion`: если он задан, endpoint выставляет его как ожидаемую версию
(`db.Entry(entity).Property(x => x.RowVersion).OriginalValue = …`) перед `SaveChanges`.
SQL Server добавляет к UPDATE условие `WHERE RowVersion = @original`; при затронутых 0 строках
(строку успели изменить) EF бросает `DbUpdateConcurrencyException`, которую endpoint ловит
`try/catch` вокруг `SaveChanges` и мапит в **409**. Пустой `RowVersion` (старый клиент / без
проверки версии) сохраняет прежнее поведение.

Коды конфликта по сущности:

- `PUT /tenants/{id}` → `TENANT_CONCURRENCY_CONFLICT`. Catch ставится **отдельным** `try/catch`
  вокруг `SaveWithUniquenessBackstopAsync`. Concurrency-исключение — подкласс `DbUpdateException`,
  но uniqueness-backstop его не проглатывает: `DbUniqueViolation.Identify` вернёт `None` (нет
  имени индекса) и пробросит дальше, где его перехватывает concurrency-catch.
- `PUT /infobases/{id}` → `INFOBASE_CONCURRENCY_CONFLICT`. Это aggregate-апдейт: один запрос
  правит и инфобазу (корень), и её публикацию. Проверяются **оба** токена — `RowVersion`
  инфобазы и вложенный `Publication.RowVersion`; устаревание любого даёт один и тот же 409.
  Catch — тоже отдельным `try/catch` вокруг uniqueness-backstop (как у `Tenant`).
- `PUT /publications/{id}` → `PUBLICATION_CONCURRENCY_CONFLICT`. Самостоятельный путь правки
  публикации (есть помимо aggregate-апдейта инфобазы), поэтому у публикации собственный токен,
  а не только транзитивная защита через инфобазу. Здесь catch — вокруг обычного `SaveChanges`
  (uniqueness-backstop в этом эндпоинте не применяется).

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

#### Серверный фильтр «не найдена в кластере» (MLC-150)

`GET /api/v1/infobases` принимает `notInCluster=true` рядом с `tenantId`/`publishStatus`:
оставляет записи панели, чьего `ClusterInfobaseId` нет в снапшоте кластера 1С (обратный
дрейф — «мёртвые души»). Снапшот берётся через **тот же** TTL-кэш (`UnassignedInfobasesCache`,
`GetClusterSnapshotAsync`), что и `GET /infobases/unassigned` — без второго спавна rac.exe
(спавн-бюджет ADR-3.3). Фильтр применяется до пагинации — `Total` честный.
Развилка недоступности RAS: `InfobaseListResponse.ClusterAvailable` заполняется **только**
при `notInCluster=true` (в остальных случаях `null` → опускается на проводе). При недоступном
RAS (`Available=false`) эндпоинт **не** фильтрует по неполному снапшоту и возвращает пустой
набор с `ClusterAvailable=false` — фронт показывает честное «не удалось проверить кластер»,
а не вводящий в заблуждение «0 найдено» (нельзя отличить «нет пропавших» от «не знаем»; та же
семантика, что у `Available` на `/infobases/unassigned`, MLC-095).

#### Агрегат сигналов «Требует внимания» (MLC-186a)

`GET /api/v1/dashboard/alerts` (Viewer) — серверный агрегат сигналов для виджета «Требует
внимания» на «Обзоре»: всё одним запросом, не N вызовами с фронта. Отдельный эндпоинт (не
расширение `/dashboard/summary`): источники тяжелее/медленнее (снапшот RAS, server-side чтение
свободного места SQL-диска), каданс реже лёгкого 5-секундного summary; контракт summary не
трогается. RAS-здоровье и факт лицензий уже в `/summary` — здесь не дублируются.

`DashboardAlertsResponse`:
- `quotaWarning` / `quotaDanger` — число активных тенантов с положительным лимитом в зонах
  75 ≤ pct < 90 и pct ≥ 90 (бакеты непересекающиеся; пороги зеркалят `frontend/src/lib/quota.ts`,
  держатся в синхроне константами `DashboardEndpoints`). Потребление — факт rac (`Consuming`, ADR-48).
- `clusterDrift` (`DashboardClusterDriftAlert?`) — дрейф панель↔кластер: `unassignedBases`
  (в кластере, не в панели, не скрытые) + `basesNotInCluster` (в панели, нет в кластере). Считается
  через **тот же** TTL-кэш снапшота (`UnassignedInfobasesCache`, общий хелпер `CountDriftAsync`),
  что `/infobases/unassigned` — без второго спавна rac. **Admin-only** (discovery — Admin-only):
  для не-Admin `clusterDrift=null` и кластер **не опрашивается** (Viewer не триггерит rac).
  `available=false` ⇒ RAS недоступен, счётчики `null` (не «ложный ноль», как MLC-095).
- `backupDisk` (`DashboardBackupDiskAlert`) — мало места на диске бэкапов: `low = free <`
  склампленный `Backup.DiskSafetyMarginMb`. Свободное место читает новый метод порта
  `ISqlBackupService.GetBackupDiskFreeBytesAsync` (server-side `xp_fixeddrives`, без оценки размера
  базы; «never throws» → `null`=«не знаем»). Папка/сервер не заданы ⇒ `configured=false` (без SQL).

Ответ кэшируется per-role (`dashboard:alerts:admin|viewer`, TTL 30с). Аудита нет (enum заморожен).

### 3.5 Управление службой RAS (ADR-47, MLC-159)

Группа `/api/v1/ras-service` (Admin-only) управляет локальной службой Windows, под
которой работает `ras.exe`. Доступ — через порт `IRasServiceManager` (`ScRasServiceManager`),
без прямого доступа к реестру/`sc.exe`/`Process` в Web (граница ADR-20). Обнаружение службы
идёт через **реестр** (`HKLM\SYSTEM\CurrentControlSet\Services`, `IServiceRegistryReader`)
плюс состояние через `ServiceController` (`IServiceStateReader`); `sc.exe` используется
только для **действий** (`create`/`config`/`start`/`stop`). Хост фиксирован `localhost`
(single-host ADR-28).

| Маршрут | Роль | Тело ответа | Аудит |
|---|---|---|---|
| `GET /api/v1/ras-service/status` | Admin | `RasServiceStatusResponse` | — |
| `POST /api/v1/ras-service/register` | Admin | `RasServiceOperationResponse` | `RasServiceRegistered` (600) |
| `POST /api/v1/ras-service/update` | Admin | `RasServiceOperationResponse` | `RasServiceUpdated` (601) |
| `POST /api/v1/ras-service/start` | Admin | `RasServiceOperationResponse` | `RasServiceStarted` (602) |

`status` диагностирует одно из **4 состояний** (`state`: `Ok` / `NotRegistered` / `Outdated`
/ `Stopped`) и отдаёт `commandPreview` — точную команду `sc` рекомендованного действия
(прозрачность ADR-47). Поле `targetReady=false` + `issue` означает, что окружение не готово
(нет `ras.exe` выбранной платформы / не задана `OneC.DefaultPlatformVersion`) — действие
выполнить нельзя. Обнаружение службы — по `ImagePath` (из реестра), содержащему `ras.exe`
(имя службы у операторов не стандартизировано); состояние — через `ServiceController`. Порт
берётся из `OneC.RAS.Endpoint`, платформа — из `OneC.DefaultPlatformVersion`, цель — локальный
агент `localhost:1540`.

Действия идемпотентны: `register` создаёт службу (`sc create` → `start`), `update`
перенастраивает существующую под платформу/порт (`stop` → `sc config` → `start`), `start`
запускает остановленную. Сбой (неготовое окружение или ненулевой код `sc.exe`) →
`409` с кодом `RAS_SERVICE_OPERATION_FAILED` и санитизированным русским текстом (секреты не
кладутся — служба слушает loopback, `obj=`/`password=` не задаются). Удаление/останов
службы — вне объёма. Вывод `sc.exe` декодируется по OEM-кодовой странице (как `rac.exe`).

---

### 3.6 Проверка обновлений (ADR-50, MLC-176)

Группа `/api/v1/updates` сверяет версию панели с последним релизом на GitHub и сигналит о новой
версии. Это **первый исходящий HTTP** в проекте: типизированный `HttpClient` к публичному GitHub
Releases API за портом `IGitHubReleaseClient` (граница ADR-20). Текущая версия берётся из
informational-версии сборки (как в `/health`), репозиторий — из настройки `Updates.Repository`.

| Маршрут | Роль | Тело ответа | Аудит |
|---|---|---|---|
| `GET /api/v1/updates/status` | Viewer | `UpdateStatusResponse` | — |
| `POST /api/v1/updates/check-now` | Admin | `UpdateStatusResponse` | — |

Аудит **не пишется** (проверка read-only, enum `AuditActionType` заморожен). Контракт
`UpdateStatusResponse(CurrentVersion, LatestVersion?, UpdateAvailable, ReleaseUrl?, DownloadUrl?,
CheckAvailable, CheckedAtUtc)`: `CurrentVersion` всегда заполнен; при `CheckAvailable=false`
(рубильник `Updates.Enabled=0`, пустой репозиторий или GitHub-сбой) — `LatestVersion`/`ReleaseUrl`/
`DownloadUrl=null` и `UpdateAvailable=false`. `DownloadUrl` = `browser_download_url` первого ассета
релиза с именем на `.exe`.

**Кеш-стратегия.** Ленивый `IMemoryCache.GetOrCreateAsync("updates:status", …)` (паттерн
`DashboardEndpoints`), **без фонового hosted-service**. TTL = `Updates.CheckIntervalHours` при успешной
проверке; при `CheckAvailable=false` — короткий TTL 5 мин (временный сбой не «залипает» на часы).
`check-now` делает `cache.Remove` и пересчитывает. Сравнение тегов — чистый semver-компаратор
`AppVersion`/`UpdateComparison` (Domain/Updates): major.minor.patch, при равной тройке release >
prerelease; непарсимая сторона → «обновление недоступно». Установщик запускается **вручную** админом
под UAC — бэкенд только отдаёт URL (ADR-50).

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

`Enforcement.TerminateMessage` — свободный текст (`Kind = Text`, не секрет, с сидируемым
RU-дефолтом), который `KillEnforcer` передаёт в `rac session terminate` флагом
`--error-message` при завершении сеанса по лимиту лицензий: тонкий клиент 1С показывает его
пользователю модальным окном (причина + контакты провайдера). Пустая настройка → флаг не
передаётся, сеанс гасится молча (текущее поведение). Текст добавляется только на
enforcement-пути (`KillEnforcer` → `IClusterClient.KillSessionAsync(..., errorMessage)` →
`RacExecutableRasClusterClient`); ручное завершение сеанса оператором (`SessionsEndpoints`)
вызывает `KillSessionAsync` без текста. Аргумент уходит через `ProcessStartInfo.ArgumentList`
(wide-args .NET с авто-экранированием) — отдельная CP866/OEM-перекодировка не нужна (OEM
касается только чтения stdout/stderr `rac.exe`). Поддерживается платформой 1С 8.x.

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
смена роли, сброс пароля, отключение/включение и удаление учётки (`UserDeleted = 109`);
изменение параметров; on-demand бэкапы (запрос/успех/ошибка/удаление/ночная чистка);
управление службой RAS (регистрация/перенастройка/запуск — **600-серия**:
`RasServiceRegistered = 600`, `RasServiceUpdated = 601`, `RasServiceStarted = 602`,
server-scope, секреты в описание не пишутся);
диагностические записи «Быстродействия» (старт/стоп/удаление — **700-серия**:
`PerfRecordingStarted = 700`, `PerfRecordingStopped = 701`, `PerfRecordingDeleted = 702`,
host-scope `TenantId=null`, только при фактическом успехе — MLC-179);
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
| `publication-status-refresh` | `IPublicationStatusJob.RefreshAllAsync` | `*/5 * * * *` | каждые 5 минут |
| `audit-retention` | `IAuditRetentionJob.RunAsync` | `0 3 * * *` | ежедневно 03:00 UTC |
| `backup-retention` | `IBackupRetentionJob.RunAsync` | `15 3 * * *` | ежедневно 03:15 UTC |
| `license-usage-retention` | `ILicenseUsageRetentionJob.RunAsync` | `30 3 * * *` | ежедневно 03:30 UTC |
| `perf-recording-retention` | `IPerfRecordingRetentionJob.RunAsync` | `45 3 * * *` | ежедневно 03:45 UTC |
| `database-size-collection` | `IDatabaseSizeCollectionJob.RunAsync` | `0 2 * * *` | ежедневно 02:00 UTC |
| `database-size-retention` | `IDatabaseSizeRetentionJob.RunAsync` | `0 4 * * *` | ежедневно 04:00 UTC |

Cold-цикл сеансов с MLC-154 — не Hangfire-джоб, а `ColdTierPollingService` (см. §8.2); при
апгрейде `Program.cs` снимает осиротевшую запись `RecurringJob.RemoveIfExists("cold-snapshot")`.

### 8.2 BackgroundService-циклы сеансов и прочие

Следующие `IHostedService` работают вне Hangfire и зарегистрированы в `DependencyInjection.cs`:

| Сервис | Назначение |
|---|---|
| `ColdTierPollingService` | «Холодный» полный опрос сеансов 1С: общий снимок для дашборда и `/sessions`, расчёт потребления, promote/demote hot-tier, семпл телеметрии лицензий, enforcement-kill. Таймер читает `Polling.ColdIntervalSeconds` (дефолт 15 с) каждый цикл; на старте — немедленный warm-up снимка (без окна «ещё не обновлялось» при `CapturedAtUtc = MinValue`). Вне Hangfire, т.к. минута-минимум CRON делала настройку инертной (MLC-154, ADR-6.1). Cold↔hot сериализует `IEnforcementGate`; петля последовательна, узел один (ADR-28) — распределённый лок не нужен. |
| `HotTierPollingService` | Sub-minute опрос сеансов 1С для клиентов «у лимита» (hot-tier, 3–5 с). Hangfire CRON не подходит (минимум 1 мин). |
| `RasHealthProbingService` | Независимый 30-секундный ping-loop: публикует `IRasHealthReader`-снимок для дашборда. |
| `PerfRecordingSamplingService` | Тикает по таймеру при активной записи быстродействия, вызывает `PerfRecordingService.SampleOnceAsync`. |
| `BackupPumpService` | Оркестратор очереди on-demand бэкапов; тикает по wake-сигналу или таймауту. |

Подробности поведения горячего цикла и enforcement — в `docs/02_ARCHITECTURE.md`.

**Live-форс cold-обхода (MLC-156).** `ColdTierPollingService` реализует Application-порт
`ISessionRefreshTrigger.RunNowAsync(ct)`: будит петлю досрочно (прерывает ожидание таймера),
прогоняет cold-цикл сейчас и завершает Task по его окончании. Single-flight — несколько
одновременных вызовов коалесцируются в один ближайший прогон. Через порт (а не прямую
инъекцию `Infrastructure.Jobs` в Web) граница слоёв сохранена (ADR-5/16/20). Потребитель —
эндпоинт `POST /api/v1/sessions/refresh` (Viewer, 204 без тела, без аудита) под кнопкой
«Обновить сейчас» на «Сеансах»: фронт ждёт ответ и перечитывает `GET /sessions/snapshot`.

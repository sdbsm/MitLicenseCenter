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
- `quotaExceeded` / `quotaAtLimit` / `quotaNearLimit` — число активных тенантов с положительным
  лимитом в трёх ФАКТИЧЕСКИХ бакетах по `consumed` vs `limit` (MLC-193, зеркало `quotaLabel` из
  `frontend/src/lib/quota.ts`): превышение (`consumed > limit`), лимит достигнут (`consumed == limit`)
  и близко к лимиту (ниже лимита, но процент ≥ 75 %). Бакеты непересекающиеся; нижняя граница
  «близко» — константа `QuotaWarningThreshold` в синхроне с `QUOTA_WARNING_THRESHOLD`. Цвет-severity
  (danger ≥ 90 %) — визуальный язык фронта, в бакетах не участвует. Потребление — факт rac
  (`Consuming`, ADR-48).
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
службы берётся из `OneC.RAS.Endpoint`, платформа — из `OneC.DefaultPlatformVersion`, цель —
локальный агент кластера `localhost:<OneC.RAS.AgentPort>` (порт по умолчанию `1540`, настройка
MLC-194; меняется только при нестандартном порту агента 1С).

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

### 3.7 Надёжный контроллер службы Windows (ADR-55, MLC-212)

`IWindowsServiceController` (реализация `WindowsServiceController`, Infrastructure) — универсальный
контроллер мутирующих операций над службой Windows **по имени** (`StartAsync`/`StopAsync`/
`RestartAsync`), обобщающий RAS-паттерн на любую службу узла (single-host, ADR-28). Каждая операция
подчиняется контракту надёжности ADR-55:

- **Команда + верификация.** После команды (`sc start <name>` / `sc stop <name>`, переиспользуется
  `IScProcessRunner`) контроллер **опрашивает фактическое состояние** службы через `ServiceController`
  (`IServiceStateReader`) до целевого (`Running` / `Stopped`) и возвращает достоверный
  `WindowsServiceOperationResult` с верифицированным `FinalStatus`, а не факт отправки команды.
- **Ограниченное ожидание.** Между опросами — пауза `PollInterval` (дефолт ~500 мс) через
  `TimeProvider`; общий бюджет — `VerificationTimeout` (дефолт 30 с, «sc 30 с» ADR-55). По истечении
  без перехода → `WindowsServiceOperationException` (позже эндпоинт MLC-213+ мапит в `409`), а не
  зависание/падение.
- **Идемпотентность.** `sc start` код `1056` (ALREADY_RUNNING) и `sc stop` код `1062` (NOT_ACTIVE) —
  успех (те же константы, что в RAS-адаптере). Ненулевой не-идемпотентный код `sc` → исключение.
- **Сериализация.** Мутации одной службы — под `IServiceOperationGate.AcquireAsync(serviceName)`
  (`ServiceOperationGate`, per-service-name `SemaphoreSlim(1,1)` в `ConcurrentDictionary`). В отличие
  от глобального `IIisResetConcurrencyGate` (один слот на весь процесс), здесь замок по ключу-имени:
  старт службы A и стоп службы B идут параллельно. `RestartAsync` = верифицированный стоп, затем
  верифицированный старт под **одним** захватом замка.

Это только Infrastructure-слой + интерфейс Application; эндпоинты появляются в §3.8 (MLC-213),
frontend — в MLC-214. 800-серия аудита (`OneCServerStarted`/`Stopped`/`Restarted`) объявлена в
MLC-212, пишется эндпоинтами `POST /api/v1/server/onec/*` (§3.8).

---

### 3.8 Агрегатор статусов служб узла + управление сервером 1С (ADR-54/55, MLC-213)

Группа `/api/v1/server` (`ServerEndpoints`) даёт сводный статус стека узла, управление
**только сервером 1С** (служба `ragent`) и обслуживание (свежесть бэкапов SQL, read-only,
MLC-216). Single-host (ADR-28); анти-коррупционная граница (ADR-20): всё через
`IServerStatusProvider` / `IWindowsServiceController` / `IMaintenanceProbe`, без прямого
`sc.exe`/реестра/`Process`/`ServerManager` в Web.

- **`GET /server/status` (Viewer)** — сводный снимок через `IServerStatusProvider`
  (`ServerStatusProvider`, Infrastructure), композирующий четыре источника:
  - **сервер(ы) 1С** — обнаружение служб `ragent` через реестр (`IServiceRegistryReader`, один
    проход без спавнов — тот же приём, что RAS-discovery MLC-162) + состояние
    (`IServiceStateReader`); несколько установленных версий платформы = несколько служб
    (версия — best-effort из ImagePath `...\<N.N.N.N>\bin\ragent.exe`);
  - **RAS** — `IRasServiceManager.DiagnoseAsync` (только наблюдение);
  - **SQL** — статус локальной службы `sqlservr` (discovery по ImagePath + состояние) + имя
    инстанса (`ISqlInstanceDiscovery`, best-effort), **never-throws**; только наблюдение;
  - **IIS** — `IIisLifecycleService.GetServerStateAsync` (только наблюдение).

  Каждый источник обёрнут в try/catch (кроме `OperationCanceledException`): сбой одного
  адаптера отражается его флагами `Available:false`/`Error`, а не падением всего снимка (как
  discovery `IisEndpoints` — эндпоинт **не 500-ит**, провайдер уже вернул флаги). Снимок несёт
  общий индикатор здоровья `Overall` — простая эвристика «светофора» для FE:
  `Down` — ни одной запущенной службы `ragent` (сервер 1С — ядро узла); `Degraded` — есть
  запущенный `ragent`, но что-то не так (RAS не `Ok`/не running, SQL не running, IIS не
  `Started`, либо любой адаптер `Available:false`); `Healthy` — всё доступно и в норме;
  `Unknown` — опросить узел вообще не удалось (служб `ragent` нет и все адаптеры недоступны).

- **`POST /server/onec/{start,stop,restart}` (Admin)** — мутации **только сервера 1С** через
  `IWindowsServiceController` (§3.7, верифицированный итог). Перед вызовом — **whitelist цели**:
  имя службы (trim) должно быть среди обнаруженных служб `ragent` (по снимку
  `IServerStatusProvider`), иначе `404` (нельзя дёргать произвольную службу / SQL / саму
  панель — требование безопасности); пустое имя → `ValidationProblem`. `stop`/`restart` —
  разрушительны (прерывают работу всех баз узла), поэтому за **серверным Confirm-гейтом**
  (`Confirm=false` → `409 SERVER_CONFIRM_REQUIRED`); `start` подтверждения не требует.
  `WindowsServiceOperationException` → `409 SERVER_OPERATION_FAILED` (санитизированный текст +
  `correlationId`). При успехе — аудит **800-серии** (`OneCServerStarted`/`Stopped`/`Restarted`),
  server-scope `TenantId=null`, имя службы в описании (секретов нет).

- **`GET /server/maintenance/backups` (Viewer)** — свежесть резервных копий баз для вкладки
  **«Обслуживание»** (MLC-216): **live-read** `msdb.dbo.backupset` через `IMaintenanceProbe`
  (`SqlMaintenanceProbe`, Infrastructure, чистый ADO.NET) — **БЕЗ собственных таблиц/миграций/
  Hangfire-джоб**. По каждой пользовательской базе (`sys.databases`, `database_id > 4`) — время
  завершения последнего бэкапа каждого типа (`full`/`diff`/`log`, `backupset.type` `D`/`I`/`L`)
  и вычисленный флаг **`isStale`** («устарел»). Порог свежести — **фиксированная константа
  ~26 часов для full** (`BackupFreshnessPolicy.FullFreshnessThreshold` = сутки + 2ч запас на
  длительность задания/сдвиг расписания; отдельной настройки не заводим, ADR-54): база «устарела»,
  если нет ни одного `full`-бэкапа **либо** последний `full` старше порога. Источник —
  тот же `msdb.dbo.backupset` (вокруг `backup_finish_date`/`compressed_backup_size`), что читает
  `SqlBackupAdapter`; фича on-demand бэкапа (`COPY_ONLY`, ADR-27) этим **не затрагивается**. Ручные
  `COPY_ONLY`-бэкапы (`is_copy_only = 1`) **исключены** из сигнала свежести — вкладка отражает
  **штатный** бэкап-режим (разовый ручной бэкап панели не маскирует провал ночного плана).
  Права — `HAS_PERMS_BY_NAME('msdb.dbo.backupset','OBJECT','SELECT')`: **never-throws** — нет
  прав → статус `PermissionDenied`, SQL недоступен / строка не настроена → `Unavailable` (в обоих
  случаях список баз пуст), эндпоинт **не 500-ит**. Строка подключения/инстанс — как соседние
  пробы (`SettingKey.SqlServer`, наследование `Trusted_Connection`/`Encrypt` из `Default`,
  `master`); таймауты Connect 15s / Command 30s (как `DatabaseSizeProbe`).

- **`GET /server/maintenance/plans` (Viewer)** — планы обслуживания SQL для вкладки
  **«Обслуживание»** (MLC-217): **live-read** `msdb.dbo.sysmaintplan_*` + история заданий
  SQL Agent через **ту же** пробу `IMaintenanceProbe` (`SqlMaintenanceProbe`, новый метод
  `GetMaintenancePlansAsync`) — **БЕЗ собственных таблиц/миграций/Hangfire-джоб**. Источники
  `msdb`: `sysmaintplan_plans`/`sysmaintplan_subplans` (планы и под-планы; под-план привязан к
  заданию SQL Agent по `job_id`); `sysmaintplan_log` — последний прогон под-плана (время +
  итог `succeeded`, длительность = `end_time − start_time`); `sysmaintplan_logdetail` —
  **детализация по шагам** последнего прогона (что именно делалось/упало: проверка целостности /
  бэкап / реиндекс); `sysjobschedules`/`sysschedules` — есть ли у задания **включённое
  расписание**. Время лога — локаль SQL-хоста → UTC (как backupset).
  - **Различение «по расписанию» vs «по запросу»:** под-план «по расписанию» = у его задания
    есть включённое расписание (`sysschedules.enabled = 1`); под-план без такого — **ручной**
    (владелец держит ручные под-планы «перестроение индекса»/«month»). Классификация итога
    прогона — **чистая `SubplanRunPolicy`** (тестируется без SQL): `Succeeded` / `Failed` /
    `Overdue` / `NeverRun` / `Unknown`. **«Просрочен» (`Overdue`) считается ТОЛЬКО для под-плана
    С расписанием** (нет истории при расписании → `Overdue`; успех, но последний прогон старше
    порога ~26ч `SubplanRunPolicy.ScheduledOverdueThreshold` → `Overdue`); ручной под-план без
    прогонов → `NeverRun` (норма, не алерт). Сигнал «требует внимания» (`IsAlerting`) — только
    `Failed`/`Overdue`. **Допущение:** порог `Overdue` по времени рассчитан на **суточную**
    каденцию расписания (типовой ночной план); под-план с недельным/месячным расписанием после
    26ч простоя будет помечен `Overdue` ложно. Для целевого узла это не проявляется (штатный план —
    суточный); каденция-зависимый порог — возможная доработка, если появятся не-суточные расписания.
  - **Never-throws + деградация статусом** (`MaintenancePlansStatus`, строкой на проводе):
    нет прав на `msdb.dbo.sysmaintplan_plans` (`HAS_PERMS_BY_NAME`) → `PermissionDenied`;
    **SQL Agent отсутствует/остановлен** (Express-редакция) или нет доступа к
    `msdb.dbo.sysjobhistory` → **`AgentUnavailable`** (честный «агент недоступен», не ошибка);
    SQL недоступен / строка не настроена → `Unavailable`; в degraded-ветках `plans` пуст,
    эндпоинт **не 500-ит**. Строка подключения/инстанс/таймауты — как у пробы свежести бэкапов.

- **`GET /server/auto-restart` (Viewer)** / **`PUT /server/auto-restart` (Admin)** — расписание
  ночного авто-рестарта сервера 1С для карточки **«Расписание авто-рестартов»** во вкладке
  «Службы» (MLC-218, ADR-55). `GET` возвращает `{enabled, time, lastRunUtc?, targetServices}`:
  текущее состояние расписания (`OneC.AutoRestart.Enabled`/`Time`), время последнего прогона
  джобы (`OneC.AutoRestart.LastRunUtc`, UTC; null опускается — ещё не запускалась) и **целевые
  службы** (запущенные `ragent` из снимка `IServerStatusProvider` — что именно рестартнётся).
  `PUT` принимает `{enabled, time}`: валидирует `time` как `HH:mm` (00:00–23:59,
  `AutoRestartTimeRegex`; невалидное → `ValidationProblem`, мутация не идёт), канонизирует к
  двум разрядам, пишет обе настройки, **перерегистрирует Hangfire-джобу** через
  `OneCAutoRestartScheduler.Apply` (включено → `RecurringJob.AddOrUpdate` с дневным cron в
  местном поясе; выключено → `RemoveIfExists` — **НЕ** тик-каждые-5-минут) и пишет аудит
  изменения расписания (`OneCServerAutoRestartScheduleChanged = 804`, server-scope). Тело самой
  джобы — см. §8.1.

- **`GET /server/onec/processes` (Viewer)** — рабочие процессы 1С (`rphost`) для блока
  **«Рабочие процессы 1С»** во вкладке «Службы» (MLC-219, ADR-54): **live-read** `rac process
  list` через **тот же** порт `IClusterClient.ListProcessesAsync` (`RacExecutableRasClusterClient`),
  что питает «Быстродействие» — без нового адаптера и без прямого спавна `rac.exe` в Web
  (vertical slice, ADR-20). Отдаёт список процессов кластера: UUID процесса, `pid`,
  `availablePerformance` (APDEX-подобный индикатор доступной производительности), `avgCallTime`
  (средняя длительность вызова, секунды) и `memorySize` (байты); perf-поля nullable, **null
  опускается** на проводе (парсер «never throws» — на иных версиях платформы поля может не быть).
  **Never-throws + деградация пустым списком:** `rac` не настроен/недоступен → адаптер отдаёт
  пустой список, эндпоинт **не 500-ит**.

- **`POST /server/onec/processes/restart` (Admin)** — мягкий рестарт рабочего процесса 1С
  (`rphost`) по `Pid` (MLC-220, **ADR-56**). У `rac` нет команды «restart process» → рестарт =
  **завершение ОС-процесса `rphost` по `Pid`**, после чего кластер 1С авто-поднимает новый процесс
  (с другим `Pid`). Тело `{ pid, confirm }`. Логика — `IOneCProcessRestartService`
  (`OneCProcessRestartService`, Infrastructure/Server), контракт безопасности:
  **(1) whitelist** — завершаем только `Pid` из текущего `rac process list`
  (`IClusterClient.ListProcessesAsync`); не в списке → `404`. **(2) guard от переиспользования
  `Pid`** — перед kill проверяем, что ОС-процесс с этим `Pid` действительно `rphost`
  (`ILocalProcessTerminator.GetProcessName`, имя без расширения); не `rphost` → `409`; процесса уже
  нет → идемпотентный успех. **(3) верификация исчезновения** (дух ADR-55) — опрашиваем
  `rac process list`, пока старый `Pid` не исчезнет, в пределах таймаута (~30 с,
  `OneCProcessRestartOptions`, через `TimeProvider`); исчез → успех, не исчез → `409`. **(4)
  идемпотентность** — уже отсутствующий `Pid` → успех. **Маппинг:** не-confirm →
  `409 PROCESS_CONFIRM_REQUIRED`; `Pid` не в whitelist → `404`; переиспользован/таймаут →
  `409 PROCESS_RESTART_FAILED`; успех → `200` + аудит `OneCProcessRestarted = 805` (server-scope,
  описание несёт `Pid`). Завершение ОС-процесса инкапсулировано в `ILocalProcessTerminator`
  (Application) + реализация поверх `System.Diagnostics.Process` (Infrastructure) — `Process` не
  течёт в Web (ADR-20); чистая классификация исхода — `OneCProcessRestartPolicy` (юнит).

Управление **RAS и IIS здесь не дублируется** (остаётся в `/ras-service/*` и `/api/v1/iis/*`);
**SQL — без управления** (ADR-54, только наблюдение).

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

Ключи **`OneC.AutoRestart.Enabled`** (`Number` 0/1, дефолт 0), **`OneC.AutoRestart.Time`**
(`Text`, `HH:mm`, дефолт `04:00`) и **`OneC.AutoRestart.LastRunUtc`** (`Text`, UTC, без дефолта)
обслуживают расписание авто-рестартов сервера 1С (MLC-218, §3.8/§8.1). Управляются
преимущественно карточкой «Расписание авто-рестартов» (эндпоинты `/server/auto-restart`,
строгая валидация `HH:mm` там); в каталоге — для сидинга дефолтов и общего whitelist (генерик
`PUT /settings/{key}` строгий формат `Time` не проверяет — штатный путь правки — карточка).
`LastRunUtc` заполняется **самой джобой**, оператор его не задаёт.

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
управление сервером 1С (старт/стоп/рестарт — **800-серия**: `OneCServerStarted = 800`,
`OneCServerStopped = 801`, `OneCServerRestarted = 802`, server-scope `TenantId=null`,
значения добавлены в MLC-212, пишутся `POST /api/v1/server/onec/*` (MLC-213));
авто-рестарт сервера 1С (**803/804**: `OneCServerAutoRestarted = 803` — срабатывание ночной
джобы, initiator `"system"`, описание перечисляет рестартнутые службы; `OneCServerAutoRestartScheduleChanged = 804`
— Admin изменил расписание через `PUT /api/v1/server/auto-restart`; server-scope — MLC-218);
рестарт рабочего процесса 1С (`rphost`) (**805**: `OneCProcessRestarted = 805` — Admin завершил
ОС-процесс `rphost` по `Pid` через `POST /api/v1/server/onec/processes/restart`, server-scope
`TenantId=null`, описание несёт `Pid`, только при фактическом успехе — MLC-220, ADR-56);
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
ночным джобом `audit-retention` (03:00 по часам хоста — ADR-52). Сам факт очистки пишется в аудит (действие
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
| `audit-retention` | `IAuditRetentionJob.RunAsync` | `0 3 * * *` | ежедневно 03:00 по часам хоста |
| `backup-retention` | `IBackupRetentionJob.RunAsync` | `15 3 * * *` | ежедневно 03:15 по часам хоста |
| `license-usage-retention` | `ILicenseUsageRetentionJob.RunAsync` | `30 3 * * *` | ежедневно 03:30 по часам хоста |
| `perf-recording-retention` | `IPerfRecordingRetentionJob.RunAsync` | `45 3 * * *` | ежедневно 03:45 по часам хоста |
| `database-size-collection` | `IDatabaseSizeCollectionJob.RunAsync` | `0 2 * * *` | ежедневно 02:00 по часам хоста |
| `database-size-retention` | `IDatabaseSizeRetentionJob.RunAsync` | `0 4 * * *` | ежедневно 04:00 по часам хоста |
| `onec-auto-restart` | `IOneCAutoRestartJob.RunAsync` | из настройки `OneC.AutoRestart.Time` | ежедневно в заданное время по часам хоста (только когда расписание включено) |

Шесть суточных джоб планируются по **местному поясу хоста** (`TimeZoneInfo.Local`) через общий
`NightlyJobSchedule.LocalTimeZoneOptions` (`RecurringJobOptions { TimeZone = ... }`), а не по UTC —
чтобы «ночные» работы шли ночью по часам сервера (ADR-52); времена в колонке «Расписание» — местные.
`publication-status-refresh` к поясу нечувствителен (оставлен на дефолте). Хранение/транспорт меток
времени остаются UTC (ADR-23).

**Авто-рестарт сервера 1С** (`onec-auto-restart`, MLC-218, ADR-55) — единственная джоба с
**настраиваемым оператором** расписанием: cron строится из `OneC.AutoRestart.Time` (ежедневно в
`HH:mm`, тот же местный пояс) и **регистрируется только когда `OneC.AutoRestart.Enabled = 1`**.
Регистрация/снятие — через `OneCAutoRestartScheduler.Apply` (`RecurringJob.AddOrUpdate` при
включении, `RecurringJob.RemoveIfExists` при выключении): на старте `Program.cs` применяет текущую
настройку, при изменении расписания — эндпоинт `PUT /server/auto-restart` (§3.8). **Не** тик-каждые-
5-минут. Тело джобы (`OneCAutoRestartJob`, Infrastructure): проверяет `Enabled` авторитетно через
`ISettingsStore` (выключено → no-op — защита от рассинхрона, если задание задержалось в сторадже);
находит **запущенные** службы `ragent` через discovery (MLC-213) и рестартит **каждую** через
надёжный `IWindowsServiceController` (MLC-212, верификация состояния встроена; per-service-сбой не
валит весь прогон — логируется, остальные продолжаются); пишет аудит срабатывания (`OneCServerAutoRestarted = 803`,
initiator `"system"`) и обновляет `OneC.AutoRestart.LastRunUtc`. `[AutomaticRetry(Attempts = 0)]` —
рестарт разрушителен (прерывает все базы узла), повтор при сбое не нужен, самоисцеление = следующий
суточный прогон.

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
| `TechLogWatchdogService` | Драйвер безопасного сбора ТЖ: на старте orphan-recovery (`Active`→`Interrupted`) → сверка `logcfg.xml` (снимает «забытый» конфиг без активного дела), затем периодический сторож активного дела — авто-стоп по окну времени и лимиту места. |

Подробности поведения горячего цикла и enforcement — в `docs/02_ARCHITECTURE.md`.

**Live-форс cold-обхода (MLC-156).** `ColdTierPollingService` реализует Application-порт
`ISessionRefreshTrigger.RunNowAsync(ct)`: будит петлю досрочно (прерывает ожидание таймера),
прогоняет cold-цикл сейчас и завершает Task по его окончании. Single-flight — несколько
одновременных вызовов коалесцируются в один ближайший прогон. Через порт (а не прямую
инъекцию `Infrastructure.Jobs` в Web) граница слоёв сохранена (ADR-5/16/20). Потребитель —
эндпоинт `POST /api/v1/sessions/refresh` (Viewer, 204 без тела, без аудита) под кнопкой
«Обновить сейчас» на «Сеансах»: фронт ждёт ответ и перечитывает `GET /sessions/snapshot`.

### Сервисы управления технологическим журналом (трек 1.2)

К существующему live-режиму «Наблюдение» добавляется режим «Расследование» (ADR-57): сбор
технологического журнала 1С под управлением панели. Реализованы слой **управления `logcfg.xml`**
(генератор + store + сервис жизненного цикла + сторож на старте), **безопасный сбор** (окно/лимит
места/авто-стоп, single-active по БД, orphan-recovery), **NDJSON-парсер** собранного ТЖ и четыре
агрегатора-анализатора: **управляемых блокировок 1С** (MLC-233), **долгих запросов к СУБД**
(MLC-234), **исключений платформы** (MLC-235) и **СУБД-блокировок** (MLC-236, дерево по `lkX`).
Этап B (парсер + анализаторы) завершён.

- **`ILogcfgBuilder`** (Application) / `LogcfgBuilder` (Infrastructure) — чистый генератор целевого
  `logcfg.xml` по (сценарий, имя ИБ?, каталог сбора, history). Конфиг строго целевой по event-scope
  (ADR-58): `format="json"`, ограниченный `history`, набор событий по сценарию (`TechLogScenario`:
  блокировки `TLOCK/TTIMEOUT/TDEADLOCK`+`SDBL`; долгие запросы `DBMSSQL/SDBL`+`<plansql/>`; исключения
  `EXCP/EXCPCNTX`+`<dump/>`; общая медленная работа `CALL/DBMSSQL`; СУБД-блокировки `DBMSSQL`+`<dbmslocks/>`)
  — никогда полный event-scope
  (`<ne name="">`). **Свойства событий пишутся только при наличии `<property>` (офиц. спека
  `41_LOGCFG_SPEC` §4.2), поэтому конфиг содержит `<property name="all"/>` внутри `<log>` — иначе
  события писались бы лишь с базовыми полями и анализаторы получили бы пусто. Это property-scope (все
  свойства уже отобранных событий), НЕ «полный ТЖ»: объём режут фильтры события + арендатора + окно +
  `history`.**
  **Теги-обогатители `<plansql/>` и `<dump/>` — глобальные директивы УРОВНЯ `<config>` (рядом с `<log>`,
  не внутри него; шаблоны infostart 2020498/1431026): `<plansql/>` включает захват планов (план попадает
  в свойство `planSQLText`, которое пишется благодаря `<property name="all"/>`), `<dump/>` (с атрибутами
  `location`/`create`/`type`, дампы в подкаталог каталога сбора → их объём учитывает сторож размера) —
  дампы аварий. ⚠ Тип/объём дампа — за стенд-приёмкой (full-dump тяжёл на проде).**
  **Объём режется только типом события и `p:processName`: фильтр по длительности в `logcfg` не ставится
  (прежнее `Dur` — несуществующее свойство, `41_LOGCFG_SPEC` §5; верный `Duration` — за стенд-ретестом,
  F-3), порог длительности применяет парсер на этапе разбора.** При заданном имени ИБ конфиг обязан
  содержать точный `<eq property="p:processName" value="…"/>` **внутри каждого `<event>`** (изоляция
  арендатора, объединение по «И» с `name`; bare-`<eq>` под `<log>` платформа игнорирует) — `<like>` не
  используется (в JSON не фильтрует). В конфиг встроен опознавательный XML-маркер, по которому сторож
  отличает «наш» конфиг от чужого.
- **`ITechLogParser`** (Application) / `TechLogParser` (Infrastructure) — построчный парсер собранного
  ТЖ. Чистый C# без файловой системы (как `ILogcfgBuilder`): принимает строки/`TextReader`, ленивое
  потоковое перечисление событий (память не растёт на больших журналах). По фактам стенда (KB
  `40_TECHLOG §4/§7`): вход — **NDJSON** (один JSON-объект на строку, не массив), BOM в начале снимается;
  событие — **плоский объект** `TechLogEvent` (базовые поля `ts/duration/name/depth/level/process` +
  свойства верхнего уровня), **все значения — строки**. Парсер **толерантен к дублям ключей** (`t:clientID`,
  `Func` у завершения транзакции `SDBL`, `p:processName` при динамическом обновлении пишутся дважды):
  сохраняет **все** вхождения списком пар и даёт аксессоры «первое/последнее/все значения по ключу»
  (разбор потоковым `Utf8JsonReader`, не `Dictionary` — иначе теряются дубли). **`duration`
  нормализуется µs→секунды** (`60005971` µs → ≈60.006 с; единица — микросекунды, факт стенда MLC-229),
  с сохранением сырого строкового значения; отсутствие/пустое/нечисловое значение → нормализованные поля
  `null`. Парсер **никогда не бросает**: пустые строки пропускаются, невалидная JSON-строка пропускается
  со счётчиком (`SkippedLines` в результате), а не валит весь файл; «поля-призраки» (нет `Sql`/`p:processName`)
  → аксессор возвращает `null`. На нём построен анализатор управляемых блокировок (ниже).
- **`ILockTreeAnalyzer`** (Application) / `LockTreeAnalyzer` (Infrastructure) — анализатор
  **управляемых блокировок уровня 1С** (MLC-233). Строит из `IEnumerable<TechLogEvent>` дерево
  «кто кого ждёт» (`WaitEdges`, рёбра из `TLOCK` с непустым `WaitConnections`), список таймаутов
  ожидания (`Timeouts`, из `TTIMEOUT`) и список взаимоблокировок (`Deadlocks`, из `TDEADLOCK`).
  Привязка к ИБ через `p:processName`: у фоновых/динамических сессий имя может иметь GUID-суффикс
  (`<имя ИБ>_<GUID>`) — нормализуется к базовому имени с сохранением сырого значения (`40_TECHLOG §8`).
  **Граница: ТОЛЬКО 1С-уровень** (менеджер блокировок платформы). Блокировки уровня СУБД НЕ видны
  в этих событиях — их источник отдельный тег `<dbmslocks/>` с полями `lkX`: реализовано анализатором
  `IDbmsLockAnalyzer` (MLC-236, ниже); не обещать единое дерево всех блокировок, иначе ложная полнота
  (`40_TECHLOG §5`). Устойчив к «полям-призракам»
  (`WaitConnections` может отсутствовать/быть пустым, `escalating` — только при `=true` и т.д.),
  вариантам имён (`Usr`/`UserName`, `40_TECHLOG §7`), дублям ключей. **Никогда не бросает**:
  нераспознанные/неполные события пропускаются без ошибки. Stateless singleton.
- **`IDbmsLockAnalyzer`** (Application) / `DbmsLockAnalyzer` (Infrastructure) — анализатор
  **блокировок уровня СУБД** (MLC-236). Строит из `IEnumerable<TechLogEvent>` дерево «жертва → источник»
  (`WaitEdges`) по полям `lkX` событий `DBMSSQL`: связка `жертва.lksrc → источник.connectID`
  (`40_TECHLOG §5`, `41_LOGCFG_SPEC §8`, infostart 1431026); жертва без найденного в окне источника —
  ребро с `SourceMatched=false` (счётчик `UnmatchedVictimCount`). Сбор включается сценарием `DbmsLocks`
  (`<dbmslocks/>` формирует поля, `<property name="all"/>` их выводит). **Граница: ТОЛЬКО СУБД-уровень** —
  ОТДЕЛЬНЫЙ механизм от управляемых блокировок 1С (`ILockTreeAnalyzer`); не путать. Привязка к ИБ через
  `TechLogProcessName.Normalize`, устойчив к «полям-призракам» (`lkX` появляются только при блокировке),
  **никогда не бросает**. Stateless singleton. ⚠ Структура полей `lkX` собрана по документации (infostart
  1431026) — точная форма в JSON-ТЖ 8.5 **подлежит подтверждению на стенде** (приёмка владельца).
- **`ISlowQueryAnalyzer`** (Application) / `SlowQueryAnalyzer` (Infrastructure) — анализатор
  **долгих запросов к СУБД** (MLC-234). Строит из `IEnumerable<TechLogEvent>` топ медленных вызовов
  (`TopQueries`, отсортирован по длительности убывающим, топ-N) и группы похожих запросов
  (`SimilarGroups`, агрегат по нормализованному SQL: строковые/числовые литералы и параметры
  схлопываются к `?`). **Граница: ТОЛЬКО `DBMSSQL`**. Ключевой факт стенда (`40_TECHLOG §6`):
  фильтр по длительности (`<ge property="Dur"/>`) в `logcfg` для JSON-ТЖ 8.5 не работает — **порог
  длительности (`thresholdMicroseconds`) применяет этот анализатор**, а не logcfg. Объём входа
  режется типом события `DBMSSQL` и `p:processName` в logcfg (оба работают). Привязка к ИБ через
  общий хелпер `TechLogProcessName.Normalize` (`40_TECHLOG §8`): отсекает GUID-суффикс фоновых
  сессий, сохраняет сырое значение в `RawProcessName`. Текст плана запроса — best-effort: поле
  плана собирается **только при явном теге `<plansql/>`** в logcfg (`40_TECHLOG §5/§6`); по
  умолчанию не собирается; точное имя поля плана на стенде 8.5 подлежит подтверждению (кандидат
  `PlanSQLText`). Устойчив к «полям-призракам» (`Sql` у DBMSSQL иногда отсутствует): запись без
  `Sql` попадает в `TopQueries` с `Sql=null`, но в `SimilarGroups` не входит (нечего нормализовать,
  `40_TECHLOG §7`). **Никогда не бросает**. Stateless singleton.
- **`IExceptionAnalyzer`** (Application) / `ExceptionAnalyzer` (Infrastructure) — анализатор
  **исключений платформы** (MLC-235). Строит из `IEnumerable<TechLogEvent>` топ по частоте
  (`TopExceptions`, группировка по паре «тип `Exception` + нормализованный `Descr`»: числа/hex-литералы
  схлопываются к `#`, пробелы нормализуются — «timeout 100» и «timeout 999» в одну группу). **Граница:
  ТОЛЬКО `EXCP`**. Группы с `Exception=DataBaseException` помечаются флагом `IsDatabaseException`
  (кандидаты на блокировки/дедлоки СУБД); честная оговорка (`40_TECHLOG §7`): **дедлок СУБД пишет ДВА
  `EXCP`** — частота `DataBaseException` может удваивать число инцидентов (оценка `Count / 2`), точная
  пара-корреляция — после подтверждения на стенде. Привязка к ИБ через общий хелпер
  `TechLogProcessName.Normalize`. Устойчив к специфике `EXCP` (`40_TECHLOG §7`: «вездесущ», «ложные
  `EXCP` при окне авторизации», поля-призраки `Descr`/`Context`): событие без `Exception`/`Descr`
  обрабатывается толерантно (тип `null` / плейсхолдер «(без описания)»), не валит разбор. **Никогда не
  бросает**. Stateless singleton.
- **`ILogcfgStore`** (Application) / `LogcfgStore` (Infrastructure) — файловые операции над
  `<корень 1С>\conf\logcfg.xml` (путь через тот же `OneCInstallRoots`, что у rac/ras): чтение/запись,
  резервная копия исходного перед установкой и восстановление из неё при снятии, **проба прав записи**.
  Сервисный аккаунт `NT SERVICE\MitLicenseCenter` по умолчанию прав записи в `conf` не имеет (ADR-8/58):
  при отказе store возвращает структурный результат с точной командой
  `icacls "…\logcfg.xml" /grant "NT SERVICE\MitLicenseCenter:(M)"` для оператора (тот же паттерн
  «детектируем проблему прав → отдаём точную команду», что у healing службы RAS). Грант установщик
  не выдаёт в этой задаче.
- **`ITechLogCollectionService`** (Application) / `TechLogCollectionService` (Infrastructure, singleton)
  — жизненный цикл «дела сбора» (зеркаль `PerfRecordingService`: один `SemaphoreSlim`,
  `IServiceScopeFactory` для scoped `AppDbContext`/`IAuditLogger`, `TimeProvider`). `InstallAsync`:
  **single-active по БД** (есть `Status==Active` дело → `AlreadyActive`, ещё до записи конфига —
  переживает потерю in-memory стейта при рестарте) → проба прав → **сторож свободного места** (меньше
  `TechLog.MinFreeDiskMb` → структурный отказ `InsufficientDiskSpace`, конфиг не пишется) → генерация
  конфига → бэкап исходного → запись → дело `TechLogCollection` (`Active`) → аудит. `RemoveAsync`:
  восстановление исходного `logcfg` → дело `Stopped` (с причиной) → аудит. `MonitorActiveAsync`
  (периодический сторож активного дела): по окну времени (`TechLog.MaxDurationMinutes` → авто-стоп
  `TimeLimit`) и размеру каталога сбора (`TechLog.DiskLimitMb` → авто-стоп `DiskLimit`). Измерение
  свободного места и размера каталога — за seam'ом `ILogcfgStore` (детерминированные тесты).
  `RecoverInterruptedAsync`: на старте все `Active` → `Interrupted` (зеркаль `PerfRecording`). Идемпотентно:
  повторная установка не переписывает конфиг, снятие хранит снимок установленного.
- **`TechLogWatchdogService`** (hosted `BackgroundService`, зеркаль `PerfRecordingSamplingService`) —
  **драйвер безопасного сбора**. На старте — orphan-recovery (`RecoverInterruptedAsync`,
  `Active`→`Interrupted`), **затем** стартовая сверка файла (`ReconcileOnStartupAsync`): если в `conf`
  лежит наш конфиг (по маркеру), но активного дела в БД нет (краш ОС оставил «забытый» конфиг) —
  принудительно восстанавливает исходный и пишет аудит. **Порядок критичен:** сначала перевести
  осиротевшее дело в `Interrupted`, потом снять «забытый» `logcfg` — так после рестарта сбор и помечается
  прерванным, и его конфиг снимается. Затем — петля с коротким фиксированным интервалом, на каждом тике
  `MonitorActiveAsync` (авто-стоп по окну/лимиту места), no-op при отсутствии активного дела.

Сущность `TechLogCollection` (Domain) — прокси «активного дела» (мигрирует в полноценную сущность
расследования позже); телеметрия в `dbo.TechLogCollections`, enum'ы `TechLogCollectionStatus`/
`TechLogCollectionStopReason` (`Manual`/`TimeLimit`/`DiskLimit`) хранятся `int`'ом (`HasConversion<int>`,
frozen-int как у `PerfRecording*`). Настройки: `TechLog.CollectionRoot` (каталог сбора, дефолт под
`%PROGRAMDATA%`), `TechLog.HistoryHours` (короткий дефолт по политике безопасности),
`TechLog.MaxDurationMinutes` (окно авто-снятия), `TechLog.DiskLimitMb` (потолок размера каталога),
`TechLog.MinFreeDiskMb` (порог свободного места перед стартом). Аудит —
`TechLogCollectionStarted/Stopped/ConfigForceRestored` (host-scope, новые int-значения 806–808 — enum
аудита заморожен); авто-стоп переиспользует `TechLogCollectionStopped` (807) с причиной в описании.

Формат ТЖ, события, шаблоны `logcfg.xml` и грабли парсера — база знаний
[`../research/perf-investigation/40_TECHLOG.md`](../research/perf-investigation/40_TECHLOG.md);
политика безопасного сбора — [`60_SAFETY.md`](../research/perf-investigation/60_SAFETY.md). Направление
зафиксировано в ADR-57 (двухрежимный раздел) и ADR-58 (безопасный сбор ТЖ).

# Доменная модель и схема данных

Этот документ описывает сущности MitLicense Center, их связи, инварианты,
жизненные циклы, замороженные целочисленные значения перечислений, правила
лимитов лицензий и схему базы данных «как построено». Источник всех утверждений —
доменный код (`MitLicenseCenter.Domain`), сущности-телеметрии в Infrastructure,
конфигурации EF в `AppDbContext.OnModelCreating` и снимок модели
`AppDbContextModelSnapshot.cs`. Терминология — по `01_OVERVIEW.md`, §5.

База данных — единственная для приложения; в ней сосуществуют три схемы:
доменные и телеметрийные таблицы в `dbo`, таблицы ASP.NET Core Identity в `auth`,
рабочие таблицы Hangfire в `hangfire`. На один SQL-хост развёрнута одна
инсталляция панели (single-host, ADR-28).

---

## 1. Сущности

### 1.1 Доменное ядро (`MitLicenseCenter.Domain`)

Доменные сущности реализуют маркер `IEntity` (`Guid Id { get; }`). Все метки
времени хранятся в UTC; EF при чтении помечает любое `DateTime` как
`DateTimeKind.Utc`, поэтому JSON на проводе несёт суффикс `Z`.

**Tenant — клиент (арендатор).**

| Поле | Тип | Смысл |
|---|---|---|
| `Id` | `Guid` | Первичный ключ. |
| `Name` | `string` (обяз.) | Название клиента. Глобально уникально. |
| `MaxConcurrentLicenses` | `int` | Лимит одновременных лицензионных сеансов. `0` = без лимита. |
| `IsActive` | `bool` | Активен ли клиент. Enforcement применяется только к активным. |
| `CreatedAt` | `DateTime` | Момент создания. |
| `UpdatedAt` | `DateTime?` | Момент последнего изменения; `null` до первого изменения. |
| `RowVersion` | `byte[]?` | Токен оптимистической блокировки (SQL Server `rowversion`). |

**Инвариант (оптимистическая блокировка).** `Tenant`, `Infobase` и `Publication` несут
rowversion-токен: форма редактирования читает его и возвращает при сохранении. Конкурентный
апдейт с устаревшим токеном (запись изменена другим пользователем между чтением и записью)
отклоняется с **409** — потерянного обновления не происходит. Коды конфликта различают
сущность: `TENANT_CONCURRENCY_CONFLICT` (`PUT /tenants/{id}`), `INFOBASE_CONCURRENCY_CONFLICT`
(`PUT /infobases/{id}` — aggregate-апдейт корня и его публикации одним запросом),
`PUBLICATION_CONCURRENCY_CONFLICT` (самостоятельный `PUT /publications/{id}`). Токен опционален
в запросе: при его отсутствии апдейт выполняется без проверки версии (обратная совместимость).

`Publication` получает собственный токен (а не защищается только транзитивно через
`Infobase`), потому что у неё есть самостоятельный путь правки `PUT /publications/{id}`
(SiteName/VirtualPath/PlatformVersion/PhysicalPathOverride) помимо вложенного апдейта через
aggregate инфобазы. В `PUT /infobases/{id}` проверяются оба токена: корня (`RowVersion`) и
вложенной публикации (`Publication.RowVersion`).

**Infobase — информационная база.**

| Поле | Тип | Смысл |
|---|---|---|
| `Id` | `Guid` | Первичный ключ. |
| `TenantId` | `Guid` | Владелец-клиент. Смена владельца — только через `POST /infobases/{id}/reassign` (с проверкой коллизии имени и записью в аудит); обычный PUT/форма владельца не меняют. |
| `Name` | `string` (обяз.) | Имя базы в панели. Уникально в пределах клиента. |
| `ClusterInfobaseId` | `Guid` | Идентификатор базы в кластере 1С. Глобально уникален. |
| `DatabaseName` | `string` (обяз.) | Имя базы в SQL Server. SQL-инстанс задаётся одной настройкой `Sql.Server` (single-host), серверного поля у базы нет. |
| `Status` | `InfobaseStatus` | Операционный статус в панели. |
| `CreatedAt` / `UpdatedAt` | `DateTime` / `DateTime?` | Метки создания/изменения. |
| `RowVersion` | `byte[]?` | Токен оптимистической блокировки (SQL Server `rowversion`). |

**Publication — публикация инфобазы в IIS.** Одна инфобаза — одна публикация
(связь 1-к-1, обязательная).

| Поле | Тип | Смысл |
|---|---|---|
| `Id` | `Guid` | Первичный ключ. |
| `InfobaseId` | `Guid` | Инфобаза публикации (FK, уникален). |
| `SiteName` | `string` (обяз.) | Сайт IIS. |
| `VirtualPath` | `string` (обяз.) | Виртуальный путь приложения IIS. |
| `PlatformVersion` | `string` (обяз.) | Версия платформы 1С (`Мажор.Минор.Билд.Ревизия`). |
| `Source` | `PublicationSource` | Происхождение публикации. Дефолт `Unknown`. |
| `LastCheckStatus` | `PublicationPublishStatus` | Результат последней проверки факта в IIS. Read-only, дефолт `Unknown`. |
| `LastCheckAt` | `DateTime?` | Момент последней проверки. |
| `LastCheckDetails` | `string?` | Текстовая детализация проверки. |
| `PhysicalPathOverride` | `string?` | Переопределение физического пути приложения IIS; `null`/пусто → путь по convention. |
| `CreatedAt` / `UpdatedAt` | `DateTime` / `DateTime?` | Метки создания/изменения. |
| `RowVersion` | `byte[]?` | Токен оптимистической блокировки (SQL Server `rowversion`). |

**HiddenClusterInfobase — скрытая нераспределённая база кластера.** Игнор-лист
служебных баз кластера, которые оператор сознательно не заводит в панель и
скрывает из баннера-счётчика. Первичный ключ — сам `ClusterInfobaseId`
(суррогатного `Id` нет; сущность не реализует `IEntity`).

| Поле | Тип | Смысл |
|---|---|---|
| `ClusterInfobaseId` | `Guid` (PK) | База кластера, исключённая из баннера. |
| `Name` | `string` (обяз.) | Снимок имени на момент скрытия — блок «Скрытые» рендерится из БД даже при недоступном RAS. |
| `HiddenAtUtc` | `DateTime` | Момент скрытия. |
| `HiddenBy` | `string` (обяз.) | Логин оператора, скрывшего базу. |

**SettingEntry — runtime-параметр системы.** Одна строка `dbo.Settings`. Каталог
допустимых ключей фиксируется `SettingKey`; ключ вне каталога получает
`404 SETTING_UNKNOWN_KEY` на PUT. Имена ключей — wire-контракт (на них опираются
фронтенд и описания аудита).

| Поле | Тип | Смысл |
|---|---|---|
| `Key` | `string` (PK) | Имя параметра. |
| `ValueText` | `string?` | Открытое значение (для не-секретных). |
| `Value` | `byte[]?` | Зашифрованные UTF-8 байты (для секретных). |
| `IsSecret` | `bool` | Какой столбец актуален на запись. |
| `Description` | `string?` | Описание параметра. |
| `UpdatedAt` | `DateTime` | Момент изменения. |
| `UpdatedBy` | `string` | Кто изменил. |

Инвариант хранения: ровно один из `ValueText`/`Value` заполнен. Секретные
значения шифруются через ASP.NET Data Protection (purpose `mlc.settings.v1`); key
ring лежит в файловой системе без шифрования, защита — NTFS ACL (ADR-8).

### 1.2 Сущности-телеметрии (`MitLicenseCenter.Infrastructure.*`)

Это записи измерений и операций, а не доменные агрегаты, поэтому они живут в
Infrastructure (по прецеденту `AuditLog`) и читаются Web напрямую через
`AppDbContext`. Их конфигурации EF — inline в `OnModelCreating`.

**AuditLog — запись аудита.** Неизменяемый хронологический журнал значимых
операций. Записи только добавляются и удаляются ночной retention-джобой; правок нет.
Срок хранения задаётся настройкой `Audit.RetentionDays` (дефолт 365, диапазон [30, 3650]).
Запись `AuditLogsPurged` (тип 500) пишется только при непустой чистке (`totalDeleted > 0`);
в тихие дни без устаревших строк аудит-событие не создаётся.

| Поле | Тип | Смысл |
|---|---|---|
| `Id` | `Guid` | Первичный ключ. |
| `Timestamp` | `DateTime` | Момент события. DEFAULT на уровне БД — `SYSUTCDATETIME()`. |
| `ActionType` | `AuditActionType` | Тип действия. |
| `Reason` | `AuditReason?` | Причина (заполняется для kill-операций; иначе `null`). |
| `Initiator` | `string` (обяз.) | Инициатор (логин оператора, `System`, либо введённое имя при неудачном входе). |
| `Description` | `string` (обяз.) | Человекочитаемое описание. Пароли в описание не пишутся никогда. |
| `TenantId` | `Guid?` | Клиент, к которому относится действие; `null` для server-scope операций. |

**LicenseUsageSnapshot — снимок потребления лицензий.** Агрегат потребления
одного клиента за закрытый 15-минутный бакет (ADR-25). Сводный эндпоинт
(`GET /reports/license-usage`) агрегирует `ConsumedMax`, `ConsumedAvg` и `Limit`
суммированием по всем клиентам внутри каждого бакета — это сумма по тенантам,
а не глобальный пик платформы. Осиротевшие строки (`TenantId=null` после удаления
клиента, FK SetNull) включаются в сводку без фильтрации: история платформы не
«усыхает» при удалении клиента. В drill-down (`GET /reports/license-usage/{tenantId}`)
фильтруется по конкретному `TenantId ≠ null`, поэтому осиротевшие строки туда
не попадают.

| Поле | Тип | Смысл |
|---|---|---|
| `Id` | `Guid` | Первичный ключ. |
| `TenantId` | `Guid?` | Клиент; `null`, если клиент удалён (FK SetNull). |
| `BucketStartUtc` | `DateTime` | Начало 15-минутного бакета. |
| `ConsumedMin` / `ConsumedMax` | `int` | Минимум/максимум потребления за бакет. |
| `ConsumedAvg` | `double` | Среднее потребление за бакет. |
| `Limit` | `int` | Последний наблюдённый лимит в бакете. |

**PerfRecording — запись быстродействия по требованию (ADR-26).** Одно
расследование, охватывает весь хост (FK на клиента нет).

| Поле | Тип | Смысл |
|---|---|---|
| `Id` | `Guid` | Первичный ключ. |
| `StartedAtUtc` | `DateTime` | Старт записи. |
| `StoppedAtUtc` | `DateTime?` | Останов; `null` для активной. |
| `Status` | `PerfRecordingStatus` | Статус записи. |
| `StopReason` | `PerfRecordingStopReason?` | Причина останова; заполнена только для `Stopped`. |
| `StartedBy` | `string` | Логин оператора, запустившего запись. |
| `Samples` | коллекция | Навигация на сэмплы. |

**PerfRecordingSample — сэмпл записи быстродействия.** Снимок «сейчас» в момент
`SampleUtc`. Метрики хоста уровня 1 — плоскими колонками; атрибуция по семьям
процессов и точечные топ-виновники 1С/SQL — в JSON-колонках.

| Поле | Тип | Смысл |
|---|---|---|
| `Id` | `Guid` | Первичный ключ. |
| `RecordingId` | `Guid` | Родительская запись (FK, cascade). |
| `SampleUtc` | `DateTime` | Момент сэмпла. |
| `Measuring` | `bool` | `true`, если дельта-метрики ещё не готовы (первый сэмпл после старта пробы). |
| `CpuPercent`, `CpuQueueLength` | `double` | Загрузка и очередь CPU. |
| `MemoryAvailableMBytes`, `MemoryTotalMBytes`, `MemoryPagesPerSec` | `double` | Память хоста. |
| `DiskAvgReadSecPerOp`, `DiskAvgWriteSecPerOp`, `DiskQueueLength` | `double` | Латентность и очередь диска. |
| `ProcessesInaccessible` | `int` | Сколько процессов проба не смогла прочитать (атрибуция неполна). |
| `ProcessGroupsJson` | `string` (JSON) | Атрибуция по семьям процессов; дефолт `[]`. |
| `OneCLoadJson`, `SqlLoadJson` | `string?` (JSON) | Топ-виновники 1С/SQL; `null`, если источник не настроен/недоступен. |

**Investigation — «Дело» расследования производительности (ADR-57, трек 1.2).**
Аггрегат режима «Расследование»: окно времени поверх сбора и разбора
технологического журнала 1С (ТЖ). Сущность доменного ядра
(`MitLicenseCenter.Domain.TechLog`), но хранится телеметрийной таблицей в `dbo`
(конфиг inline в `AppDbContext`, как `PerfRecording`). FK на клиента нет — сбор
охватывает узел (`logcfg` действует на весь кластер), изоляция арендатора — фильтр
`p:processName` в снимке сбора, не FK. Заменил лёгкий прокси `TechLogCollection`
(MLC-230): его данные мигрированы в эту таблицу, старая удалена.

| Поле | Тип | Смысл |
|---|---|---|
| `Id` | `Guid` | Первичный ключ. |
| `Scenario` | `InvestigationScenario` | Сценарий сбора (набор событий `logcfg`); хранится `int`. |
| `Status` | `InvestigationStatus` | Статус дела (жизненный цикл — §3.6). |
| `StartedAtUtc` | `DateTime` | Старт сбора. |
| `StoppedAtUtc` | `DateTime?` | Снятие сбора; `null` для активного. |
| `StopReason` | `InvestigationStopReason?` | Причина остановки; заполнена только для `Completed`. |
| `StartedBy` | `string` | Логин оператора (Admin), запустившего дело. |
| `TenantId`, `InfobaseId` | `Guid?` | Опц. справочная привязка к арендатору/инфобазе; сама по себе **не** изолирует сбор. |
| `InfobaseProcessName` | `string?` | Имя ИБ (`p:processName`) для изоляции арендатора; `null` = весь кластер. |
| `CollectionDirectory` | `string` | Каталог сбора ТЖ (атрибут `location` в `logcfg`), под контролем панели. |
| `ConfigMarker` | `string` | Маркер-комментарий установленного `logcfg`; по нему сторож на старте отличает «наш» конфиг. |
| `RowVersion` | `byte[]?` | Optimistic-concurrency токен (как `Tenant`/`Infobase`); конкурентный targeted-UPDATE ловится 409. |
| `CollectionConfig` | owned 1:1 | Снимок включённого сбора (см. ниже); `null` для перенесённых исторических дел. |
| `Findings` | коллекция | Навигация на результаты анализа (дочерняя таблица, cascade). |

Инвариант изоляции арендатора (`EnsureProcessFilterInvariant`): если задан
`InfobaseId`, снимок сбора **обязан** нести непустой `ProcessNameFilter`
(`p:processName`) — иначе `logcfg` пишет ТЖ всех арендаторов (ADR-58 №2).

**CollectionConfig — снимок включённого сбора (ADR-58).** Неизменяемый снимок
момента установки: что именно собирали и что безопасно снять. Owned-entity
`Investigation` — колонки `Config_*` в той же таблице `dbo.Investigations` (1:1),
отдельной таблицы/ключа нет.

| Поле | Тип | Смысл |
|---|---|---|
| `LogcfgLocation` | `string` | Каталог сбора ТЖ (атрибут `location`). |
| `Events` | `string` (CSV) | Включённые события (напр. `TLOCK,TTIMEOUT,TDEADLOCK`). |
| `DurationThresholdMicros` | `long?` | Порог длительности в микросекундах; `null` = без порога. |
| `ProcessNameFilter` | `string?` | Значение `p:processName` (имя ИБ); обязателен при заданном `InfobaseId`. |
| `Format` | `string` | Целевой формат ТЖ (всегда `json` для 8.5). |
| `HistoryHours` | `int` | Лимит ротации (атрибут `history`), в часах. |

**Finding — результат анализатора ТЖ под «Дело» (ADR-57).** Один `Finding` на
результат анализатора: `Kind` различает, что разбирали, `ResultJson` несёт сам
версионированный результат (богатый DTO анализатора хранится JSON, как
`PerfRecordingSample.*Json` — не нормализованными таблицами). Дочерняя таблица
`Investigations`, каскадное удаление с делом.

| Поле | Тип | Смысл |
|---|---|---|
| `Id` | `Guid` | Первичный ключ. |
| `InvestigationId` | `Guid` | Родительское дело (FK, cascade). |
| `Kind` | `FindingKind` | Какой анализатор дал результат; различает форму `ResultJson`. |
| `SchemaVersion` | `int` | Версия формы payload'а `ResultJson` (форвард-совместимость без EF-миграции). |
| `ResultJson` | `string` (JSON) | Сериализованный результат анализатора (`nvarchar(max)`). |

**DatabaseBackup — учётная запись бэкапа (ADR-27).** Одна строка = один
запрошенный бэкап. Таблица одновременно служит очередью оркестратора: строки
`Queued` — очередь FIFO, `Running` — выполняющиеся. FK на инфобазу нет — запись
переживает её удаление.

| Поле | Тип | Смысл |
|---|---|---|
| `Id` | `Guid` | Первичный ключ. |
| `InfobaseId` | `Guid` | Инфобаза (простой `Guid`, без FK). |
| `DatabaseServer`, `DatabaseName` | `string` | Снимок server/db на момент запроса. |
| `Status` | `BackupStatus` | Статус бэкапа. |
| `RequestedBy` | `string` | Логин запросившего. |
| `RequestedAtUtc` | `DateTime` | Момент постановки в очередь. |
| `StartedAtUtc`, `CompletedAtUtc` | `DateTime?` | Старт и завершение. |
| `FilePath` | `string?` | Путь к готовому `.bak`. |
| `FileSizeBytes` | `long?` | Размер файла (может быть `null`, если msdb не вернул). |
| `FailureReason` | `BackupFailureReason` | Причина провала; `None` для не-Failed. |
| `ErrorMessage` | `string?` | Текст ошибки. |

### 1.3 Identity (`MitLicenseCenter.Infrastructure.Identity`)

Модель построена на ASP.NET Core Identity с ключами `Guid`
(`IdentityDbContext<AppUser, AppRole, Guid>`).

**AppUser : IdentityUser&lt;Guid&gt;** — учётная запись панели. Помимо
стандартных полей Identity (`Id`, `UserName`, `NormalizedUserName`, `Email`,
`PasswordHash`, `SecurityStamp`, `ConcurrencyStamp`, `LockoutEnd`,
`AccessFailedCount` и т. д.) добавлены:

| Поле | Тип | Смысл |
|---|---|---|
| `MustChangePassword` | `bool` | Форс-смена временного пароля при первом входе; снимается успешной сменой через `/auth/change-password`. |
| `LastLoginAt` | `DateTime?` | Момент последнего успешного входа; `null`, если ни разу не входил. |

**AppRole : IdentityRole&lt;Guid&gt;** — роль. Заведены ровно две роли:
`Admin` и `Viewer` (см. `Roles`).

---

## 2. Связи, поведение FK, индексы

Поведение при удалении и индексы заданы в `AppDbContext.OnModelCreating` и
подтверждаются снимком модели.

| Связь | Кратность | OnDelete | Обоснование |
|---|---|---|---|
| `Infobase.TenantId → Tenant.Id` | many-to-one | **Restrict** | Инфобаза — часть агрегата клиента; удаление клиента с непустым набором баз блокируется guard'ом в эндпоинте (409), FK — fallback. |
| `Publication.InfobaseId → Infobase.Id` | one-to-one (обяз.) | **Cascade** | Публикация — часть агрегата инфобазы; удаление инфобазы каскадом сносит публикацию. |
| `AuditLog.TenantId → Tenant.Id` | many-to-one (nullable) | **SetNull** | Удаление клиента обнуляет ссылку, но запись аудита остаётся — история сохраняется всегда. |
| `LicenseUsageSnapshot.TenantId → Tenant.Id` | many-to-one (nullable) | **SetNull** | Телеметрия переживает удаление клиента (как аудит). |
| `PerfRecordingSample.RecordingId → PerfRecording.Id` | many-to-one | **Cascade** | Удаление записи сносит её сэмплы. |
| `Finding.InvestigationId → Investigation.Id` | many-to-one | **Cascade** | Удаление «Дела» сносит его находки. |
| `Investigation.TenantId`/`InfobaseId` | — | **FK нет** | Простой `Guid?`; справочная привязка, сбор охватывает узел (изоляция — `p:processName`). |
| `DatabaseBackup.InfobaseId` | — | **FK нет** | Простой `Guid`; запись переживает удаление инфобазы. |
| `HiddenClusterInfobase` | — | **FK нет** | База кластера панели не принадлежит; снапшот решения оператора. |

**Backstop уникальности → 409.** Эндпоинты выполняют предварительную проверку
наличия (`AnyAsync`) на happy-path. Гонящиеся вставки, прошедшие эту проверку,
перехватываются на уровне `DbUpdateException`: если внутреннее исключение —
`SqlException` с кодом `2601` (duplicate key) или `2627` (unique constraint), то
нарушенный индекс идентифицируется по имени индекса в тексте сообщения
(стабильный идентификатор схемы, не локализованный текст). На основании имени
возвращается `409` с документированным `ProblemCodes.*` вместо `500`.

**Уникальные ограничения и ключевые индексы.**

| Таблица | Индекс | Уникальный | Назначение |
|---|---|---|---|
| `Tenants` | `IX_Tenants_Name` `(Name)` | да | Глобальная уникальность имени клиента. |
| `Infobases` | `IX_Infobases_TenantId_Name` `(TenantId, Name)` | да | Имя базы уникально в пределах клиента. |
| `Infobases` | `IX_Infobases_ClusterInfobaseId` `(ClusterInfobaseId)` | **да** | Одна база кластера принадлежит ровно одному клиенту. |
| `Publications` | `(InfobaseId)` | да | Следствие связи 1-к-1. |
| `AuditLogs` | `(Timestamp)` | нет | Сортировка журнала по времени. |
| `AuditLogs` | `(ActionType)` | нет | Фильтр по типу действия. |
| `AuditLogs` | `IX_AuditLogs_TenantId_Timestamp_Id` `(TenantId ASC, Timestamp DESC, Id DESC)` | нет | Дорога `/audit`: фильтр по клиенту + `ORDER BY Timestamp DESC, Id DESC`; покрывает FK-seek, убирает Sort. |
| `LicenseUsageSnapshots` | `(TenantId, BucketStartUtc)` | нет | Дорога чтения отчётов: фильтр по клиенту + диапазон бакетов. |
| `PerfRecordings` | `(StartedAtUtc)` | нет | Список расследований (свежие сверху). |
| `PerfRecordingSamples` | `(RecordingId, SampleUtc)` | нет | Ряд сэмплов записи по времени. |
| `Investigations` | `(StartedAtUtc)` | нет | Список дел (свежие сверху). |
| `Investigations` | `(Status)` | нет | Поиск активного дела сторожем (orphan-recovery). |
| `Findings` | `IX_Findings_InvestigationId` `(InvestigationId)` | нет | Чтение результатов дела по `InvestigationId`. |
| `DatabaseBackups` | `(RequestedAtUtc)` | нет | Список бэкапов (свежие сверху). |
| `DatabaseBackups` | `(DatabaseServer, DatabaseName, Status)` | нет | Дорога насоса: есть ли активный бэкап пары server+db; выборка самой старой `Queued`. |

---

## 3. Инварианты и жизненные циклы

### 3.1 Статус инфобазы (`InfobaseStatus`)

Операционный статус инфобазы в панели; не связан с состоянием базы в кластере 1С.
Значения и переходы задаются оператором при создании/редактировании базы.

```mermaid
stateDiagram-v2
    [*] --> Active
    Active --> Maintenance
    Maintenance --> Active
    Active --> Suspended
    Suspended --> Active
    Maintenance --> Suspended
    Suspended --> Maintenance
```

`Active` — штатная работа; `Maintenance` — обслуживание; `Suspended` —
приостановлена.

### 3.2 Статус и источник публикации

`PublicationPublishStatus` — результат последней проверки факта публикации в IIS.
Заполняется проверкой («Проверить сейчас») и фоновым обновлением каждые 5 минут.
Это read-only-статус, сравнения с эталоном и авто-исправления нет.

```mermaid
stateDiagram-v2
    [*] --> Unknown
    Unknown --> Published
    Unknown --> NotPublished
    Unknown --> Error
    Published --> NotPublished
    Published --> Error
    NotPublished --> Published
    NotPublished --> Error
    Error --> Published
    Error --> NotPublished
```

`Unknown` — проверка ещё не выполнялась; `Published` — сайт, виртуальный каталог
и `web.config` на месте; `NotPublished` — чего-то из этого физически нет;
`Error` — адаптер не смог прочитать состояние (нет прав / COM / IO).

`PublicationSource` — происхождение публикации: `Unknown` (дефолт для строк,
существовавших до введения поля), `Webinst` (создана/перезаписана панелью через
`webinst.exe`), `Configurator` (помечена как ручная). Перезаписать
`Configurator`-публикацию повторной webinst-публикацией можно только с явным
подтверждением; «свою» (`Webinst`) панель перезаписывает молча.

### 3.3 Бэкап базы (`BackupStatus`)

```mermaid
stateDiagram-v2
    [*] --> Queued
    Queued --> Running
    Running --> Succeeded
    Running --> Failed
    Succeeded --> [*]
    Failed --> [*]
```

`Queued` — поставлен в очередь (строка-очередь FIFO); `Running` — выполняется;
`Succeeded`/`Failed` — итог. Для пары (server, db) одновременно допустима только
одна активная (`Queued`/`Running`) строка — повторный запрос отвечает
`409 BACKUP_ACTIVE`. Причина провала — в `FailureReason` (см. §5).

### 3.4 Запись быстродействия (`PerfRecordingStatus`)

```mermaid
stateDiagram-v2
    [*] --> Active
    Active --> Stopped: оператор / авто-стоп
    Active --> Interrupted: рестарт процесса
    Stopped --> [*]
    Interrupted --> [*]
```

`Active` сэмплится фоновым таймером, пока оператор не остановит (`StopReason =
Manual`) или не сработает авто-стоп по времени (`TimeLimit`) либо по числу сэмплов
(`SampleLimit`) — что наступит раньше. На рестарте процесса осиротевшая активная
запись закрывается как `Interrupted` (`StopReason` остаётся `null`). У `Stopped`
причина останова заполнена всегда; у `Active` и `Interrupted` — `null`.

### 3.5 Правила валидации полей Infobase/Publication

Поля проверяются над trimmed-значением в порядке: обязательность → длина →
формат/символы; сообщается первое нарушение. Единый источник правил — общие
хелперы-предикаты (backend `InfobaseValidationRules`, frontend `validation.ts`);
оба слоя обновляются в одном PR (parity BE↔FE). Аннотации `[StringLength]` на
request-record'ах minimal API в рантайме не срабатывают, поэтому длину и опасные
символы проверяют именно эти хелперы.

| Поле | Max-длина | Запрещённые символы / правила |
|---|---:|---|
| `Infobase.Name` | 200 | Без управляющих символов и без `; = "` — имя уходит в `Ref=<name>` строки соединения webinst, метасимволы дали бы connstr-инъекцию. |
| `Infobase.DatabaseName` | 200 | Без управляющих символов, без последовательности `..` и без `\ / : * ? " < > | ; ' [ ]` — имя ложится в `Path.Combine` (подпапка бэкапа) и используется как SQL-идентификатор. |
| `Publication.SiteName` | 200 | Обязательно; длина. |
| `Publication.VirtualPath` | 200 | Начинается с `/`, без пробелов; дополнительно без управляющих символов, без `\` и без `..` (path-traversal). |
| `Publication.PlatformVersion` | 50 | Четыре числовых сегмента `Major.Minor.Build.Revision`; длина. |
| `Publication.PhysicalPathOverride` | 260 | Если задан — абсолютный путь (`Path.IsPathFullyQualified`); без управляющих символов, без `..` и без `; = "` (символы `\ / :` легитимны в абсолютном пути). |

Defense-in-depth: `WebinstArgs.BuildConnStr` отдельно отвергает имя инфобазы с
`; = "` (последний рубеж против connstr-инъекции; первичная защита — валидация
имени на входе).

### 3.6 «Дело» расследования (`InvestigationStatus` / `InvestigationStopReason`)

```mermaid
stateDiagram-v2
    [*] --> Collecting
    Collecting --> Analyzing: снятие сбора (оператор / авто-стоп)
    Collecting --> Interrupted: рестарт процесса / ОС
    Analyzing --> Completed: отчёт сформирован
    Analyzing --> Failed: ошибка разбора
    Completed --> [*]
    Interrupted --> [*]
    Failed --> [*]
```

`Collecting` — идёт сбор ТЖ (наш `logcfg.xml` установлен в `conf` платформы). Сбор
снимается оператором (`StopReason = Manual`) либо авто-стопом по политике
безопасности — окно времени (`TimeLimit`) или лимит места (`DiskLimit`) (ADR-58).
После снятия дело переходит в `Analyzing` (идёт разбор сырья ТЖ анализаторами в
`Finding`), затем в `Completed` (отчёт готов). Сбой сбора/разбора закрывает дело как
`Failed` (`StopReason = Error`). Осиротевшее активное дело после рестарта процесса/ОС
закрывается сторожем как `Interrupted` (`StopReason` остаётся `null` — это несёт сам
статус). `StopReason` заполнен только у `Completed`; у `Collecting`/`Analyzing`/
`Interrupted` — `null`. Активное дело (`Collecting`/`Analyzing`) удалить нельзя —
эндпоинт отвечает `409 INVESTIGATION_ACTIVE` (сначала остановить).

---

## 4. Правила лимитов лицензий

**Что такое лицензионный сеанс.** Потребление лицензии — **факт от 1С**: сеанс потребляет
лицензию, если кластер реально выдал ему клиентскую лицензию. Факт берётся из
`rac session list --licenses` (нелицензионный сеанс в вывод не попадает; **ADR-48**) и
фиксируется в поле `LicenseStatus` записи снимка (`SnapshotSessionEntry`): `Consuming` —
лицензию держит; `NotConsuming` — известен факту без лицензии; `Pending` — свежий сеанс,
ещё не классифицированный фактом (грейс держит его от завершения). `app-id` лицензию больше
не определяет (это лишь тип клиента; whitelist `OneC.LicenseConsumingAppIds` удалён).

**Подсчёт потребления.** Потребление клиента = число его сеансов со `LicenseStatus ==
Consuming`, сгруппированных по `TenantId` (`Pending`/`NotConsuming` не считаются). Клиенты
без потребляющих сеансов в расчёт не попадают. Если факт недоступен (`--licenses` не
отработал) — `SnapshotPayload.LicenseFactAvailable = false`, панель показывает «данные о
лицензиях недоступны», а enforcement приостановлен (не завершаем без подтверждённого факта).

**Семантика лимита.** `Tenant.MaxConcurrentLicenses` — потолок одновременных
лицензионных сеансов. Значение `0` означает отсутствие лимита: потребление
по-прежнему считается и пишется в историю, но enforcement не применяется.
Превышение фиксируется только при `limit > 0` и `consumed > limit`, и только для
активных клиентов (`IsActive = true`).

**Enforcement (контроль квот).** При превышении лимита фоновое задание завершает
избыточные сеансы по принципу «newest-first» — самый молодой потребляющий сеанс
убивается первым, до восстановления нормы. Перед завершением соблюдается отсрочка
`Enforcement.KillGraceSeconds` (дефолт 15 с): сеансы моложе порога не трогаются,
поскольку 1С проставляет `user-name` только после аутентификации; кандидаты
отсортированы newest-first, поэтому встретив недоросший до порога сеанс цикл
прекращает обход (а не пропускает). За один цикл завершается не более 20 сеансов
(`MaxKillsPerCycle`). Каждое завершение пишется в аудит как `SessionKilled` с
причиной `LimitExceeded`. Ручное завершение оператором — причина `ManualByAdmin`.

**Идемпотентный протокол kill сеанса.** Запись аудита `SessionKilled` создаётся
только при фактическом завершении (`Killed = true`) или если сеанс к моменту
вызова уже отсутствовал в кластере (`AlreadyGone = true` — идемпотентный успех).
Это правило действует одинаково для enforcement и для ручного завершения оператором:
если RAS недоступен и оба флага `false`, ручной kill возвращает
`502 CLUSTER_UNAVAILABLE` и аудит не пишется (ни одна из ветвей не создаёт
запись-ложь о несостоявшемся завершении).

**История потребления.** Singleton-аккумулятор копит running-агрегаты текущего
15-минутного бакета (min/max/sum/count + последний наблюдённый лимит) и на
границе бакета пишет строку `LicenseUsageSnapshot`. Бакет считается полом
`SampleUtc` по 15-минутной сетке; откат часов назад игнорируется; частичный бакет
при рестарте теряется (best-effort).

---

## 5. Замороженные целочисленные значения перечислений

Все перечисления ниже хранятся в БД как `int` (`HasConversion<int>`), а на проводе
сериализуются именем (`JsonStringEnumConverter`). **Правило заморозки: число
переиспользовать нельзя.** Новое действие/состояние получает новое число; удалённые
или revoked-механики сохраняют свои слоты, чтобы исторические записи рендерились
по имени. Это касается исторических `AuditActionType` 20 (PublicationCreated — с MLC-164 не
пишется при добавлении базы), 210/211 (publication drift) и 300/301 (circuit breaker) —
новые строки с этими значениями не пишутся, но слоты заняты навсегда.

### 5.1 `AuditActionType`

Пропуски в нумерации зарезервированы под будущие действия в той же группе.

| Значение | Имя | Группа / примечание |
|---:|---|---|
| 1 | TenantCreated | Клиенты |
| 2 | TenantUpdated | |
| 3 | TenantDeleted | |
| 10 | InfobaseCreated | Инфобазы |
| 11 | InfobaseUpdated | |
| 12 | InfobaseDeleted | |
| 13 | InfobaseReassigned | |
| 14 | UnassignedInfobaseHidden | Скрытие нераспределённой базы (server-scope) |
| 15 | UnassignedInfobaseUnhidden | Возврат скрытой базы |
| 20 | PublicationCreated | Публикации. **Исторический слот** (MLC-164: при добавлении базы не пишется — служебная запись-метаданные публикации не аудируется отдельно; реальная публикация — `PublicationPublished` 212) |
| 21 | PublicationUpdated | |
| 22 | PublicationDeleted | |
| 23 | PublicationUnpublished | Снятие IIS-публикации через webinst -delete |
| 100 | AdminLoggedIn | Сессии оператора |
| 101 | AdminLoggedOut | |
| 102 | AdminPasswordChanged | |
| 103 | UserCreated | Управление учётками (имена переименованы, числа заморожены) |
| 104 | UserDisabled | |
| 105 | UserPasswordReset | |
| 106 | UserEnabled | |
| 107 | UserRoleChanged | Смена роли Admin↔Viewer |
| 108 | LoginFailed | Неудачная попытка входа (server-scope) |
| 109 | UserDeleted | Жёсткое удаление учётки (MLC-180, server-scope; «Отключить» остаётся) |
| 200 | SessionKilled | Завершение сеанса |
| 201 | LimitChanged | Изменение лимита клиента |
| 210 | PublicationDriftDetected | **Исторический слот** (drift-enforcement удалён) |
| 211 | PublicationReconciled | **Исторический слот** |
| 212 | PublicationPublished | Публикация через webinst |
| 213 | PublicationPlatformChanged | Смена версии платформы |
| 220 | IisApplicationPoolRecycled | Управление IIS (server-scope) |
| 221 | IisApplicationPoolStarted | |
| 222 | IisApplicationPoolStopped | |
| 223 | IisSiteStarted | |
| 224 | IisSiteStopped | |
| 225 | IisSiteRestarted | |
| 226 | IisReset | |
| 227 | IisStopped | |
| 228 | IisStarted | |
| 300 | ClusterAdapterCircuitOpened | **Исторический слот** (circuit breaker удалён) |
| 301 | ClusterAdapterCircuitClosed | **Исторический слот** |
| 400 | SettingChanged | Изменение параметра |
| 500 | AuditLogsPurged | Системное обслуживание (очистка аудита) |
| 510 | BackupRequested | Бэкапы баз SQL |
| 511 | BackupSucceeded | |
| 512 | BackupFailed | |
| 513 | BackupDeleted | Ручное удаление администратором |
| 514 | BackupsPurged | Ночная TTL-очистка устаревших файлов |
| 600 | RasServiceRegistered | Управление службой RAS (ADR-47): регистрация (`sc create`) |
| 601 | RasServiceUpdated | Перенастройка под платформу/порт (`sc config`) |
| 602 | RasServiceStarted | Запуск остановленной службы |
| 700 | PerfRecordingStarted | «Быстродействие» (MLC-179): старт диагностической записи (host-уровень, `TenantId=null`) |
| 701 | PerfRecordingStopped | Ручной стоп активной записи |
| 702 | PerfRecordingDeleted | Удаление завершённой записи (+ каскад сэмплов) |
| 800 | OneCServerStarted | Управление сервером 1С (ADR-55, server-scope): верифицированный старт службы узла |
| 801 | OneCServerStopped | Верифицированный стоп службы узла |
| 802 | OneCServerRestarted | Верифицированный рестарт службы узла |
| 803 | OneCServerAutoRestarted | Срабатывание ночной джобы авто-рестарта (initiator `system`) |
| 804 | OneCServerAutoRestartScheduleChanged | Admin изменил расписание авто-рестартов |
| 805 | OneCProcessRestarted | Рестарт рабочего процесса `rphost` по `Pid` (ADR-56) |
| 806 | TechLogCollectionStarted | «Расследование» (ADR-57/58): старт сбора ТЖ (установлен целевой `logcfg.xml`) |
| 807 | TechLogCollectionStopped | Снятие сбора (исходный `logcfg` восстановлен; причина — в описании) |
| 808 | TechLogConfigForceRestored | Сторож на старте снял «забытый» `logcfg` панели без активного дела |
| 809 | InvestigationDeleted | Удаление завершённого «Дела» (+ каскад находок; зеркаль `PerfRecordingDeleted` 702) |

### 5.2 `AuditReason`

| Значение | Имя | Смысл |
|---:|---|---|
| 1 | LimitExceeded | Сеанс завершён из-за превышения лимита (enforcement). |
| 2 | ManualByAdmin | Сеанс завершён вручную оператором. |

### 5.3 `InfobaseStatus`

| Значение | Имя |
|---:|---|
| 0 | Active |
| 1 | Maintenance |
| 2 | Suspended |

### 5.4 `PublicationPublishStatus`

| Значение | Имя |
|---:|---|
| 0 | Unknown |
| 1 | Published |
| 2 | NotPublished |
| 3 | Error |

### 5.5 `PublicationSource`

| Значение | Имя |
|---:|---|
| 0 | Unknown |
| 1 | Webinst |
| 2 | Configurator |

### 5.6 `BackupStatus`

| Значение | Имя |
|---:|---|
| 0 | Queued |
| 1 | Running |
| 2 | Succeeded |
| 3 | Failed |

### 5.7 `BackupFailureReason`

| Значение | Имя | Смысл |
|---:|---|---|
| 0 | None | Успех / ещё не завершён. |
| 1 | InsufficientSpace | На диске нет места под оценку + запас. |
| 2 | EstimateUnavailable | Не удалось получить оценку размера. |
| 3 | PermissionDenied | Учётка панели не sysadmin. |
| 4 | BackupFailed | Сам BACKUP/VERIFY или инфраструктура упали. |
| 5 | Interrupted | Рестарт панели оборвал выполнение (файл может быть неполным). |

### 5.8 `PerfRecordingStatus` и `PerfRecordingStopReason`

`PerfRecordingStatus` (значения по порядку объявления): `Active` = 0,
`Stopped` = 1, `Interrupted` = 2.

`PerfRecordingStopReason`: `Manual` = 0, `TimeLimit` = 1, `SampleLimit` = 2.

### 5.9 «Дело» расследования — `InvestigationStatus`, `InvestigationStopReason`, `FindingKind`, `InvestigationScenario`

`InvestigationStatus`: `Collecting` = 0, `Analyzing` = 1, `Completed` = 2,
`Interrupted` = 3, `Failed` = 4.

`InvestigationStopReason`: `Manual` = 0, `TimeLimit` = 1, `DiskLimit` = 2,
`Error` = 3.

`FindingKind`: `ManagedLocks` = 0 (управляемые блокировки 1С — TLOCK/TTIMEOUT/
TDEADLOCK), `SlowQueries` = 1 (долгие запросы к СУБД — DBMSSQL), `Exceptions` = 2
(исключения платформы — EXCP), `DbmsLocks` = 3 (СУБД-блокировки — DBMSSQL c полями
`lkX`), `Call` = 4 (серверные вызовы 1С — CALL).

`InvestigationScenario` (int 1:1 с `Application.TechLogScenario`): `Locks` = 0,
`SlowQueries` = 1, `Exceptions` = 2, `GeneralSlow` = 3, `DbmsLocks` = 4.

---

## 6. Схема БД «как построено»

База развёрнута 23 EF-миграциями (от `20260518010940_InitialCreate` до
`20260621014956_MLC237InvestigationModel`). Состояние схемы соответствует
`AppDbContextModelSnapshot.cs`.

### 6.1 Схема `dbo` — доменные и телеметрийные таблицы

| Таблица | Назначение | Заметки по столбцам |
|---|---|---|
| `Tenants` | Клиенты | `Name nvarchar(200)`; `RowVersion rowversion` (токен оптимистической блокировки). |
| `Infobases` | Инфобазы | `Name nvarchar(200)`, `DatabaseName nvarchar(200)`; `Status int`; `RowVersion rowversion` (токен оптимистической блокировки). |
| `Publications` | Публикации IIS | `SiteName`/`VirtualPath nvarchar(200)`, `PlatformVersion nvarchar(50)`, `PhysicalPathOverride nvarchar(260)` (MAX_PATH); `Source`/`LastCheckStatus int`; `LastCheckDetails nvarchar(max)`; `RowVersion rowversion` (токен оптимистической блокировки). |
| `AuditLogs` | Журнал аудита | `Initiator nvarchar(256)`, `Description nvarchar(max)`; `ActionType int`, `Reason int?`; `Timestamp` DEFAULT `SYSUTCDATETIME()`. |
| `LicenseUsageSnapshots` | История потребления | `ConsumedMin/Max int`, `ConsumedAvg float`, `Limit int`. |
| `PerfRecordings` | Записи быстродействия | `StartedBy nvarchar(256)`; `Status int`, `StopReason int?`. |
| `PerfRecordingSamples` | Сэмплы записей | host-метрики `float`; `ProcessGroupsJson`/`OneCLoadJson`/`SqlLoadJson nvarchar(max)`. |
| `Investigations` | «Дела» расследования (трек 1.2) | `Scenario`/`Status int`, `StopReason int?`; `StartedBy nvarchar(256)`, `InfobaseProcessName nvarchar(200)`, `CollectionDirectory nvarchar(512)`, `ConfigMarker nvarchar(256)`; `TenantId`/`InfobaseId uniqueidentifier?` (без FK); `RowVersion rowversion`; снимок сбора — owned-колонки `Config_*` (`Config_Events nvarchar(1024)`, `Config_DurationThresholdMicros bigint?`, `Config_ProcessNameFilter nvarchar(200)`, …). |
| `Findings` | Результаты анализаторов под «Дело» | `InvestigationId uniqueidentifier` (FK→`Investigations`, Cascade); `Kind int`, `SchemaVersion int`; `ResultJson nvarchar(max)`. |
| `DatabaseBackups` | Учёт/очередь бэкапов | `DatabaseServer`/`DatabaseName nvarchar(200)`, `RequestedBy nvarchar(256)`, `FilePath nvarchar(512)`, `FileSizeBytes bigint?`; `Status`/`FailureReason int`. |
| `HiddenClusterInfobases` | Игнор-лист баз кластера | PK `ClusterInfobaseId`; `Name nvarchar(200)`, `HiddenBy nvarchar(256)`. |
| `Settings` | Runtime-параметры | PK `Key nvarchar(200)`; `ValueText nvarchar(max)`, `Value varbinary(max)`, `Description nvarchar(500)`, `UpdatedBy nvarchar(256)`. |

Хранение параметра (`Settings`): открытое значение — в `ValueText`
(`nvarchar(max)`); секрет — в `Value` (`varbinary(max)`, зашифрованные UTF-8
байты). Флаг `IsSecret` указывает актуальный столбец; заполнен всегда ровно один.

### 6.2 Схема `auth` — таблицы ASP.NET Core Identity

Таблицы Identity переименованы и вынесены в схему `auth`: `Users`, `Roles`,
`UserRoles`, `UserClaims`, `UserLogins`, `UserTokens`, `RoleClaims`. Ключи —
`Guid`. Нормализованные имена и e-mail — `nvarchar(256)`; ключи логинов/токенов —
`nvarchar(450)`; полезные нагрузки (значения claim'ов, токенов) — `nvarchar(max)`.
Кастомные поля `AppUser`: `MustChangePassword bit`, `LastLoginAt datetime2 NULL`.

### 6.3 Схема `hangfire` — рабочие таблицы Hangfire

Hangfire размещает свои рабочие таблицы (очереди, состояния задач, серверы,
наборы, счётчики) в отдельной схеме `hangfire` на том же SQL-инстансе; схема и
строка подключения задаются при старте приложения. Эти таблицы создаёт и
обслуживает сам Hangfire — они не входят в EF-модель и не покрываются миграциями
приложения. Содержимое — recurring-задания панели и история их выполнения.

---

## 7. ER-диаграмма

Диаграмма охватывает доменное ядро и связанные телеметрийные таблицы.
`LicenseUsageSnapshot` и `AuditLog` ссылаются на клиента «мягко» (FK с SetNull,
nullable). `DatabaseBackup`, `HiddenClusterInfobase` и `Investigation` ссылок-FK
на клиента/инфобазу не имеют (привязка — простой `Guid?`).

```mermaid
erDiagram
    TENANT ||--o{ INFOBASE : "владеет (Restrict)"
    INFOBASE ||--|| PUBLICATION : "публикуется (Cascade)"
    TENANT |o..o{ AUDITLOG : "SetNull / nullable"
    TENANT |o..o{ LICENSEUSAGESNAPSHOT : "SetNull / nullable"
    PERFRECORDING ||--o{ PERFRECORDINGSAMPLE : "сэмплы (Cascade)"
    INVESTIGATION ||--o{ FINDING : "находки (Cascade)"

    TENANT {
        Guid Id PK
        string Name UK
        int MaxConcurrentLicenses
        bool IsActive
    }
    INFOBASE {
        Guid Id PK
        Guid TenantId FK
        string Name
        Guid ClusterInfobaseId UK
        string DatabaseName
        int Status
    }
    PUBLICATION {
        Guid Id PK
        Guid InfobaseId FK,UK
        string SiteName
        string VirtualPath
        string PlatformVersion
        int Source
        int LastCheckStatus
    }
    AUDITLOG {
        Guid Id PK
        DateTime Timestamp
        int ActionType
        int Reason
        Guid TenantId FK
    }
    LICENSEUSAGESNAPSHOT {
        Guid Id PK
        Guid TenantId FK
        DateTime BucketStartUtc
        int ConsumedMax
        int Limit
    }
    PERFRECORDING {
        Guid Id PK
        DateTime StartedAtUtc
        int Status
    }
    PERFRECORDINGSAMPLE {
        Guid Id PK
        Guid RecordingId FK
        DateTime SampleUtc
    }
    INVESTIGATION {
        Guid Id PK
        int Scenario
        int Status
        DateTime StartedAtUtc
        Guid TenantId
        Guid InfobaseId
    }
    FINDING {
        Guid Id PK
        Guid InvestigationId FK
        int Kind
        int SchemaVersion
    }
    DATABASEBACKUP {
        Guid Id PK
        Guid InfobaseId
        int Status
        int FailureReason
    }
    HIDDENCLUSTERINFOBASE {
        Guid ClusterInfobaseId PK
        string Name
        DateTime HiddenAtUtc
    }
    SETTINGENTRY {
        string Key PK
        string ValueText
        bytes Value
        bool IsSecret
    }
```

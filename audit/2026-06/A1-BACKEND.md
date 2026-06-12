# A1 — BACKEND: архитектура, EF, эндпоинты, джобы, адаптеры, аудит-лог, тесты

Этап А1 пред-релизного аудита. Дата: **2026-06-12**, репозиторий `F:\dev\MitLicense Center`,
ветка `main`, HEAD `e6b1317`. Входная фактура — `audit/2026-06/A0-BASELINE.md` (71 эндпоинт,
5 recurring-джобов, 19 миграций, 636 зелёных BE-тестов). Источник истины — код; docs/ и
CLAUDE.md рассматривались как проверяемые утверждения.

**Методика:** 7 субагентов по зонам (слои/NetArchTest, EF Core, Web-эндпоинты, Hangfire,
адаптеры 1С/IIS, аудит-лог, качество тестов; opus — архитектура/конкурентность/адаптеры,
sonnet — стандартный разбор). Каждый кандидат в Blocker/High прошёл отдельного
субагента-скептика (opus, установка «опровергни»). Итог скептиков: **ни один High не
подтвердился в заявленной severity** — два понижены до Medium, один до Low, один finding
(«500 на GET /audit при неизвестном enum-int») **опровергнут эмпирическим прогоном** на
net10.0 (`JsonStringEnumConverter` сериализует неизвестное значение числом, исключения нет).

**Итог: Blocker — 0 · High — 0 · Medium — 12 · Low — 14.**

---

## 1. Findings — Medium

### [BE-01] Аудит-запись неатомарна с действием; для DELETE tenant аудит коммитится ДО удаления
- **Severity:** Medium · **Confidence:** High (проверено скептиком: подтверждён частично, High снят)
- **Evidence:** `Infrastructure/Audit/AuditLogger.cs:29-41` (собственный `SaveChangesAsync` в `LogAsync`); `Web/Endpoints/Tenants/TenantsEndpoints.cs:191-196` (порядок: `audit.LogAsync` → коммит аудита → `Remove` → коммит удаления); `Web/Endpoints/Infobases/InfobasesEndpoints.cs:252-266` (мутация → два отдельных аудит-коммита); `Web/Endpoints/Publications/PublicationsEndpoints.cs:161-179`; DI — `Infrastructure/DependencyInjection.cs:47,97` (оба scoped, один DbContext). Явных транзакций вокруг мутация+аудит нет нигде в Web (grep `BeginTransaction` — только retention-джобы).
- **Суть:** каждый `LogAsync` — отдельный коммит. Для create/publish направление безопаснее (сбой → действие есть, аудита нет — undercount). Для DELETE tenant порядок обратный (намеренно, ради FK SetNull — комментарий :189-190): сбой `SaveChanges` удаления оставит **ложную запись `TenantDeleted`** о неслучившемся событии (overcount). Дополнительно `BackupOrchestrator.CompleteAsync` (`Infrastructure/Backups/BackupOrchestrator.cs:291-319`) глотает сбой аудита best-effort — `BackupSucceeded/Failed` может не записаться.
- **Риск:** расхождение immutable-аудита с реальностью; ложная запись об удалении — худший вариант для доверия журналу. Окно узкое (сбой/деплой ровно между коммитами), данные бизнес-сущностей не страдают.
- **Рекомендация:** буферизовать аудит в ChangeTracker без собственного `SaveChanges` и коммитить одним `SaveChanges` с мутацией (один scoped DbContext это позволяет); для DELETE tenant — фиксировать аудит после успешного удаления либо в одной транзакции через `CreateExecutionStrategy().ExecuteAsync`.

### [BE-02] Optimistic concurrency отсутствует — lost update на лимитах лицензий
- **Severity:** Medium · **Confidence:** High (факт), Med (вероятность сценария)
- **Evidence:** `Infrastructure/Persistence/AppDbContext.cs` — ни одного `IsRowVersion`/`IsConcurrencyToken` на доменных сущностях (все 40 хитов — Identity `ConcurrencyStamp`); `TenantsEndpoints.cs:130-167` (read-modify-write без токена).
- **Суть:** два админа одновременно правят одного тенанта — last-write-wins без обнаружения; критично именно для `MaxConcurrentLicenses`: enforcement начнёт убивать сессии по «случайно победившему» лимиту. Гонки джоб↔лимит нет (ReconciliationJob пишет только в insert-only таблицы).
- **Риск:** недетерминированный действующий лимит при конкурентной правке; редкий, но «дорогой» сценарий.
- **Рекомендация:** `rowversion`-токен хотя бы на `Tenant`, маппинг `DbUpdateConcurrencyException` → 409.

### [BE-03] `MaxConcurrentLicenses` без runtime-валидации диапазона: отрицательный лимит тихо отключает enforcement
- **Severity:** Medium · **Confidence:** High
- **Evidence:** `Web/Endpoints/Tenants/TenantsContracts.cs:23` (`[Range(0,100_000)]` — только DataAnnotations, в minimal API не исполняется); `TenantsEndpoints.cs:82-98,136-150` (значение пишется без проверки); `Infrastructure/Jobs/ReconciliationJob.cs:124` (`<= 0` → тенант пропускается enforcement'ом); `Web/Endpoints/Dashboard/DashboardEndpoints.cs:71` (`Sum` по лимитам — `OverflowException` при значениях около `int.MaxValue`).
- **Суть:** `-1` или `int.MaxValue` сохраняются в БД без ошибки (SQL-тип int). Отрицательный лимит = тенант молча выпадает из квотирования; гигантские значения ломают сумму на dashboard (500).
- **Риск:** ядро продукта — контроль квот — отключается опечаткой оператора без какой-либо индикации.
- **Рекомендация:** ручная проверка `0 ≤ value ≤ 100_000` в Create/Update → 400 ValidationProblem.

### [BE-04] Max-длины строк ловятся только nvarchar БД → 500 вместо 400 на 11 мутационных эндпоинтах
- **Severity:** Medium · **Confidence:** High
- **Evidence:** `TenantsEndpoints.cs:201-209` (`ValidateName` — только IsNullOrWhiteSpace, длины нет); `InfobasesEndpoints.cs:453-472` (то же для Name/DatabaseName); `Web/Endpoints/Shared/InfobaseValidationRules.cs:31-76` (`AppendPublicationFieldErrors` НЕ проверяет длины, хотя константы `SiteNameMaxLength`/`VirtualPathMaxLength`/`PlatformVersionMaxLength`/`PhysicalPathMaxLength` объявлены в :14-19); `UsersEndpoints.cs:67-78` (UserName >256 → Identity-ошибка → необработанный путь → 500). Контраст: hide-эндпоинт длину проверяет (`UnassignedInfobasesEndpoints`).
- **Суть:** строка длиннее nvarchar(N) проходит ручную валидацию и падает `DbUpdateException` → глобальный handler → 500 ProblemDetails (без утечки стека — это подтверждено).
- **Риск:** оператор получает «внутреннюю ошибку» вместо понятной валидации; известная гоча CLAUDE.md, but код её не закрывает.
- **Рекомендация:** добавить проверки длин в `ValidateName`/`ValidateInfobase`/`AppendPublicationFieldErrors` из уже объявленных констант (синхронно с FE `validation.ts`); в Users — явная проверка 256.

### [BE-05] PublicationStatusRefreshJob: одно исключение обрывает обновление всех остальных публикаций
- **Severity:** Medium · **Confidence:** High
- **Evidence:** `Infrastructure/Jobs/PublicationStatusRefreshJob.cs:54-73` (один внешний try/catch на весь цикл, per-item обработки нет); :84-97 + :116-125 (attach транзиента + UPDATE по Id → удалённая параллельно публикация даёт `DbUpdateConcurrencyException`, которое валит остаток цикла).
- **Суть:** сбой IIS-чтения/записи на i-й публикации (или её конкурентное удаление админом) прекращает обработку i+1..N; джоб при этом завершается «успешно» — деградация тихая.
- **Риск:** оператор видит устаревшие статусы публикаций после первой сбойной; маскируется под «всё проверено» до следующего тика, а при стабильно сбойной публикации — постоянно.
- **Рекомендация:** per-item try/catch (сбойную помечать Error-статусом, `DbUpdateConcurrencyException` трактовать как «публикация исчезла, пропустить») и продолжать цикл.

### [BE-06] Enforcement-цикл может превысить 180с distributed-lock → параллельный enforce и over-kill
- **Severity:** Medium · **Confidence:** Med (требуется системно медленный кластер)
- **Evidence:** `Infrastructure/Clusters/RacExecutableRasClusterClient.cs:27` (`InvocationTimeout = 30s` на каждый спавн rac); `Infrastructure/Jobs/KillEnforcer.cs:17` (`MaxKillsPerCycle = 20`), :76 (re-fetch), :135 (per-kill спавн); `Application/Jobs/IReconciliationJob.cs:21` (`[DisableConcurrentExecution(180)]`; комментарий :18-20 утверждает «с запасом», арифметика этого не подтверждает: worst-case 2×30 + 20×30 = 660с).
- **Суть:** при тормозящем кластере (kill-вызовы упираются в таймауты) cold-цикл живёт дольше 180с; lock истекает, следующий минутный тик входит в `EnforceAsync` параллельно — каждый независимо считает превышение и убивает newest-first.
- **Риск:** суммарный over-kill пользовательских 1С-сеансов — ровно то, от чего `DisableConcurrentExecution` должен защищать; проявляется в наихудший момент (кластер уже деградирует).
- **Рекомендация:** поднять lock-таймаут до покрытия worst-case (≈700с) либо ограничить суммарное время цикла `CancellationTokenSource.CancelAfter`, согласованным с lock.

### [BE-07] Имя инфобазы подставляется в connection string webinst без валидации символов
- **Severity:** Medium · **Confidence:** High
- **Evidence:** `Infrastructure/Publishing/WebinstArgs.cs:37-38` (`Srvr={clusterServer};Ref={infobaseName};`); `Web/Endpoints/Shared/InfobaseValidationRules.cs:13-14` (для Name — только max-длина 200, ограничений символов нет).
- **Суть:** `;`/`=` в имени расщепляют connstr 1С на дополнительные параметры (`Ref=foo;Usr=x;Pwd=y`). Это не shell-инъекция (аргумент идёт одним элементом `ArgumentList`), а инъекция уровня connstr-парсера 1С.
- **Риск:** публикация с «хитрым» именем получает неожиданную строку соединения (в худшем случае — чужая ИБ). Эксплуатируемость низкая (имя заводит Admin, обычно из discovery), но это defense-in-depth дыра.
- **Рекомендация:** запретить `;` `=` `"` в `Infobase.Name` (BE+FE синхронно) либо экранировать в `BuildConnStr` по правилам 1С.

### [BE-08] NetArchTest-банлист anti-corruption границы дыряв: ловит не все обходы
- **Severity:** Medium · **Confidence:** High
- **Evidence:** `tests/MitLicenseCenter.Tests.Unit/Architecture/LayerBoundaryTests.cs:76-83` — банлист правила 3: `Infrastructure.{Clusters,Publishing,Discovery,Jobs,Performance,Backups}` + `Microsoft.Web.Administration` + тип `System.Diagnostics.Process`. Вне списка: `Infrastructure.Diagnostics` (RacMetrics и пр.), `Infrastructure.Reporting` (сервис `LicenseUsageAccumulator` в одном namespace с EF-сущностями, которые Web легально использует — `BackupsEndpoints.cs:13`, `PerformanceEndpoints.cs:11`), `Microsoft.Data.SqlClient` (уже протёк: `Web/Endpoints/Shared/DbUniqueViolation.cs:1,63-66` — разбор `SqlException.Number`), `System.Management` (WMI, транзитивно доступен). Правило 4 («Web не обходит DI») сознательно не реализовано в расчёте на полноту правила 3 (:28-31).
- **Суть:** сама граница ПО КОДУ сейчас соблюдена (прямых rac/ServerManager/Process/SqlConnection в Web нет — проверено), но CI-страж пропустит инжект `RacMetrics`/`LicenseUsageAccumulator` или `new SqlConnection` в эндпоинт. Смешение сущностей и сервисов в `Infrastructure.Reporting` делает «бан namespace целиком» невыразимым.
- **Риск:** граница держится ревью, а не тестами; ложное чувство покрытия.
- **Рекомендация:** инвертировать правило 3 в whitelist разрешённых Infrastructure-namespace'ов; разнести Reporting на Entities/сервисы; добавить в бан `Infrastructure.Diagnostics`, `Microsoft.Data.SqlClient`, `System.Management`; SqlException-классификацию увести за абстракцию или зафиксировать как исключение в ADR.

### [BE-09] Валидационный барьер `AppendPublicationFieldErrors` без поведенческих тестов; `PublicationsEndpoints.UpdateAsync` не вызывается ни одним тестом
- **Severity:** Medium · **Confidence:** High (проверено скептиком: подтверждён, High снят — пробел UX-валидации, не целостности)
- **Evidence:** `Web/Endpoints/Shared/InfobaseValidationRules.cs:31-76`; call-sites: `PublicationsEndpoints.cs:74`, `InfobasesEndpoints.cs:480,488`. Тесты: `Endpoints/InfobasesValidationTests.cs` — только `Validator.TryValidateObject` (DataAnnotations, в runtime мёртвые) + parity-константы/regex; `PublicationsOperationsTests.cs` покрывает только Check/Publish/ChangePlatform; все вызовы Create/Update в тестах подают исключительно валидную публикацию (grep по тестам: единственные значения — `"Default Web Site"`, `/ib`, `8.3.23.1865`).
- **Суть:** ветки SiteName-пустой, VirtualPath-без-слеша/с-пробелом, относительный PhysicalPathOverride и сам факт срабатывания барьера в хендлерах не покрыты вообще; запинен golden-table только regex версии платформы. Регрессия (потеря вызова хелпера, инверсия `StartsWith('/')`) пройдёт 636 зелёных тестов незамеченной.
- **Риск:** единственный runtime-барьер валидации публикационных полей не защищён от регрессий; PUT /publications/{id} — целиком слепая зона.
- **Рекомендация:** `[Theory]` с негативными кейсами прямого вызова хелпера + хендлер-тесты UpdateAsync обоих эндпоинтов.

### [BE-10] Неудачные попытки входа не аудируются
- **Severity:** Medium · **Confidence:** High
- **Evidence:** `Web/Endpoints/Auth/AuthEndpoints.cs:47-57` — `PasswordSignInAsync` при неудаче → 401 без `audit.LogAsync` (lockout Identity работает, но в аудит-журнал не попадает). Отказы 403 также не аудируются (`Program.cs:93`).
- **Суть:** журнал аудита показывает «всё чисто» при активном переборе паролей.
- **Риск:** brute-force на admin-панель невидим оператору штатными средствами продукта.
- **Рекомендация:** `AuditActionType.LoginFailed = 108` (следующее свободное число) при `!result.Succeeded`, без записи пароля.

### [BE-11] `LimitChanged = 201` объявлен и мёртв: смена квоты не имеет структурированного аудит-события
- **Severity:** Medium · **Confidence:** High
- **Evidence:** `Domain/Audit/AuditActionType.cs:45` — единственное вхождение `LimitChanged` во всём `backend/src`; `TenantsEndpoints.UpdateAsync` пишет только общий `TenantUpdated`.
- **Суть:** смена `MaxConcurrentLicenses` — чувствительная операция (прямое влияние на enforcement) — неотличима в аудите от переименования тенанта; фильтр по `LimitChanged` всегда пуст.
- **Риск:** разбор инцидента «почему стали убиваться сессии» не находит, кто и когда сменил лимит.
- **Рекомендация:** писать `LimitChanged` при фактическом изменении лимита (со старым/новым значением) либо удалить член с комментарием «зарезервировано, не переиспользовать».

### [BE-12] Слой CP866/OEM-декодирования rac.exe вне тест-контура; от него зависит идемпотентность kill
- **Severity:** Medium · **Confidence:** High
- **Evidence:** `Infrastructure/Clusters/SystemProcessRacRunner.cs:103-121,137-154` (`ResolveOemEncoding` + декод сырых байтов); все `RacExecutableRasClusterClientTests` подают fake-runner'у уже готовые строки — путь bytes→string не тестируется (единственное упоминание CP866 в тестах — комментарий в smoke); маркер идемпотентности «Сеанс … не найден» матчится как русский литерал — `RacExecutableRasClusterClient.cs:23,124`. Усугубляется [BE-22]-смежным фактом: заголовочный комментарий runner'а (:9-13) ложно утверждает «UTF-8» — приглашение «починить» и сломать.
- **Суть:** регрессия декодирования (или правка по стейл-комментарию) превратит кириллицу в mojibake, маркер перестанет матчиться, kill уже-завершённого сеанса станет трактоваться как ошибка — тесты этого не заметят.
- **Риск:** тихая регрессия enforcement-семантики в самом хрупком месте интеграции с 1С.
- **Рекомендация:** юнит-тест на байтах CP866 через реальный runner-декод; исправить заголовочный комментарий (см. [BE-15]).

---

## 2. Findings — Low

### [BE-13] Незамороженные int-значения `PerfRecordingStatus`/`PerfRecordingStopReason` при `HasConversion<int>`
- **Severity:** Low (понижено скептиком с High) · **Confidence:** High
- **Evidence:** `Application/Performance/PerfRecordingModels.cs:8-23` (нет `= N`); `AppDbContext.cs:168-169` (`HasConversion<int>`/`<int?>`); freeze-тестов нет (grep по tests). Данные долгоживущие (retention-джоба для PerfRecordings нет — `SettingKey.cs:70`, удаление только ручное).
- **Суть/Риск:** вставка члена в середину молча перепишет смысл исторических строк. Понижено: enum'ы узкие и замкнутые, расширение в середину маловероятно, на провод идёт строка, конвенция в комментарии задокументирована — отсутствует лишь машинная проверка.
- **Рекомендация:** проставить `= 0/1/2` + freeze-тест по образцу Audit.

### [BE-14] Freeze-тест `AuditActionType` покрывает 11 из 43 членов
- **Severity:** Low · **Confidence:** High
- **Evidence:** `tests/.../Audit/AuditLogEnumMappingTests.cs:43-58` — InlineData только Tenant*, AdminLogged*, Backup*; не закреплены Infobase (10-15), Publication (20-22, 210-213), User (103-107), Session (200-201), IIS (220-228), Circuit (300-301), Setting (400), Purge (500). Сами значения в коде дубликатов не имеют, все явные; git-история переиспользования чисел не выявила (MLC-060 — переименование без смены чисел).
- **Суть/Риск:** случайная смена незакреплённого числа не будет поймана.
- **Рекомендация:** дополнить InlineData всеми членами; freeze-тесты на `PublicationPublishStatus`/`PublicationSource`.

### [BE-15] Стейл-комментарий: заголовок `SystemProcessRacRunner` утверждает «UTF-8», код декодирует OEM/CP866
- **Severity:** Low · **Confidence:** High
- **Evidence:** `SystemProcessRacRunner.cs:9-13` (комментарий, ссылается на ADR-3.3) vs :24,103-104 (OemEncoding). Поведение кода правильное, ошибочен комментарий; ADR-3.3 стоит сверить.
- **Риск:** приманка для «исправления» на UTF-8 → mojibake → регрессия kill-идемпотентности (см. [BE-12]).
- **Рекомендация:** привести комментарий в соответствие.

### [BE-16] OEM-декод по культуре хоста: на EN Windows (CP437) русский маркер «сеанс не найден» не сматчится
- **Severity:** Low (Medium при установке на EN-сервер) · **Confidence:** Med
- **Evidence:** `SystemProcessRacRunner.cs:137-154` (OEM текущей культуры); `RacExecutableRasClusterClient.cs:23,124` (русский литерал, Ordinal).
- **Риск:** вне RU-локали kill уже-завершённого сеанса трактуется как ошибка, а не идемпотентный no-op. Для текущего RU-стенда неактуально.
- **Рекомендация:** задокументировать «RU Windows» как требование либо перейти на языконезависимый признак.

### [BE-17] Списки без пагинации: `GET /backups` и `GET /performance/recordings` материализуют всю таблицу
- **Severity:** Low · **Confidence:** High
- **Evidence:** `BackupsEndpoints.cs:40-61`, `PerformanceEndpoints.cs:95-108` — `OrderByDescending(...).ToListAsync()` без Skip/Take (остальные списки пагинированы и закламплены).
- **Риск:** деградация на горизонте месяцев/лет; PerfRecordings к тому же без retention ([BE-13]).
- **Рекомендация:** пагинация или явный `Take(N)`.

### [BE-18] Смена роли пользователя — два коммита Identity без транзакции; сбой посередине оставляет пользователя без ролей
- **Severity:** Low · **Confidence:** High
- **Evidence:** `UsersEndpoints.cs:288-306` (`RemoveFromRolesAsync` → `AddToRoleAsync`).
- **Риск:** окно крошечное, восстановимо повторной правкой; guard «последний админ» сбой посередине не покрывает.
- **Рекомендация:** транзакция вокруг пары либо осознанно принять.

### [BE-19] Застрявший `Running`-бэкап восстанавливается только при рестарте процесса
- **Severity:** Low · **Confidence:** Med
- **Evidence:** `BackupOrchestrator.cs:147-151` (fire-and-forget `Task.Run`), :160-204 (`RecoverInterruptedAsync` — только на старте, `BackupPumpService.cs:35`); `BackupRetentionJob.cs:100-101` (reap исключает Running — зависший Running не чистится никогда).
- **Риск:** экзотический сбой Task без падения процесса → база навсегда выпадает из бэкапов до рестарта панели. Штатные пути закрыты try/catch'ами.
- **Рекомендация:** TTL-reaper «Running дольше X» в retention-джобе или pump-тике.

### [BE-20] `CancellationToken.None` у всех 5 recurring-джобов — нет graceful-прерывания при остановке службы
- **Severity:** Low · **Confidence:** Med
- **Evidence:** `Program.cs:290,298,307,315,323`; `ct.ThrowIfCancellationRequested()` внутри джобов (напр. `AuditRetentionJob.cs:76`) никогда не срабатывает по shutdown.
- **Риск:** abort между батчами безопасен (commit-per-batch), но прерывание негладкое; snapshot-бакет может потеряться.
- **Рекомендация:** прокинуть Hangfire job-token вместо None.

### [BE-21] Retention-джобы при устойчивом сбое БД ретраятся дефолтные 10 раз
- **Severity:** Low · **Confidence:** High
- **Evidence:** `AuditRetentionJob.cs:86`, `BackupRetentionJob.cs:135` (`throw`); `AutomaticRetry` нигде не переопределён.
- **Риск:** только шум — удаление идемпотентно (commit-per-batch, cutoff пересчитывается), аудит пишется после удаления.
- **Рекомендация:** опционально `[AutomaticRetry(Attempts=0..1)]` на ночные джобы.

### [BE-22] Неизвестный enum-int в исторической аудит-записи сериализуется числом — тихая деградация контракта (не 500)
- **Severity:** Low · **Confidence:** High (эмпирический прогон скептика на net10.0)
- **Evidence:** `Program.cs:35` (`JsonStringEnumConverter` без `allowIntegerValues:false`); проверочный запуск: `(Foo)9999` → `9999` числом, без исключения. Исходное утверждение зоны 6 «GET /audit упадёт 500» — **опровергнуто**.
- **Риск:** FE получит число вместо строкового имени — деградация отображения, не отказ.
- **Рекомендация:** тест на `(AuditActionType)9999` + обработка числового значения на FE (или не трогать — поведение приемлемое).

### [BE-23] Уникальность под single-host допущением: дубли защищены только in-process замками
- **Severity:** Low (на single-host риска нет; флаг на будущее) · **Confidence:** High
- **Evidence:** `BackupOrchestrator.cs:62-86` (check-then-insert активного бэкапа под `SemaphoreSlim`; индекс `(DatabaseServer,DatabaseName,Status)` НЕ unique — `AppDbContext.cs:229`); `LicenseUsageSnapshots` — нет unique на (TenantId, BucketStart), сериализация только `DisableConcurrentExecution`.
- **Риск:** при отходе от single-host (ADR-28) — дубли активных бэкапов и телеметрии.
- **Рекомендация:** зафиксировать допущение в ADR; при масштабировании — фильтрованные unique-индексы.

### [BE-24] Хрупкие/пустые тесты: timezone-зависимый assert, Task.Delay-синхронизация, smoke-пустышка
- **Severity:** Low · **Confidence:** High
- **Evidence:** `tests/.../Clusters/RacExecutableRasClusterClientTests.cs:55-57` (assert через `DateTimeKind.Local → ToUniversalTime()` — корректность зависит от пояса машины; на RU-машине UTC+3 регрессия конвертации может быть замаскирована, а CI-гейт сейчас локальный); `Jobs/EnforcementGateTests.cs:28,51,78` + `Publishing/WebinstConcurrencyGateTests.cs` (`Task.Delay(50)` — флакинесс под нагрузкой); `SmokeTests.cs:20-32` (NotBeNull на DbSet — не может упасть).
- **Риск:** маскировка регрессий и ложное чувство покрытия.
- **Рекомендация:** DateTimeOffset с явной зоной; TaskCompletionSource вместо Delay; смысловой smoke.

### [BE-25] KillEnforcer: до 20 последовательных SaveChanges под enforcement-замком
- **Severity:** Low · **Confidence:** High
- **Evidence:** `KillEnforcer.cs:139-145` (`_audit.LogAsync` в foreach, `MaxKillsPerCycle=20`).
- **Риск:** только латентность — замок держится дольше на 20 round-trip к БД; усугубляет [BE-06].
- **Рекомендация:** батчить аудит-записи одним SaveChanges после цикла.

### [BE-26] IIS discovery-методы бросают вне try — хрупкий контракт «эндпоинт обязан ловить»
- **Severity:** Low · **Confidence:** Med
- **Evidence:** `Publishing/OneCIisPublishingService.cs:46-55`, `OneCIisLifecycleService.cs:44-62` (by-design проброс; в отличие от `ReadActualStateAsync`, где исключения мапятся в ErrorState).
- **Риск:** новый потребитель без обёртки получит generic 500 на типовой ситуации (неэлевированный процесс).
- **Рекомендация:** ловить в адаптере и возвращать Available-флаг единообразно. Смежно: `RacOutputParser` построчный — многострочные значения rac потеряются (`RacOutputParser.cs:31-58`); для текущих полей переносов не бывает, зафиксировать ограничение в ADR-3.3.

---

## 3. Положительно подтверждено (контрпроверки, не findings)

- **Anti-corruption граница соблюдена по коду:** в Web нет ни одного прямого `rac.exe`/`ServerManager`/`Process.Start`/`SqlConnection`-вызова (единственная утечка — тип `SqlException` для классификации уникальных нарушений, см. [BE-08]); инфраструктурные пакеты только в Infrastructure.csproj; DI централизована в `AddInfrastructure`. Направление слоёв по ProjectReference чистое (Domain без зависимостей).
- **Критический сценарий «джоб массово отвязывает базы при недоступном кластере» НЕ воспроизводится:** снапшот строится из сессий, недоступный кластер → пустой список → нулевое потребление → early-return enforcement'а без kill'ов (`ReconciliationJob.cs:78-115`, `KillEnforcer.cs:67-70`); привязки ИБ reconcile-путь не трогает; diff «missing in cluster» гейтится на `Available:true` (`UnassignedInfobasesEndpoints.cs:66-74`).
- **Запуск rac.exe сделан добротно:** таймаут 30с + `Process.Kill(entireProcessTree:true)`, параллельное чтение stdout/stderr (deadlock-free), декод сырых байтов OEM/CP866, аргументы через `ArgumentList` (без shell-инъекции). Парсер rac «never throws»: пустой/обрезанный/мусорный вывод → пустой/частичный список, битые записи отфильтрованы `Guid.TryParse`.
- **IIS-адаптеры:** `ServerManager` везде в using (COM Dispose), мутации сериализованы гейтом N=1, многошагового полусоздания публикаций нет (создание делегировано webinst), патч web.config — атомарный `File.Replace`.
- **Ошибки эндпоинтов единообразны:** везде ProblemDetails/ValidationProblem, глобальный `UseExceptionHandler` санитизирует 5xx, stack trace наружу не утекает. Динамической сортировки по пользовательскому полю нет (инъекция в OrderBy неприменима).
- **EF-гигиена на read-пути:** `AsNoTracking` + проекции систематически; Hangfire-джобы получают свежий DI-scope (утечек ChangeTracker нет); singleton-сервисы корректно делают `CreateScope()`; retention-джобы — образцовые (батчи 5000, commit-per-batch, ExecutionStrategy под `EnableRetryOnFailure`). Снапшот соответствует конфигурациям (выборочная сверка последних миграций — расхождений нет).
- **Аудит-покрытие мутаций широкое:** все CRUD tenant/infobase/publication, user-операции, kill (ручной и авто), настройки, IIS-операции, бэкапы — пишут аудит (полная таблица — в материалах зоны 6); пробелы — только [BE-10]/[BE-11].

---

## 4. Топ-5 рисков

1. **[BE-06] Over-kill пользовательских сеансов при истечении 180с-замка** — единственный finding, способный навредить конечным пользователям 1С, и проявляется именно когда кластер уже деградирует. Дешёвый фикс (таймауты согласовать).
2. **[BE-03] Отрицательный лимит тихо отключает enforcement** — ядро продукта (квоты) отключается опечаткой без индикации; плюс 500 на dashboard от переполнения суммы.
3. **[BE-01] Аудит неатомарен; ложная запись TenantDeleted возможна** — подрывает доверие к журналу, который является одной из главных ценностей продукта.
4. **[BE-05] Тихое «застревание» статусов публикаций** — одна сбойная публикация лишает оператора актуальной картины по всем остальным, без какого-либо сигнала.
5. **[BE-09]+[BE-04]+[BE-12] Связка тестовых слепых зон вокруг главных барьеров:** валидационный хелпер и CP866-декод не защищены от регрессий, а max-длины дают 500 — при выключенном GitHub CI (локальные прогоны — единственный гейт) цена регрессии возрастает.

## 5. Вердикт по области

**Готово с оговорками.** Blocker'ов и подтверждённых High нет: архитектурная граница реально соблюдена, адаптеры 1С/IIS сделаны устойчиво, опасные сценарии (массовая отвязка при недоступном кластере, утечка стека, deadlock на rac) контрпроверены и не воспроизводятся. Оговорки: 12 Medium-findings, из которых до релиза рекомендуется закрыть как минимум четыре дешёвых и значимых — [BE-03] (валидация лимита), [BE-06] (согласовать lock-таймаут), [BE-05] (per-item catch), [BE-10] (аудит неудачных входов); [BE-01] и [BE-04] — в первый пострелизный цикл; остальное — плановый hardening.

## 6. Что НЕ успел покрыть

- Полная построчная сверка всех 19 миграций со снапшотом (сделана выборочная: последние миграции + ключевые индексы — чисто).
- Поведение под реальным кластером/IIS: весь аудит статический; таймауты `ServerManager`-чтений внутри `ReadActualStateAsync`, фактический язык вывода rac на не-RU локали (определяет реальную severity [BE-16]) не проверялись исполнением.
- Внутренности `SqlBackupAdapter` (BACKUP-команды, verify-before-delete), `LicenseUsageAccumulator` (теряется ли бакет при сбое после `RecordSample` — finding зоны 4 остался Low/Med-confidence), `RasHealthProbingService`/`ClusterUuidCache`.
- Сквозные HTTP-тесты авторизации (WebApplicationFactory в проекте отсутствует — роли проверяются только на уровне деклараций; фактическую матрицу доступа покрывает А0 §2.2 статически).
- Сигнатуры всех 32 Application-интерфейсов на утечку инфраструктурных типов (граф ссылок Application чист, что исключает худшее).
- Multi-node сценарии — вне объёма по ADR-28 (single-host); флаги на будущее — [BE-23].

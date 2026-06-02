# MitLicense Center — Project Backlog

Единый реестр проблем, улучшений и выполненных работ. Этот файл **постоянно
поддерживается в актуальном состоянии** и читается первым при каждом следующем
запуске работы над проектом.

Канон проекта (`docs/01..06 + DECISIONS.md + ROADMAP.md + OPERATIONS.md`) — источник
правды по архитектуре v1. Бэклог не дублирует канон, а фиксирует расхождения,
дефекты и улучшения поверх него.

## Как пользоваться

1. Прочитать этот файл.
2. Найти задачу, помеченную `NEXT TASK`.
3. Выполнить **только её**.
4. После выполнения обновить `Status` и (при необходимости) описание.
5. Выбрать следующую задачу с максимальным ROI как `NEXT TASK`.
6. Не выполнять больше одной задачи за сессию без отдельного указания.

При обнаружении расхождения кода и документации — не исправлять автоматически:
завести запись `[Doc divergence]`, описать расхождение, предложить варианты.

## Формат записи

`ID` · `Category` (Architecture / Security / Performance / Maintainability / Testing /
Frontend / Backend) · `Priority` (P1 / P2 / P3) · `Severity` (Critical / High / Medium /
Low) · `Module` · `File(s)` · `Description` · `Impact` · `Recommendation` · `Status`.

**Статусы:** `Open` · `Approved` · `In Progress` · `Done` · `Rejected`.

**Приоритеты:** `P1` — исправить обязательно · `P2` — желательно · `P3` — можно отложить.

## Общая оценка (аудит от 2026-06-01)

Зрелый, аккуратно сделанный проект. Строгие adapter-границы к 1С/IIS (Web никогда не
дёргает `rac.exe`/IIS напрямую), source-generated логирование, DPAPI-секреты,
идемпотентный kill с re-fetch+verify в `KillEnforcer`, две очереди hot/cold,
аудит-инвариант 210/211, двухслойный `dbo.Settings`. Background-сервисы устойчивы
(try/catch вокруг цикла + обработка cancellation). Найденные проблемы точечные, не
структурные. Критичных дыр в безопасности нет; есть один безопасностно-значимый
concurrency-дефект на пути авто-kill'а (MLC-001).

---

## P1 — исправить обязательно

### MLC-001 — Нет защиты от параллельного запуска цикла согласования → возможен over-kill
- **Category:** Architecture / Backend
- **Priority:** P1 · **Severity:** High
- **Module:** Session & License Enforcer
- **File(s):** `backend/src/MitLicenseCenter.Web/Program.cs:156` (RecurringJob `cold-snapshot`); `backend/src/MitLicenseCenter.Infrastructure/Jobs/ReconciliationJob.cs:52`; `backend/src/MitLicenseCenter.Infrastructure/Jobs/ColdThrottleState.cs`
- **Description:** Recurring-job `cold-snapshot` зарегистрирован как `* * * * *` (раз в
  минуту). Единственный guard — in-memory `ColdThrottleState`, который ставит
  `LastRunAttemptUtc` в начале цикла (`MarkRun(now)` на строке 60). Если цикл длится
  дольше `Polling.ColdIntervalSeconds` (реально под таймаутами `rac.exe`: до ~120с —
  `ListActiveSessionsAsync` = 2×30с, плюс повторный re-fetch внутри `EnforceAsync` —
  ещё 2×30с), следующий минутный тик проходит guard (60с ≥ 25с) и `EnforceAsync`
  исполняется **параллельно** с ещё не завершившимся первым циклом. Hangfire по
  умолчанию не запрещает overlap recurring-job.
- **Impact:** Два цикла независимо вычисляют over-limit и убивают сеансы newest-first
  до `MaxKillsPerCycle = 20` каждый → суммарно может быть убито больше сеансов, чем
  нужно (потеря несохранённой работы у арендатора). Прямо нарушает требование
  `02_ARCHITECTURE_REQUIREMENTS.md` («distributed locks (or database locks) … only one
  enforcement loop runs at a time to prevent race conditions (e.g., killing too many
  sessions at once)»).
- **Recommendation:** Навесить `[DisableConcurrentExecution(timeoutInSeconds)]` на
  `RunColdAsync` (атрибут Hangfire на методе интерфейса `IReconciliationJob`).
  Опционально тот же атрибут на `DriftCheckJob.RunAllAsync` и `AuditRetentionJob.RunAsync`.
  Покрыть тестом: повторный (re-entrant) вызов при «занятом» цикле не должен запускать
  вторую `EnforceAsync`. Риск регрессий минимален — поведение при нормальной скорости
  RAS не меняется.
- **Status:** **Done** (2026-06-01) — см. «Выполненные работы».

### MLC-002 — Ручной kill пишет аудит «завершён» даже при неудаче и не сверяет дескриптор
- **Category:** Backend / Security (audit-integrity)
- **Priority:** P1 · **Severity:** Medium-High
- **Module:** Session & License Enforcer
- **File(s):** `backend/src/MitLicenseCenter.Web/Endpoints/SessionsEndpoints.cs:53-90`
- **Description:** `KillAsync` вызывает `cluster.KillSessionAsync(...)`, **игнорирует**
  возвращаемый `KillSessionResult` и безусловно пишет `SessionKilled (ManualByAdmin)`.
  В отличие от `KillEnforcer`, нет re-fetch + verify `(InfobaseId, AppID, StartedAt)`
  против свежих данных кластера — kill идёт по возможно устаревшему (до 25с) снапшоту.
- **Impact:** При недоступном RAS оператор видит «сеанс завершён оператором», хотя
  ничего не произошло → запись-ложь в неизменяемом аудите (нарушение базового
  требования аудита из `01`/`03`).
- **Recommendation:** Проверять `KillSessionResult`; писать аудит только при
  `Killed || AlreadyGone`, иначе вернуть осмысленную локализованную ошибку (например,
  409/502). Привести к протоколу `KillEnforcer` (см. `DECISIONS.md` «Idempotent kill
  protocol»).
- **Status:** **Done** (2026-06-01) — см. «Выполненные работы».

### MLC-003 — Сидинг — fire-and-forget; при ошибке приложение продолжает работать «полузасеянным»
- **Category:** Backend / Maintainability (operability)
- **Priority:** P1 · **Severity:** Medium
- **Module:** Web host / bootstrap
- **File(s):** `backend/src/MitLicenseCenter.Web/Program.cs:177-195`
- **Description:** `IdentitySeeder` (применяет миграции) и `SettingsSeeder` запускаются
  внутри `Task.Run(...)` из `ApplicationStarted`. В `catch` стоит `LogCritical; throw;`,
  но `throw` внутри `Task.Run` остаётся unobserved — приложение не падает и продолжает
  принимать трафик без засеянного admin-аккаунта и (если упали миграции) с неработающей БД.
- **Impact:** Тихий старт в нерабочем состоянии; оператор не получает чёткого сигнала
  об ошибке инициализации.
- **Recommendation:** Выполнять миграции/сидинг синхронно до приёма трафика, либо при
  сбое вызывать `IHostApplicationLifetime.StopApplication()`. Принцип fail-fast.
- **Status:** **Done** (2026-06-01) — см. «Выполненные работы».

---

## P2 — желательно

### MLC-004 — Нет глобального обработчика ошибок / ProblemDetails; гонки уникальности → голый 500
- **Category:** Backend / API-contract
- **Priority:** P2 · **Severity:** Medium
- **Module:** Web pipeline / Infobase & Tenant management
- **File(s):** `backend/src/MitLicenseCenter.Web/Program.cs` (нет `AddProblemDetails`/`UseExceptionHandler`); `backend/src/MitLicenseCenter.Web/Endpoints/InfobasesEndpoints.cs:147-162,247-258,334-337`; `backend/src/MitLicenseCenter.Web/Endpoints/TenantsEndpoints.cs`
- **Description:** Pipeline не содержит `UseExceptionHandler`/`AddProblemDetails`.
  Паттерн check-then-insert (две `AnyAsync` + insert) не транзакционно-безопасен: при
  гонке backstop — уникальные индексы (`IX_Infobases_ClusterInfobaseId`,
  `IX_Infobases_TenantId_Name`), но возникающий `DbUpdateException` не ловится → в
  production голый 500 без тела ProblemDetails.
- **Impact:** Нарушение задокументированного 409-контракта при конкурентной вставке;
  фронт не может разобрать ответ. Вероятность низкая (5–20 пользователей), но контракт
  нарушается.
- **Recommendation:** Добавить `AddProblemDetails()` + `UseExceptionHandler`; ловить
  `DbUpdateException` (нарушение уникального индекса) и мапить в соответствующий
  `ProblemCodes.*` 409.
- **Status:** **Done** (2026-06-02) — см. «Выполненные работы».

### MLC-005 — [Doc divergence] ADR-14 ссылается на отсутствующий scripts/Deploy-MitLicenseCenter.ps1
- **Category:** Maintainability (docs)
- **Priority:** P2 · **Severity:** Low-Medium
- **Module:** Docs / scripts
- **File(s):** `docs/DECISIONS.md:75`; `scripts/` (есть `build.ps1`/`db-reset.ps1`/`dev.ps1`/`shadcn-add.ps1`, нет `Deploy-*`)
- **Description:** ADR-14 декларирует «deploy is manual via
  `scripts/Deploy-MitLicenseCenter.ps1`», но скрипта в репозитории нет.
- **Impact:** Канон обещает несуществующий артефакт; оператор не найдёт процедуру деплоя.
- **Recommendation (варианты):** (a) добавить скрипт деплоя; (b) переписать ADR-14 под
  фактическую ручную процедуру либо убрать ссылку. Не исправлять автоматически — выбрать вариант.
- **Status:** **Done** (2026-06-02, вариант **b**) — см. «Выполненные работы».

### MLC-006 — [Doc divergence] Канон обещает OpenAPI-генерируемый TS-клиент; фактически типы рукописные
- **Category:** Maintainability (docs / API)
- **Priority:** P2 · **Severity:** Medium
- **Module:** Frontend / Docs
- **File(s):** `docs/DECISIONS.md` ADR-10/ADR-13 («TypeScript client is generated from the OpenAPI spec» / «OpenAPI-generated TS client»); `frontend/src/features/*/types.ts` (рукописные); `frontend/package.json` (нет codegen-шага)
- **Description:** Типы API в `features/*/types.ts` поддерживаются вручную; генерации из
  OpenAPI-спеки нет.
- **Impact:** Риск расхождения FE-типов с реальным контрактом backend; канон вводит в
  заблуждение относительно процесса.
- **Recommendation (варианты):** (a) внедрить codegen из `/api/docs/v1/swagger.json`;
  (b) обновить ADR-10/13, зафиксировав рукописные типы как сознательный выбор.
- **Status:** **Done** (2026-06-02, вариант **b**) — см. «Выполненные работы». Вариант (a)
  (codegen) остаётся открытой опцией как `MLC-006(a)`; связан с `MLC-016` (runtime-валидация).

### MLC-007 — Frontend: нет тестов на CRUD-мутации, 409-обработчики и ролевой ProtectedRoute
- **Category:** Testing / Frontend
- **Priority:** P2 · **Severity:** Medium
- **Module:** Frontend
- **File(s):** `frontend/src/features/**` (диалоги Create/Update/Delete/Reassign); `frontend/src/features/auth/ProtectedRoute.tsx`
- **Description:** Хорошо покрыты pure-утилиты (`api.ts`, `grouping`, `paths`,
  `retention`, `urlState`). Не покрыты: мутации, разбор 409 (`NAME_DUPLICATE_IN_TENANT`,
  `INFOBASE_ALREADY_ASSIGNED`, `INFOBASE_NAME_TAKEN_IN_TARGET`, `TENANT_HAS_INFOBASES`),
  ролевое гейтирование Admin/Viewer.
- **Impact:** Регрессии в формах и ролевом доступе не отлавливаются CI.
- **Recommendation:** Тесты на ключевые мутации, маппинг 409 → локализованное поле формы
  и `ProtectedRoute`.
- **Status:** **Done** (2026-06-02) — см. «Выполненные работы».

### MLC-008 — Контрактные тесты идут на EF InMemory: уникальность/каскад/гонки не проверяются
- **Category:** Testing / Backend
- **Priority:** P2 · **Severity:** Medium
- **Module:** Backend tests
- **File(s):** `backend/tests/MitLicenseCenter.Tests.Unit/Endpoints/TestHelpers.cs` (`NewInMemoryDb`); напр. `Endpoints/InfobaseCascadeDeleteTests.cs`
- **Description:** EF Core InMemory-провайдер игнорирует unique-индексы, FK-cascade и
  конкурентность (это прямо отмечено в `InfobasesEndpoints.DeleteAsync` — каскад
  публикации эмулируется вручную).
- **Impact:** Центральные доменные инварианты (per-tenant имя, глобальная уникальность
  кластер-базы, каскад публикации) не валидируются на уровне реального поведения БД.
- **Recommendation:** Контрактные тесты на SQLite-in-memory или Testcontainers MSSQL для
  persistence-инвариантов.
- **Status:** **Done** (2026-06-02) — см. «Выполненные работы».

### MLC-009 — Сообщения инфраструктурных исключений уходят клиенту дословно (не локализованы, info-leak)
- **Category:** Security / Frontend (UX)
- **Priority:** P2 · **Severity:** Low-Medium
- **Module:** Discovery / Publications (IIS)
- **File(s):** `backend/src/MitLicenseCenter.Web/Endpoints/DiscoveryEndpoints.cs:62-67,80-84`; `backend/src/MitLicenseCenter.Web/Endpoints/PublicationsEndpoints.cs:194-211`
- **Description:** `ex.Message` (SQL / COM / IO) возвращается в `Error`/`detail`.
  Admin-only снижает риск, но это англоязычные тексты (нарушают «русские user-facing
  ошибки» из `01`) и могут раскрывать имена серверов/пути.
- **Impact:** Утечка внутренних деталей в UI; нарушение требования локализации.
- **Recommendation:** Логировать исключение полностью, наружу отдавать санитизированное
  локализованное сообщение (детали — в логи / Hangfire dashboard).
- **Status:** **Done** (2026-06-02) — см. «Выполненные работы».

---

## P3 — можно отложить

### MLC-010 — SettingsSnapshot: sync-over-async под локом + N запросов на каждое обновление кэша
- **Category:** Performance / Backend
- **Priority:** P3 · **Severity:** Low-Medium
- **Module:** Settings
- **File(s):** `backend/src/MitLicenseCenter.Infrastructure/Settings/SettingsSnapshot.cs:53-78`
- **Description:** `EnsureLoaded` держит `lock` и выполняет
  `store.GetAsync(key).GetAwaiter().GetResult()` по каждому ключу каталога (~13 запросов).
  При залипании БД все hot-path читатели (reconciliation, hot-polling, адаптер)
  блокируются на локе; sync-over-async рискует истощением пула потоков.
- **Recommendation:** Грузить одним запросом и вне лока (либо асинхронный refresh с
  double-check внутри короткой критической секции).
- **Status:** Open

### MLC-011 — Web-слой зависит от AppDbContext/Infrastructure напрямую; бизнес-правила в endpoint'ах
- **Category:** Architecture
- **Priority:** P3 · **Severity:** Low (наблюдение)
- **Module:** Web / Application
- **File(s):** `backend/src/MitLicenseCenter.Web/Endpoints/*Endpoints.cs` (инжектят `AppDbContext`, `Roles`, типы Identity)
- **Description:** Вертикальные срезы вместо строгой Clean/onion: Application — почти
  только интерфейсы, use-case-handler'ов нет; валидация / уникальность / аудит живут в
  Web-слое. При этом адаптерные границы к 1С/IIS соблюдены строго, так что главный
  запрет `02` («Web … must NEVER interact directly with 1C or IIS») не нарушен.
- **Impact:** Бизнес-правила трудно переиспользовать/тестировать вне HTTP; размывается
  «strict logical boundaries» из `02`.
- **Recommendation:** Подтвердить как осознанный выбор (зафиксировать ADR) либо поэтапно
  вынести правила в Application. Без массового рефакторинга.
- **Status:** Open

### MLC-012 — Хардненинг прод-конфигурации: нет HTTPS-redirect/HSTS; Encrypt=False/TrustServerCertificate=True; Swagger в проде
- **Category:** Security
- **Priority:** P3 · **Severity:** Low
- **Module:** Web pipeline / config
- **File(s):** `backend/src/MitLicenseCenter.Web/Program.cs:122-147`; `backend/src/MitLicenseCenter.Web/appsettings.json:3-4,14`
- **Description:** В pipeline нет `UseHttpsRedirection`/`UseHsts`; базовый `appsettings.json`
  везёт `Encrypt=False;TrustServerCertificate=True`, `AllowedHosts:*`; Swagger UI
  отдаётся без ограничения окружения. Приемлемо для single-node за сетевым периметром
  (см. `OPERATIONS.md`), но стоит задокументировать/ужесточить для прода. Cookie
  `Secure=Always` в проде — уже корректно.
- **Recommendation:** HSTS + redirect (если перед сервисом нет прокси, делающего это);
  prod-override строки подключения (`Encrypt=True`); ограничить Swagger non-prod либо
  за ролью Admin.
- **Status:** Open

### MLC-013 — Пароль кластера 1С передаётся в командной строке rac.exe
- **Category:** Security
- **Priority:** P3 · **Severity:** Low
- **Module:** 1C Cluster Adapter
- **File(s):** `backend/src/MitLicenseCenter.Infrastructure/Clusters/RacExecutableRasClusterClient.cs:264-277`
- **Description:** `--cluster-pwd=<password>` виден другим процессам/администраторам на
  Windows (командные строки процессов читаемы). Ограничение самого `rac.exe`; на
  single-node admin-only хосте риск низкий.
- **Recommendation:** Зафиксировать как принятый риск в `04_INFRASTRUCTURE.md`/ADR;
  отслеживать появление stdin-передачи пароля в `rac.exe`.
- **Status:** Open

### MLC-014 — Frontend: интерфейс ConflictBody продублирован в ~5 диалогах
- **Category:** Maintainability / Frontend
- **Priority:** P3 · **Severity:** Low
- **Module:** Frontend
- **File(s):** `frontend/src/features/infobases/{InfobaseFormDialog,ReassignInfobaseDialog}.tsx`; `frontend/src/features/tenants/{TenantFormDialog,DeleteTenantDialog}.tsx`
- **Description:** Тип `ConflictBody` (форма 409-ответа) переопределяется в каждом
  диалоге вместо единого определения.
- **Recommendation:** Вынести `ConflictBody` в `lib/api.ts` или общие типы, переиспользовать.
- **Status:** Open

### MLC-015 — Frontend: pageSize=200 захардкожен, нет UI пагинации; allInfobases грузится при каждом открытии формы
- **Category:** Performance / Frontend
- **Priority:** P3 · **Severity:** Low-Medium
- **Module:** Frontend
- **File(s):** `frontend/src/features/infobases/useInfobases.ts`; `frontend/src/features/tenants/useTenants.ts`; `frontend/src/features/infobases/InfobaseFormDialog.tsx` (`allInfobasesQuery`)
- **Description:** Списки клиентов/баз тянут до 200 элементов без UI пагинации; форма
  добавления базы подгружает все базы для проверки уникальности кластер-ID при каждом
  открытии.
- **Impact:** Не масштабируется за ~200 баз; лишний трафик.
- **Recommendation:** Серверная пагинация в UI и/или отдельный endpoint проверки
  занятости кластер-базы.
- **Status:** Open

### MLC-016 — Frontend: рукописные типы API без runtime-валидации (риск расхождения)
- **Category:** Maintainability / Frontend
- **Priority:** P3 · **Severity:** Low · _(связано с MLC-006)_
- **Module:** Frontend
- **File(s):** `frontend/src/lib/api.ts` (`payload as T`); `frontend/src/features/*/types.ts`
- **Description:** `api<T>()` приводит ответ к `T` без runtime-проверки; типы рукописные.
- **Recommendation:** Codegen из OpenAPI или Zod-схемы на границе ответа.
- **Status:** Open

### MLC-017 — Frontend: захардкоженная строка «Не авторизован» в api.ts и 401-redirect через window.location.assign
- **Category:** Frontend (i18n)
- **Priority:** P3 · **Severity:** Low
- **Module:** Frontend
- **File(s):** `frontend/src/lib/api.ts` (текст ошибки 401); `frontend/src/App.tsx` (`window.location.assign("/login")`)
- **Description:** Текст ошибки 401 — строковый литерал, не i18n-ключ (на экране он,
  впрочем, маппится в `errors.*` на LoginPage); редирект на `/login` идёт мимо React
  Router.
- **Recommendation:** Бросать код ошибки без литерала; редирект — через router-навигацию.
- **Status:** Open

---

## Приоритезация по ROI

`ROI = (риск × влияние на стабильность / безопасность / целостность аудита / поддержку) ÷ трудоёмкость.`

1. **MLC-001** — наивысший ROI: безопасностно-значимый дефект на пути авто-kill'а
   (потенциальный over-kill = потеря работы арендатора), прямо нарушает binding-требование
   `02`, фикс — один Hangfire-атрибут + тест, риск регрессий минимален. → **Done**.
2. **MLC-002** (Done), **MLC-003** (Done) — целостность аудита и fail-fast старта; малый
   объём правок. Оба P1 закрыты.
3. **P2** — контракт ошибок (MLC-004 → **Done**: глобальный ProblemDetails + маппинг
   гонок уникальности в 409), doc-divergences (MLC-005/006 → **Done**: канон приведён к
   реальности — ручной деплой в `OPERATIONS.md`, рукописные TS-типы зафиксированы в
   ADR-10.1), персистентность на реальном провайдере (MLC-008 → **Done**: SQLite-in-memory
   контрактные тесты unique/cascade/restrict/setnull), пробелы во FE-тестах (MLC-007 →
   **Done**: vitest + @testing-library тесты на ProtectedRoute, CRUD-мутации и маппинг
   409 в поле формы), info-leak (MLC-009 → **Done**: discovery/reconcile логируют
   полное исключение, наружу — санитизированный русский текст + correlationId, отмена
   пробрасывается).
4. **P3** — производительность, хардненинг, сопровождаемость (MLC-010…017).
   `MLC-010` — **NEXT TASK** (наивысший ROI в блоке: hot-path лок + sync-over-async).

---

## NEXT TASK

> **MLC-010 — SettingsSnapshot: sync-over-async под локом + N запросов на каждое обновление кэша.**
> Статус: Open. Первый таск блока P3 (все P1/P2 закрыты). `EnsureLoaded`
> (`SettingsSnapshot.cs:53-78`) держит `lock` и выполняет
> `store.GetAsync(key).GetAwaiter().GetResult()` по каждому из ~13 ключей каталога — это
> sync-over-async под локом на hot-path (reconciliation / hot-polling / адаптер). При
> залипании БД все читатели блокируются на локе, плюс риск истощения пула потоков.
> Рекомендация: грузить одним запросом и вне лока (или асинхронный refresh с
> double-check в короткой критической секции). Замерить, не сломать снапшот-семантику.

---

## Выполненные работы (Done)

### MLC-009 — Санитизация инфраструктурных исключений в discovery/reconcile — 2026-06-02
- **Что сделано:** Дословный `ex.Message` (SQL / COM / IO) больше не уходит клиенту.
  (1) **Discovery** (`DiscoveryEndpoints.GetDatabasesAsync` / `GetIisSitesAsync`): полное
  исключение логируется source-gen логгером (`LogDatabaseDiscoveryFailed` со `{Server}` /
  `LogIisSitesDiscoveryFailed`), наружу сохранён контракт `DiscoveryResponse { Available:
  false, Error: <короткий русский текст> }` (фронт показывает ручной ввод). Сырой
  `ex.Message` (имена серверов, пути, SQL/COM-детали) в `Error` не попадает. `catch(Exception)`
  заменён на `catch(Exception) when (ex is not OperationCanceledException)` — отмена запроса
  больше не выдаётся за «ошибку discovery», а пробрасывается. (2) **Reconcile**
  (`PublicationsEndpoints.ReconcileAsync`): коды `ProblemCodes.IisReconcileFailed` /
  `IisAccessDenied` сохранены, но `detail` теперь санитизированный русский текст без
  путей/имён; добавлен `correlationId` (= `HttpContext.TraceIdentifier`) в `Extensions`
  ответа и в лог (`LogReconcileAccessDenied` / `LogReconcileFailed`), чтобы оператор нашёл
  полное исключение в журнале. Перехватываются те же типы (`UnauthorizedAccessException` →
  access-denied; `COMException` / `IOException` / `InvalidOperationException` →
  reconcile-failed); cancellation там и раньше не глушился (catch'и узкие).
- **Изменения контракта:** `Problems.IisReconcileFailed` / `IisAccessDenied` больше не
  принимают `detail` из `ex.Message`, а формируют фиксированный русский текст и
  необязательный `correlationId` (новый параметр `Problems.Conflict(..., correlationId)` →
  `Extensions["correlationId"]`). Эндпоинты `GetDatabasesAsync` / `GetIisSitesAsync` /
  `ReconcileAsync` получили параметр `ILoggerFactory loggerFactory` (DI minimal-API;
  классы стали `partial` для source-gen логгеров).
- **Файлы:** `backend/src/MitLicenseCenter.Web/Endpoints/DiscoveryEndpoints.cs`,
  `.../PublicationsEndpoints.cs`, `.../Problems.cs`;
  `backend/tests/.../Endpoints/DiscoveryEndpointsTests.cs` (новый),
  `.../Endpoints/PublicationsReconcileTests.cs` (расширен); канон
  `docs/04_INFRASTRUCTURE.md` (discovery error-contract + sanitized reconcile detail) и
  `docs/DECISIONS.md` (ADR-4.1 — оговорка про санитизацию detail + correlationId).
- **Тесты (+7):** discovery — исключение → `Available:false`, `Error` без сырого текста и
  без имени сервера/`applicationHost.config`; пустой server → без вызова discovery; отмена
  (`OperationCanceledException`) пробрасывается (databases + iis-sites). Reconcile —
  `IOException`/`InvalidOperationException` → `IIS_RECONCILE_FAILED` с русским detail без
  секрета; `UnauthorizedAccessException` → `IIS_ACCESS_DENIED` + `correlationId`; во всех
  случаях аудит не пишется. Прогон: `dotnet test … --filter "Category!=Smoke"` —
  **213 passed, 0 failed** (было 206).

### MLC-008 — Контрактные тесты persistence-инвариантов на реальном провайдере (SQLite) — 2026-06-02
- **Выбранный провайдер:** **SQLite-in-memory** с одним открытым соединением
  (`DataSource=:memory:;Foreign Keys=True`). Воспроизводит все нужные инварианты
  (unique-индексы, FK Cascade/Restrict/SetNull) — Testcontainers MSSQL не понадобился.
  Схема строится из той же `AppDbContext`-модели через `EnsureCreated` (миграции —
  SQL-Server-специфичны: `varbinary(max)`, `SYSUTCDATETIME()`, схемы `dbo`/`auth` — и для
  SQLite неприменимы; `EnsureCreated` честно переносит индексы и FK-поведение из модели).
- **Фабрика БД (рядом с `NewInMemoryDb`, существующие тесты не тронуты):**
  `TestHelpers.SqliteTestDb` — держит соединение открытым на время теста (иначе in-memory
  БД исчезает) и выдаёт несколько контекстов (`NewContext`). Несколько контекстов на одной
  БД позволяют проверять поведение именно **на стороне СУБД**, а не в change-tracker'е EF:
  каскад/SetNull/restrict выполняются при удалении в «чистом» контексте, который не
  отслеживает зависимые сущности. `Foreign Keys=True` включает `PRAGMA foreign_keys`
  (в SQLite FK по умолчанию выключены).
- **Расхождение модель↔SQLite (НЕ баг схемы, отдельный MLC не заводился):** колонка
  `dbo.Settings.Value` имеет `HasColumnType("varbinary(max)")` — валидный T-SQL, но SQLite
  его DDL не парсит (`near "max": syntax error`). Решено **только в тестовом харнессе**:
  `SqliteModelCustomizer : RelationalModelCustomizer` (подключён через
  `ReplaceService<IModelCustomizer>` к тестовым опциям) после `base.Customize` переписывает
  любые `varbinary*`-типы в нативный `BLOB`. Продакшн-модель/миграции не менялись; на
  инварианты (индексы/FK) это не влияет — тип колонки к ним не относится.
- **Файлы:** `backend/Directory.Packages.props` (+`Microsoft.EntityFrameworkCore.Sqlite`
  10.0.8); `backend/tests/MitLicenseCenter.Tests.Unit/MitLicenseCenter.Tests.Unit.csproj`
  (PackageReference); `backend/tests/.../Endpoints/TestHelpers.cs` (`SqliteTestDb` +
  `SqliteModelCustomizer`); `backend/tests/.../Endpoints/PersistenceContractTests.cs` (новый).
- **Тесты (+6):** (1) `IX_Infobases_TenantId_Name` — две одноимённые базы у одного клиента
  → `DbUpdateException`; (2) одноимённые базы у **разных** клиентов — допускаются (не throw);
  (3) `IX_Infobases_ClusterInfobaseId` — один `ClusterInfobaseId` у двух клиентов →
  `DbUpdateException`; (4) FK Publication→Infobase = Cascade — удаление базы в чистом
  контексте реально сносит публикацию на стороне БД; (5) FK Infobase→Tenant = Restrict —
  БД блокирует удаление клиента с базами (`DbUpdateException`), база остаётся;
  (6) `AuditLogs.TenantId` = SetNull — удаление клиента обнуляет ссылку, но строка аудита
  остаётся. Прогон: `dotnet test … --filter "Category!=Smoke"` — **206 passed, 0 failed**
  (было 200).
- **Связь с MLC-004:** backstop-тест уникальности в `DbUpdateExceptionBackstopTests` пока
  эмулирует гонку `SaveChanges`-перехватчиком (EF InMemory не бросает unique-violation).
  Теперь, имея `SqliteTestDb`, его можно при желании укрепить **реальным**
  unique-violation сквозь endpoint — вынесено как опциональное улучшение (не входит в MLC-008).

### MLC-007 — Frontend-тесты: ProtectedRoute, CRUD-мутации, маппинг 409 — 2026-06-02
- **Что сделано:** Добавлены vitest + `@testing-library/react` тесты на ранее непокрытые
  пути фронта. (1) `ProtectedRoute` — пускает авторизованного к содержимому; admin-only
  маршрут редиректит Viewer на `/`, неавторизованного (и `data=null` без ошибки) — на
  `/login`; во время загрузки не редиректит и не показывает содержимое; Admin проходит к
  admin-only. Реализовано через мок `useMe` + `MemoryRouter`/`Routes` с маршрутами-маркерами.
  (2) Диалоги мутаций — по одному на create/update/delete/reassign: `TenantFormDialog`
  (create POST + update PUT → инвалидация `tenantsQueryKey`, success-toast, закрытие; 409
  `NAME_DUPLICATE` → ошибка на поле «Название», диалог открыт), `DeleteTenantDialog`
  (DELETE → инвалидация + закрытие; 409 `TENANT_HAS_INFOBASES` → локализованный
  error-toast), `ReassignInfobaseDialog` (POST `/reassign` → инвалидация `infobasesQueryKey`
  **и** `tenantsQueryKey`; 409 `INFOBASE_NAME_TAKEN_IN_TARGET` → inline-ошибка в диалоге),
  `InfobaseFormDialog` в edit-режиме (409 `INFOBASE_ALREADY_ASSIGNED` → поле «база кластера»;
  `NAME_DUPLICATE_IN_TENANT` → авто-раскрытие «Дополнительно» + ошибка на поле имени).
  Все четыре требуемых 409-кода покрыты с проверкой локализованного текста на правильном
  месте. Продакшн-код не менялся.
- **Подход:** Частичный мок `@/lib/api` (`{ ...actual, api: vi.fn() }`) — настоящий
  `ApiError` сохранён для `instanceof`-проверок в диалогах, сетевой `api` подменён; реальные
  хуки-мутации/запросы исполняются поверх мока, инвалидация проверяется `vi.spyOn(client,
  "invalidateQueries")`. `sonner` замокан целиком. Для `InfobaseFormDialog` `api`
  ветвится по методу: GET (discovery/список/настройки) → пустые ответы, PUT инфобазы →
  reject с нужным 409-кодом, что доводит submit до catch без правки формы. Инструмент-стаб
  Radix Select (`hasPointerCapture`/`setPointerCapture`/`releasePointerCapture`/
  `scrollIntoView`) добавлен в общий `src/test/setup.ts` — без него интеракция с `Select`
  (выбор целевого клиента в reassign) падает в jsdom. i18n инициализируется импортом
  `@/i18n` (тесты ассертят русские строки из `ru.json`).
- **Файлы:** `frontend/src/features/auth/__tests__/ProtectedRoute.test.tsx`;
  `frontend/src/features/tenants/__tests__/TenantFormDialog.test.tsx`;
  `frontend/src/features/tenants/__tests__/DeleteTenantDialog.test.tsx`;
  `frontend/src/features/infobases/__tests__/ReassignInfobaseDialog.test.tsx`;
  `frontend/src/features/infobases/__tests__/InfobaseFormDialog.test.tsx`;
  `frontend/src/test/setup.ts` (Pointer Capture / scrollIntoView стабы для Radix Select).
- **Проверка:** `pnpm lint` (0), `pnpm type-check` (0), `pnpm test` (0) — **60 passed
  (14 файлов)**, из них +13 новых. Доков с тест-стратегией в `docs/` нет — новую
  документацию не заводил.

### MLC-005 — [Doc divergence] ADR-14: ручной деплой без несуществующего скрипта — 2026-06-02
- **Выбранный вариант:** **(b)** — привести документацию к реальности (наименее рискованный
  путь для doc-driven проекта). Реальный скрипт деплоя не добавлялся: фактической
  стабильной процедуры ещё нет, а ps1 закодировал бы неустоявшийся процесс.
- **Что сделано:** (1) ADR-14 (`docs/DECISIONS.md`) переписан — убрана ссылка на
  несуществующий `scripts/Deploy-MitLicenseCenter.ps1`; теперь Decision явно говорит, что
  деплой — ручная операторская процедура из `OPERATIONS.md`, скрипта деплоя в `scripts/`
  нет (только `build`/`db-reset`/`dev`/`shadcn-add`), а будущий `Deploy-*.ps1` — пункт
  бэклога. В Reason зафиксировано, почему скрипт не делаем сейчас и как fail-fast bootstrap
  (ADR-18) делает ручную процедуру безопасной (миграции применяются на старте или хост
  не обслуживает трафик). (2) В `OPERATIONS.md` добавлен раздел «Deployment is manual —
  there is no deploy script» с пошаговой процедурой: `build.ps1` → `dotnet publish` backend
  → `pnpm build` SPA → stop/replace (не трогая `appsettings.Production.json` и DPAPI
  key ring) → backup-before-migrate → start → smoke-check; описан откат (redeploy + restore,
  down-миграций нет).
- **Файлы:** `docs/DECISIONS.md` (ADR-14); `docs/OPERATIONS.md` (новый раздел про деплой).
  Кода и скриптов не трогали.

### MLC-006 — [Doc divergence] Рукописные TS-типы зафиксированы как осознанный выбор — 2026-06-02
- **Выбранный вариант:** **(b)** — обновить канон, зафиксировав рукописные типы и способ
  поддержания соответствия контракту. Вариант (a) (внедрить codegen) вынесен как отдельная
  открытая опция `MLC-006(a)` (объём = новая задача, не правка доков), связан с `MLC-016`.
- **Что сделано:** (1) Уточнено фактическое состояние: OpenAPI/Swagger UI реально отдаётся
  (`/api/docs`, raw — `/api/docs/v1/swagger.json`, Swashbuckle) и остаётся reference-контрактом,
  но codegen-шага нет — типы рукописные в `frontend/src/features/*/types.ts` + `lib/api.ts`.
  (2) Введён **ADR-10.1 — Hand-written TypeScript API types (no OpenAPI codegen)**: решение,
  как поддерживается соответствие (ручная синхронизация по Swagger UI, camelCase System.Text.Json
  совпадает с TS-интерфейсами, `api<T>()` кастит без runtime-валидации), отклонённые
  альтернативы (`openapi-typescript`/NSwag, Zod на каждой границе) и backlog-указатели
  (`MLC-006(a)`/`MLC-016`). (3) Исправлены вводящие в заблуждение формулировки «generated
  from the OpenAPI spec»: ADR-10 (Decision), ADR-13 (Reason) и `05_UI_REQUIREMENTS.md` §2
  теперь говорят про рукописные типы и ссылаются на ADR-10.1.
- **Файлы:** `docs/DECISIONS.md` (ADR-10, новый ADR-10.1, ADR-13);
  `docs/05_UI_REQUIREMENTS.md`. Кода и `frontend/package.json` не трогали.

### MLC-004 — Глобальный ProblemDetails + backstop гонок уникальности → 409 — 2026-06-02
- **Что сделано:** (1) Pipeline получил `builder.Services.AddProblemDetails(...)` +
  outermost `app.UseExceptionHandler()`. Непойманные исключения теперь отдаются как
  RFC 7807 `ProblemDetails` (с `traceId`), а не голым 500 без тела. `CustomizeProblemDetails`
  для 5xx подменяет текст на нейтральное русское сообщение и НИКОГДА не отдаёт наружу
  message/stack trace исключения (полное исключение логирует сам middleware; в Development
  остаётся developer-exception-page). (2) В create/update/reassign `Infobase` и
  create/update `Tenant` вокруг `SaveChanges` добавлен backstop: `DbUpdateException` от
  нарушения уникального индекса мапится в тот же задокументированный 409 `ProblemCodes.*`,
  что и предварительный `AnyAsync` (который остаётся быстрым happy-path'ом). Различение
  индекса — по его имени (`IX_Infobases_ClusterInfobaseId`, `IX_Infobases_TenantId_Name`,
  `IX_Tenants_Name`), стабильному идентификатору схемы в тексте `SqlException`, с гейтом
  `SqlException.Number ∈ {2601, 2627}` — не по локализованному тексту. Один и тот же
  индекс `IX_Infobases_TenantId_Name` мапится в `NAME_DUPLICATE_IN_TENANT` на create/update
  и в `INFOBASE_NAME_TAKEN_IN_TARGET` на reassign (решение per-endpoint). Любое иное
  `DbUpdateException` пробрасывается в глобальный handler → 500. Новые `ProblemCodes` не
  заводились — backstop переиспользует существующие.
- **Файлы:** `backend/src/MitLicenseCenter.Web/Program.cs` (AddProblemDetails +
  UseExceptionHandler); `backend/src/MitLicenseCenter.Web/Endpoints/DbUniqueViolation.cs`
  (новый recognizer `UniqueIndexViolation` + `DbUniqueViolation.Identify`);
  `backend/src/MitLicenseCenter.Web/Endpoints/InfobasesEndpoints.cs` (try/catch backstop в
  Create/Update/Reassign); `backend/src/MitLicenseCenter.Web/Endpoints/TenantsEndpoints.cs`
  (backstop в Create/Update; их видимость поднята `private`→`internal` для тестов);
  `backend/tests/MitLicenseCenter.Tests.Unit/Endpoints/TestHelpers.cs`
  (`ThrowOnSaveInterceptor` + перегрузка `NewInMemoryDb` с interceptor);
  `backend/tests/MitLicenseCenter.Tests.Unit/Endpoints/DbUpdateExceptionBackstopTests.cs`;
  канон `docs/03_DOMAIN_MODEL.md` (binding «Global error envelope + uniqueness backstop»)
  и `docs/DECISIONS.md` (**ADR-19**).
- **Тесты:** +12 тестов — `Identify` по каждому имени индекса (+`None` для постороннего
  исключения и для отсутствующего inner) и endpoint-уровень: гонка create/update/reassign
  Infobase и create/update Tenant → ожидаемый `ProblemCodes.*`/409; нераспознанное
  `DbUpdateException` (FK) не глотается, а пробрасывается. EF InMemory не воспроизводит
  unique-violation (MLC-008), поэтому гонка эмулируется `SaveChanges`-перехватчиком,
  бросающим SQL-Server-подобное `DbUpdateException`. Прогон: `dotnet test … --filter
  "Category!=Smoke"` — **200 passed, 0 failed**.

### MLC-003 — Fail-fast старт: миграции/сидинг синхронно до приёма трафика — 2026-06-01
- **Что сделано:** Сидинг переведён с fire-and-forget на синхронный fail-fast. В
  `Program.cs` блок `ApplicationStarted.Register(() => Task.Run(...))` заменён на прямой
  `await` **до** `app.RunAsync()`: `IdentitySeeder.EnsureSeededAsync` (миграции +
  роли/admin) → `SettingsSeeder.EnsureSeededAsync`. Порядок сохранён (таблица
  `dbo.Settings` создаётся миграцией до своего сидера). При любой ошибке —
  `LogCritical` + проброс исключения из `Main`: процесс падает с ненулевым кодом и
  **не открывает порт**. Устранён прежний unobserved `throw` внутри `Task.Run`, из-за
  которого хост тихо стартовал «полузасеянным» (без admin'а / с неприменёнными миграциями).
- **Тестовая совместимость:** в `IdentitySeeder.EnsureSeededAsync` вызов `MigrateAsync`
  обёрнут в `if (db.Database.IsRelational())` — на EF in-memory провайдере (будущие
  интеграционные тесты через `WebApplicationFactory<Program>`, где сидинг теперь
  выполняется в пайплайне старта) `Migrate` не вызывается и не бросает. Маркер
  `public partial class Program` сохранён.
- **Поведение Development:** стартовый случайный пароль admin'а по-прежнему пишет в лог
  сам `IdentitySeeder` (`Warning`), теперь гарантированно до приёма первого запроса.
- **Файлы:** `backend/src/MitLicenseCenter.Web/Program.cs`;
  `backend/src/MitLicenseCenter.Infrastructure/Identity/IdentitySeeder.cs`; канон
  `docs/DECISIONS.md` (новый **ADR-18 — Fail-fast Bootstrap**) и `docs/OPERATIONS.md`
  (раздел «Startup is fail-fast»).
- **Проверка:** сборка 0 ошибок; `dotnet test … --filter "Category!=Smoke"` — 188 passed,
  0 failed. Ручная верификация: (1) штатная строка подключения — миграции применяются
  синхронно, в логах порядок `EF migrations` → `Now listening` → `Application started`;
  (2) `Hangfire`-строка рабочая, `Default` указывает на недоступный SQL — `LogCritical`
  (`crit: MitLicenseCenter.Web`), стек `MigrateAsync → IdentitySeeder:33 → Program.<Main>:188`,
  `Unhandled exception`, процесс завершается **без** строк `Now listening` / `Application
  started` (трафик не принят).

### MLC-002 — Ручной kill: аудит только при реальном завершении + сверка дескриптора — 2026-06-01
- **Что сделано:** `SessionsEndpoints.KillAsync` приведён к идемпотентному протоколу
  `KillEnforcer`. Теперь endpoint: (1) `404`, если сеанса нет в текущем снапшоте;
  (2) re-fetch `ListActiveSessionsAsync` и сверка `(ClusterInfobaseId, AppID, StartedAt)`
  свежего сеанса со снапшотом — при несовпадении (тот же `SessionId`, другой дескриптор)
  `409 SESSION_STALE` без kill'а (не убиваем чужой/перезапущенный сеанс); (3) проверка
  `KillSessionResult` — аудит `SessionKilled (ManualByAdmin)` пишется **только** при
  `Killed || AlreadyGone`; при недоступном RAS (оба флага `false`) — `502 CLUSTER_UNAVAILABLE`
  и **никакой** записи в неизменяемый аудит. Устранена запись-ложь «сеанс завершён
  оператором» при неудачном kill'е.
- **Контракт ответа:** прежние `204`/`404` сохранены; добавлены `409 SESSION_STALE`
  и `502 CLUSTER_UNAVAILABLE` в стиле `ProblemDetails` + machine-readable `code`
  (`Problems.cs::ProblemCodes`). Frontend `KillSessionDialog` не трогали — новые коды
  попадают в существующий generic-error-toast (404 → «уже завершён» как раньше).
- **Файлы:** `backend/src/MitLicenseCenter.Web/Endpoints/SessionsEndpoints.cs`;
  `backend/src/MitLicenseCenter.Web/Endpoints/Problems.cs` (новые `ProblemCodes.SessionStale`
  / `ClusterUnavailable` + фабрики `Problems.SessionStale()` / `ClusterUnavailable()`);
  `backend/tests/MitLicenseCenter.Tests.Unit/Endpoints/SessionsKillEndpointTests.cs`;
  канон `docs/DECISIONS.md` («Idempotent kill protocol») и `docs/03_DOMAIN_MODEL.md`
  (AuditLog.Reason, 409-контракт, новый binding «Manual session kill»).
- **Тесты:** +3 теста — (1) `AlreadyGone` → аудит пишется + `204`; (2) kill failed
  (оба флага `false`) → аудит **не** пишется + `502 CLUSTER_UNAVAILABLE`; (3) stale-дескриптор
  → kill не вызывается, аудит не пишется, `409 SESSION_STALE`. Существующие тесты дополнены
  стабом re-fetch. Прогон: `dotnet test … --filter "Category!=Smoke"` — 188 passed, 0 failed.

### MLC-001 — Защита от параллельного запуска цикла согласования (over-kill) — 2026-06-01
- **Что сделано:** На метод интерфейса `IReconciliationJob.RunColdAsync` навешен
  `[DisableConcurrentExecution(timeoutInSeconds: 180)]`. Job зарегистрирован через
  интерфейс (`RecurringJob.AddOrUpdate<IReconciliationJob>`), поэтому Hangfire берёт
  серверный фильтр именно с метода интерфейса — атрибут размещён там, а не на реализации
  в Infrastructure. Распределённый лок (Hangfire SQL-storage) гарантирует, что
  одновременно исполняется только один enforcement-цикл; перекрывающий минутный тик ждёт
  освобождения лока (до 180с — с запасом перекрывает worst-case цикл под таймаутами
  `rac.exe`) и затем отсекается in-memory `ColdThrottleState`. Двойной `EnforceAsync`
  невозможен ни параллельно, ни вплотную. Закрывает binding-требование
  `02_ARCHITECTURE_REQUIREMENTS.md` про «only one enforcement loop at a time».
- **Зависимости:** Application получил `PackageReference Hangfire.Core` (только атрибут
  job-контракта; без server/storage). Транзитивная `Newtonsoft.Json`, которую тянет
  Hangfire.Core (11.0.1, GHSA-5crp-9r3c-p9vr), поднята до 13.0.3 через transitive pin в
  `Directory.Packages.props`. `DriftCheckJob.RunAllAsync` / `AuditRetentionJob.RunAsync`
  (опциональные в Recommendation) **не трогали** — overlap там безопасен; вынесено за
  рамки задачи.
- **Файлы:** `backend/src/MitLicenseCenter.Application/Jobs/IReconciliationJob.cs`;
  `backend/src/MitLicenseCenter.Application/MitLicenseCenter.Application.csproj`;
  `backend/Directory.Packages.props`;
  `backend/tests/MitLicenseCenter.Tests.Unit/Jobs/ReconciliationJobConcurrencyGuardTests.cs`.
- **Тесты:** Два теста: (1) контракт — атрибут присутствует на зарегистрированном методе;
  (2) поведение — реальный фильтр, снятый с метода интерфейса, при re-entrant вызове
  бросает `DistributedLockTimeoutException`, пока первый цикл держит лок, → суррогат
  `EnforceAsync` не исполняется второй раз. Прогон: `dotnet test … --filter
  "Category!=Smoke"` — 185 passed, 0 failed.

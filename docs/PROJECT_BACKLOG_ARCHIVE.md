# MitLicense Center — Backlog Archive (закрытые задачи)

> **Это архив, read-only, справочный.** Активный реестр задач — `docs/PROJECT_BACKLOG.md`
> (его читают первым при каждом запуске работы). Здесь хранятся **полные** исходные
> постановки и отчёты по уже закрытым задачам MLC-001..019 (аудит 2026-06-01..03) и
> далее (MLC-020+ — анализ техдолга от 2026-06-03), чтобы не раздувать активный файл. Сюда заглядывают только за деталями конкретной закрытой
> задачи; для выбора следующей работы этот файл читать не нужно.
>
> Снимок сделан 2026-06-03 при выносе истории из активного бэклога. Процесс работы и
> формат записи — в активном `docs/PROJECT_BACKLOG.md` (источник правды); здесь не
> дублируются, чтобы не было расходящихся копий.

Канон проекта (`docs/01..06 + DECISIONS.md + ROADMAP.md + OPERATIONS.md`) — источник
правды по архитектуре v1. Бэклог не дублирует канон, а фиксирует расхождения,
дефекты и улучшения поверх него.


## Индекс закрытых задач (оглавление; перенесён из реестра 2026-06-10)

- `MLC-001` — Защита от параллельного цикла согласования (over-kill) — Done (2026-06-01)
- `MLC-002` — Ручной kill: аудит только при реальном завершении + сверка дескриптора — Done (2026-06-01)
- `MLC-003` — Fail-fast старт: миграции/сидинг синхронно до приёма трафика (ADR-18) — Done (2026-06-01)
- `MLC-004` — Глобальный ProblemDetails + backstop гонок уникальности → 409 (ADR-19) — Done (2026-06-02)
- `MLC-005` — [Doc divergence] ADR-14: ручной деплой без несуществующего скрипта — Done (2026-06-02)
- `MLC-006` — [Doc divergence] Рукописные TS-типы зафиксированы как осознанный выбор (ADR-10.1) — Done (2026-06-02)
- `MLC-007` — Frontend-тесты: ProtectedRoute, CRUD-мутации, маппинг 409 — Done (2026-06-02)
- `MLC-008` — Контрактные тесты persistence-инвариантов на SQLite — Done (2026-06-02)
- `MLC-009` — Санитизация инфраструктурных исключений в discovery/reconcile — Done (2026-06-02)
- `MLC-010` — SettingsSnapshot: bulk-загрузка вне лока + single-flight — Done (2026-06-02)
- `MLC-011` — Vertical-slice data access зафиксирован как осознанный выбор (ADR-20) — Done (2026-06-02)
- `MLC-012` — Прод-хардненинг транспорта за конфиг-флагами (ADR-22) — Done (2026-06-02)
- `MLC-013` — Принятый риск: пароль кластера в cmdline rac.exe (ADR-21) — Done (2026-06-02)
- `MLC-014` — FE: единый ConflictBody + readConflictBody — Done (2026-06-02)
- `MLC-015` — FE: серверная пагинация списков + точечная проверка занятости кластер-базы — Done (2026-06-02)
- `MLC-016` — FE: точечная Zod-валидация на 3 критичных границах — Done (2026-06-03)
- `MLC-017` — FE: i18n-чистый 401-редирект через router — Done (2026-06-02)
- `MLC-018` — FE: code-splitting маршрутов (React.lazy) + разбиение вендоров — Done (2026-06-03)
- `MLC-019` — Dev tooling: build.ps1 устойчив к stderr нативных шагов + шаг pnpm test — Done (2026-06-03)
- `MLC-020` — Дедуп расчёта потребления лицензий → доменный калькулятор `LicenseConsumption` — Done (2026-06-03)
- `MLC-021` — Web-хелперы: uniqueness-backstop + каталог описаний аудита + резолв initiator — Done (2026-06-03)
- `MLC-022` — Единый источник правил валидации Infobase/Publication (BE `InfobaseValidationRules` + FE `validation.ts` + parity-тесты) — Done (2026-06-03)
- `MLC-023` — FE: декомпозиция InfobaseFormDialog (`useInfobaseForm` + `PublicationFieldset` + `mapConflictToField`), поведение 1:1 — Done (2026-06-03)
- `MLC-024` — App-id whitelist лицензий → `dbo.Settings` (`OneC.LicenseConsumingAppIds`, хелпер `LicenseConsumingAppIds` + UI-поле), правка без редеплоя — Done (2026-06-03)
- `MLC-029` (REF-01) — Дедуп маппинга `Publication` request→entity (`ApplyPublicationFields`) — Done (2026-06-03)
- `MLC-030` (REF-02) — Архитектурные guard-тесты границ слоёв (NetArchTest) — Done (2026-06-03)
- `MLC-031` (REF-03) — Фабрика CRUD-mutation хуков `useInvalidatingMutation` — Done (2026-06-03)
- `MLC-032` (REF-04) — Декомпозиция FE-страниц Audit/Publications/Sessions — Done (2026-06-04)
- `MLC-033` (REF-05) — Обобщённый conflict→descriptor маппер + `toastFormSubmitError` — Done (2026-06-04)
- `MLC-034` (REF-06) — Web-аудит-фасад `HttpContext.AuditAsync` — Done (2026-06-04)
- `MLC-035` (REF-07) — Группировка `Web/Endpoints` по подпапкам-фичам — Done (2026-06-04)
- `MLC-037` (PERF-01) — Метрики горячего пути: спавны `rac.exe` + циклы (Meter) — Done (2026-06-04)
- `MLC-038` (PERF-02) — Опт-ин профиль EF-запросов + baseline — Done (2026-06-05)
- `MLC-039` (PERF-03) — Нагрузочный seed-харнесс (PerfHarness) — Done (2026-06-04)
- `MLC-040` (PERF-04) — Readiness-проба `/api/v1/health/ready` — Done (2026-06-05)
- `MLC-041` (PERF-05) — Кросс-вызовный кэш UUID кластера — Done (2026-06-05)
- `MLC-042` (PERF-06) — Составной индекс `AuditLogs` под фильтр+сортировку — Done (2026-06-05)
- `MLC-043` (PERF-07) — Батч-загрузка публикаций в `DriftCheckJob` (N+1→1) — Done (2026-06-05)
- `MLC-044` — Hot-тир enforce'ит (near-realtime kill ≤5с) + быстрый экран — Done (2026-06-05)
- `MLC-045` — Публикации через webinst + смена платформы правкой `web.config` (ADR-4) — Done (2026-06-05)
- `MLC-046` — Публикации: массовые операции (bulk publish / bulk change-platform) — Done (2026-06-05)
- `MLC-047` — Управление жизненным циклом IIS из панели (ADR-24) — Done (2026-06-06)
- `MLC-048` — Сбор time-series использования лицензий (ADR-25) — Done (2026-06-06)
- `MLC-049` — Reports API: сводка + drill-down — Done (2026-06-06)
- `MLC-050` — FE-раздел «Отчёты» `/reports` (первый график recharts) — Done (2026-06-06)
- `MLC-051` — Экспорт отчётов: каркас + CSV + XLSX — Done (2026-06-07)
- `MLC-052` — Экспорт отчётов: HTML (интерактивный) + PDF — Done (2026-06-07)
- `MLC-053` — dev/ops-утилита сброса пароля администратора `reset-admin` — Done (2026-06-07)
- `MLC-054` — Полировка `/reports`: плашка обрезки + помесячный выбор — Done (2026-06-07)
- `MLC-055` — Переработка `/settings`: секции, retention, порт RAS, пикер платформы — Done (2026-06-07)
- `MLC-056` — SQL-instance discovery (localhost) + пикер сервера БД — Done (2026-06-08)
- `MLC-057` — Переключатель темы (light/dark/system) — Done (2026-06-08)
- `MLC-058` — Раздел «Администраторы»: API `/admins` + UI — Done (2026-06-08)
- `MLC-059` — Форс-смена пароля при первом входе + последний вход — Done (2026-06-08)
- `MLC-060` — Переименование «Администраторы»→«Пользователи» (роуты/API/слоты аудита) — Done (2026-06-08)
- `MLC-061` — Смена роли учётки Admin↔Viewer (+слот аудита 107) — Done (2026-06-08)
- `MLC-063` — Разведка перф-трека: rac perf-поля, DMV, атрибуция — Done (2026-06-08)
- `MLC-064` — Host-проба `IHostMetricsProbe` на WMI + `/performance/host` (ADR-26) — Done (2026-06-08)
- `MLC-064a` — Честный сигнал недоступных процессов host-пробы + FE-баннер — Done (2026-06-08)
- `MLC-065` — FE-каркас `/performance`: гейджи сатурации + атрибуция по семьям — Done (2026-06-08)
- `MLC-066` — 1С-сеансы: `ListSessionLoadsAsync`/`ListProcessesAsync` + `/performance/onec-sessions` — Done (2026-06-08)
- `MLC-067` — FE-секция «кто грузит внутри 1С» — Done (2026-06-08)
- `MLC-068` — SQL DMV-проба `ISqlPerformanceProbe` + `/performance/sql` — Done (2026-06-09)
- `MLC-069` — FE-секция «1С грузит SQL?» — Done (2026-06-09)
- `MLC-070` — Запись по требованию: backend (`PerfRecording*` + API) — Done (2026-06-09)
- `MLC-071` — Запись по требованию: UI (график + топ-виновники + экспорт) — Done (2026-06-09)
- `MLC-072` — Диагностика корректности метрик «Быстродействия»: числа корректны, баг не подтверждён — Done (2026-06-09)
- `MLC-074` — Retention-джобы под execution strategy (retriable-батчи) — Done (2026-06-09)
- `MLC-075` — ADR-15 пересмотрен + ADR-27 «Бэкапы SQL» — Done (2026-06-09)
- `MLC-076` — Бэкапы: backend-фундамент (`ISqlBackupService`, COPY_ONLY, keep-latest) — Done (2026-06-09)
- `MLC-077` — Бэкапы: оркестрация (очередь + потолок + замок) + `/api/v1/backups` — Done (2026-06-10)
- `MLC-078` — Бэкапы: frontend + live e2e — трек завершён 4/4 — Done (2026-06-10)

## Процесс и формат записи

Описаны в активном `docs/PROJECT_BACKLOG.md` (источник правды) и здесь не дублируются —
этот файл хранит только исходные постановки и отчёты по закрытым задачам.

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
- **Status:** **Done** (2026-06-02) — см. «Выполненные работы».

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
- **Status:** **Done** (2026-06-02, осознанный выбор → **ADR-20**) — см. «Выполненные работы».
  Рефакторинг в Application use-cases вынесен как открытая опция `MLC-011(a)`.

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
- **Status:** **Done** (2026-06-02) — см. «Выполненные работы».

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
- **Status:** **Done** (2026-06-02, принятый риск → **ADR-21** + `04_INFRASTRUCTURE.md` §1) —
  см. «Выполненные работы».

### MLC-014 — Frontend: интерфейс ConflictBody продублирован в ~5 диалогах
- **Category:** Maintainability / Frontend
- **Priority:** P3 · **Severity:** Low
- **Module:** Frontend
- **File(s):** `frontend/src/features/infobases/{InfobaseFormDialog,ReassignInfobaseDialog}.tsx`; `frontend/src/features/tenants/{TenantFormDialog,DeleteTenantDialog}.tsx`
- **Description:** Тип `ConflictBody` (форма 409-ответа) переопределяется в каждом
  диалоге вместо единого определения.
- **Recommendation:** Вынести `ConflictBody` в `lib/api.ts` или общие типы, переиспользовать.
- **Status:** **Done** (2026-06-02) — см. «Выполненные работы».

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
- **Status:** **Done** (2026-06-02) — см. «Выполненные работы».

### MLC-016 — Frontend: рукописные типы API без runtime-валидации (риск расхождения)
- **Category:** Maintainability / Frontend
- **Priority:** P3 · **Severity:** Low · _(связано с MLC-006)_
- **Module:** Frontend
- **File(s):** `frontend/src/lib/api.ts` (`payload as T`); `frontend/src/features/*/types.ts`
- **Description:** `api<T>()` приводит ответ к `T` без runtime-проверки; типы рукописные.
- **Recommendation:** Codegen из OpenAPI или Zod-схемы на границе ответа.
- **Status:** **Done** (2026-06-03, вариант **A** точечно — Zod-схемы на 3 критичных
  границах) — см. «Выполненные работы». Codegen из OpenAPI остаётся открытой опцией
  `MLC-006(a)`.

### MLC-017 — Frontend: захардкоженная строка «Не авторизован» в api.ts и 401-redirect через window.location.assign
- **Category:** Frontend (i18n)
- **Priority:** P3 · **Severity:** Low
- **Module:** Frontend
- **File(s):** `frontend/src/lib/api.ts` (текст ошибки 401); `frontend/src/App.tsx` (`window.location.assign("/login")`)
- **Description:** Текст ошибки 401 — строковый литерал, не i18n-ключ (на экране он,
  впрочем, маппится в `errors.*` на LoginPage); редирект на `/login` идёт мимо React
  Router.
- **Recommendation:** Бросать код ошибки без литерала; редирект — через router-навигацию.
- **Status:** **Done** (2026-06-02) — см. «Выполненные работы».

### MLC-018 — Frontend: единый бандл ~829 kB, нет code-splitting по маршрутам → предупреждение билда
- **Category:** Performance / Frontend
- **Priority:** P3 · **Severity:** Low
- **Module:** Frontend
- **File(s):** `frontend/src/routes/router.tsx`; `frontend/src/App.tsx`; `frontend/vite.config.ts`
- **Description:** `pnpm build` (vite v8/rolldown) предупреждает «Some chunks are larger
  than 500 kB» — единый клиентский бандл ~829 kB (gzip ~244 kB), нет code-splitting по
  маршрутам (все страницы импортируются статически).
- **Impact:** дольше первичная загрузка; для LAN-панели админа некритично, но это лишний
  вес и предупреждение в каждом билде.
- **Recommendation:** лениво грузить страницы маршрутов (React.lazy + Suspense) и/или
  настроить ручной chunking; цель — убрать предупреждение осмысленно, а не поднятием
  лимита вслепую.
- **Status:** **Done** (2026-06-03) — см. «Выполненные работы».

### MLC-019 — build.ps1 на Windows PS 5.1: stderr нативной команды → терминирующая ошибка + нет шага pnpm test
- **Category:** Maintainability (tooling)
- **Priority:** P3 · **Severity:** Low
- **Module:** Scripts / dev tooling
- **File(s):** `scripts/build.ps1` (и потенциально другие `scripts/*.ps1` с тем же паттерном)
- **Description:** Гипотеза аудита: под Windows PowerShell 5.1 (`powershell.exe` —
  задокументированная dev-ОС) `build.ps1` обрывается на фронтовых pnpm-шагах, потому что
  pnpm печатает баннер команды (`$ eslint .`) в stderr, а PS 5.1 при
  `$ErrorActionPreference='Stop'` превращает любой stderr-вывод нативной команды в
  терминирующую ошибку (`NativeCommandError`) — ещё до реального exit-кода (eslint при этом
  проходит с кодом 0). CI это не ловит (frontend там на ubuntu/bash). Плюс `build.ps1` **не**
  запускает `pnpm test` — фронтовые тесты не входят в «полную проверку».
- **Impact:** Если бы воспроизводилось — канонический скрипт «здоров ли проект?» давал бы
  ложный «красный» на Windows; и в любом случае не покрывает фронт-тесты.
- **Recommendation:** Успех нативного шага определять ТОЛЬКО по `$LASTEXITCODE`, а запись в
  stderr не должна терминировать; добавить шаг `pnpm test`.
- **Status:** **Done** (2026-06-03) — частично. NativeCommandError-дефект **без перенаправления
  не воспроизводится** (эффект только под `2>&1`/`*>&1`, чего сам `build.ps1` не делает) →
  этот аспект переоценён как не-дефект (Rejected). Реальные под-пункты закрыты: скрипт
  защитно укреплён (успех нативного шага определяется только по `$LASTEXITCODE`) и добавлен
  шаг `pnpm test`. См. «Выполненные работы».

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
   `MLC-010` → **Done**: hot-path-кэш `SettingsSnapshot` читает БД через bulk
   `ISettingsStore.GetAllAsync` вне лока, single-flight; прогретый путь lock-light без БД.
   `MLC-011` → **Done** (docs-only): вертикальные срезы в Web зафиксированы как осознанный
   выбор в **ADR-20**; рефакторинг в Application use-cases — открытая опция `MLC-011(a)`.
   `MLC-013` → **Done** (docs-only): пароль кластера в cmdline `rac.exe` зафиксирован как
   принятый риск в **ADR-21** + `04_INFRASTRUCTURE.md` §1.
   `MLC-012` → **Done**: прод-хардненинг транспорта за конфиг-флагами (**ADR-22**) —
   HTTPS-redirect/HSTS за `Security:EnforceHttps` (dev/за-прокси не ломаются),
   `Encrypt=True` в `appsettings.Production.json`, гейт Swagger `Security:EnableSwagger`,
   суженный `AllowedHosts`.
   `MLC-014` → **Done**: единый `ConflictBody` + хелпер `readConflictBody` экспортируются
   из `lib/api.ts`; четыре диалога переключены на них, локальные переопределения убраны.
   `MLC-017` → **Done**: `ApiError` для 401 больше не несёт русский литерал «Не авторизован»
   (текст экрана — из i18n по `status===401`, как на LoginPage); `onUnauthorized`-редирект
   переведён с `window.location.assign` (полная перезагрузка) на SPA-навигацию через
   router-инстанс с явной `queryClient.clear()` (перезагрузка раньше неявно чистила кэш).
   `MLC-015` → **Done**: серверная пагинация в UI списков клиентов/баз (`{items,total,page,
   pageSize}` + общий `PaginationBar` поверх `ui/pagination`, page size 25, фильтр/группировка
   сохранены) и точечная проверка занятости кластер-базы новым admin-эндпоинтом
   `GET /api/v1/infobases/cluster-id-availability` вместо выгрузки всех баз в `InfobaseFormDialog`.
   `MLC-016` → **Done** (вариант **A** точечно): Zod-валидация введена только на 3 критичных
   границах (`/auth/me`+`/auth/login` — роли/гейтинг; `/sessions/snapshot`; пагинированные
   списки tenants+infobases через фабрику `pagedResponseSchema`), типы выведены из схем
   (`z.infer`), `api<T>()` получил опциональный `schema` + управляемую `ApiSchemaError`. Zod
   на каждом эндпоинте сознательно НЕ вводился (нет нового toolchain — `zod` уже в зависимостях).
   Codegen из OpenAPI остаётся открытой опцией `MLC-006(a)`.
   `MLC-018` → **Done**: страницы маршрутов лениво грузятся (`React.lazy` + общий `<Suspense>`
   с фолбэком-скелетоном в `AppShell`), вендоры разбиты на `react-vendor`/`vendor` через
   `build.rolldownOptions.output.codeSplitting`. Единый бандл ~829 kB → крупнейший чанк
   `vendor` ~392 kB (gzip ~117 kB); предупреждение «chunks larger than 500 kB» ушло без
   подъёма лимита.
   `MLC-019` → **Done** (частично): защитное укрепление dev-скриптов. `Invoke-Step` в
   `build.ps1` (и шаг `dotnet ef` в `db-reset.ps1`) считают шаг успешным **только** по
   `$LASTEXITCODE`, локально снимая `Stop` вокруг нативного вызова, поэтому stderr-баннер
   инструмента под PS 5.1 (pnpm `$ eslint .`) не даёт ложный «красный» даже при захвате лога
   (`*>&1`/`Tee-Object`); в `build.ps1` добавлен шаг `pnpm test` — фронт-тесты теперь часть
   «полной проверки». Заголовочный NativeCommandError-обрыв **без** перенаправления **не
   воспроизвёлся** (эффект только под `2>&1`/`*>&1`, чего build.ps1 не делает) → переоценён
   как не-дефект.

---

## NEXT TASK

> **Нет открытых задач MLC-001..019.** Все P1/P2/P3 закрыты. Остаются только осознанно
> отложенные **открытые опции** (не дефекты, объём = новая работа, выбирать по появлению
> триггера, не по умолчанию):
> - `MLC-006(a)` — внедрить OpenAPI-codegen TS-клиента из `/api/docs/v1/swagger.json`
>   (триггер: рост API-поверхности / частые расхождения, которых рукописные типы + точечный
>   Zod уже не ловят).
> - `MLC-011(a)` — вынести бизнес-правила из Web-эндпоинтов в Application use-cases (триггер:
>   появление второго потребителя правил — второй транспорт/worker вне HTTP; см. ADR-20).
>
> При следующем запуске: если новых дефектов нет — подтвердить, что бэклог пуст, и при
> необходимости провести свежий аудит для пополнения реестра.

---

## Выполненные работы (Done)

### MLC-019 — Dev tooling: build.ps1 устойчив к stderr нативных шагов + шаг pnpm test — 2026-06-03
- **Воспроизведение (СНАЧАЛА, прямым запуском, без перенаправления):** под `powershell.exe`
  (Windows PowerShell **5.1.26100.8521**) гипотеза «`build.ps1` обрывается на pnpm-шагах
  из-за NativeCommandError» **не подтвердилась**. Установлено: (1) pnpm печатает баннер
  команды (`$ eslint .`) именно в **stderr** (проверено разделением потоков); (2) под
  `$ErrorActionPreference='Stop'` нативный stderr+exit0 **без** перенаправления **не**
  становится терминирующей ошибкой — проверено и инлайн, и дочерним `powershell.exe`, и в
  **реальном окне консоли** (`Start-Process`, без редиректа потоков): harness повторяющий
  `Invoke-Step` вокруг `pnpm lint` доходит до конца с `exit 0`; (3) NativeCommandError
  возникает **только при явном перенаправлении** (`2>&1`/`2>file`/`*>&1`), чего сам
  `build.ps1` не делает. Вывод: заголовочный дефект (ложный «красный» при прямом запуске)
  **не воспроизводится** → переоценён как не-дефект (аудитор, вероятно, гонял build с
  захватом лога — `*>&1`/`Tee-Object`, что и усиливает эффект).
- **Решение (по согласованию с оператором — AskUserQuestion):** не «выдумывать» фикс под
  невоспроизводимый сценарий, но **защитно укрепить** скрипты под общий PS-5.1-footgun (он
  реально срабатывает при захвате лога) **и** закрыть отдельный, фактически верный
  под-пункт — отсутствие `pnpm test`.
- **Что сделано (`build.ps1`):** (1) `Invoke-Step` вокруг `& $Action` локально снимает
  `Stop` (`$ErrorActionPreference='Continue'` в `try/finally` с восстановлением), поэтому
  запись нативной команды в stderr перестаёт терминировать; успех/провал шага определяется
  **только** существующей проверкой `$LASTEXITCODE -ne 0` — реальные ненулевые коды
  по-прежнему валят сборку (ложь-успех не появляется). (2) Добавлен шаг
  `Invoke-Step "Frontend · pnpm test" { pnpm test }` (между type-check и build) — фронт-тесты
  теперь часть «полной проверки». (3) Обновлён `.SYNOPSIS` (frontend `…/test/build`).
- **Тот же паттерн в других скриптах:** `dev.ps1` — нет (`$LASTEXITCODE`-проверяемых нативных
  шагов под `Stop` нет, запускает окна через `Start-Process`) → не трогали. `db-reset.ps1` —
  есть один такой шаг (`dotnet ef database update` + проверка `$LASTEXITCODE`); дёшево
  выровнен тем же приёмом (локальный `Continue` вокруг вызова). Кодировка всех файлов
  сохранена **UTF-8 с BOM** (см. lessons); поведение в pwsh 7 не меняется (там footgun'а нет,
  а `$LASTEXITCODE`-семантика та же).
- **Канон:** `DECISIONS.md` → «Tooling Constraints» / «Dev scripts» обновлено present-tense:
  `build.ps1` теперь включает фронт-тесты (`lint/type-check/test/build`) и фиксирует, что
  успех нативного шага определяется только по `$LASTEXITCODE` (stderr-баннер инструмента под
  PS 5.1 не даёт ложный провал даже при `*>&1`/`Tee-Object`); `db-reset.ps1` защищает свой
  `dotnet ef` так же.
- **Файлы:** `scripts/build.ps1` (`Invoke-Step` + шаг `pnpm test` + `.SYNOPSIS`);
  `scripts/db-reset.ps1` (защита вокруг `dotnet ef`); канон `docs/DECISIONS.md` (Dev scripts).
- **Проверка (шаг 5):** (a) точечный тест семантики нового `Invoke-Step` в PS 5.1: нативный
  stderr+exit0 **под `2>&1`** — больше **не** бросает (footgun нейтрализован); `exit 3` и
  «stderr+`exit 5`» — по-прежнему бросают (реальный код валит шаг). (b) Полный
  `powershell.exe -File scripts\build.ps1` **под `*>&1 | Tee-Object`** (самый жёсткий случай) —
  проходит до конца, **exit 0** (~29с): backend build 0/0, `dotnet test` **236 passed**,
  format ок; frontend install/lint (баннер `$ eslint .` теперь нетерминирующий шум) /
  type-check / **pnpm test 78 passed (16 файлов)** / build (без предупреждения о размере) →
  «Все шаги пройдены успешно.».

### MLC-018 — FE: code-splitting маршрутов (React.lazy) + разбиение вендоров — 2026-06-03
- **Проблема:** `pnpm build` (vite v8/rolldown) выдавал «Some chunks are larger than 500 kB»
  — единый клиентский бандл **829.44 kB** (gzip 244.43 kB), все страницы импортировались
  статически в `routes/router.tsx`.
- **Что сделано (lazy-границы):** Все десять страниц маршрутов переведены на `React.lazy`
  (`LoginPage`, `DashboardPage`, `ProfilePage`, `TenantsPage`, `TenantDetailPage`,
  `InfobasesPage`, `PublicationsPage`, `SessionsPage`, `AuditPage`, `SettingsPage`).
  Компоненты — именованные экспорты, поэтому маппятся в `{ default: m.XxxPage }`. Каркас
  (`AppShell`, `ProtectedRoute`, сам роутер) остаётся в главном чанке — нужен на каждом
  маршруте. **ProtectedRoute-гейтинг и пути не тронуты** (включая `requireAdmin` на
  `/settings` — `<Suspense>` ловит ленивый `SettingsPage` уже под гардом).
- **Что сделано (Suspense):** Общая граница `<Suspense fallback={<PageFallback />}>` вокруг
  `<Outlet/>` в `AppShell` (каркас — сайдбар/топбар — остаётся на месте, пока грузится чанк
  страницы) + отдельный `<Suspense>` вокруг ленивого `LoginPage` в роутере (он вне
  `AppShell`). Новый `components/PageFallback.tsx` — короткий скелетон поверх существующего
  `ui/skeleton` (без i18n: это мгновенный лоадер чанка, не пользовательский текст — как
  оговорено в задаче).
- **Что сделано (chunking):** Только lazy недостаточно — оставался крупный вендорный остаток
  **638 kB** (React/react-dom/router/query/radix и пр. сходятся в общий чанк). Добавлено
  `build.rolldownOptions.output.codeSplitting.groups` (vite 8 / rolldown — именно этот API
  указывает само предупреждение): группа `react-vendor` (`react`/`react-dom`/`react-router`/
  `scheduler`, priority 20) и общий `vendor` (`node_modules`, priority 10). Лимит
  `chunkSizeWarningLimit` **не поднимался** — разбиение осмысленное.
- **Расчёт:** Итог — крупнейшие чанки `vendor` **391.55 kB** (gzip 117.18 kB) и
  `react-vendor` **284.69 kB** (gzip 90.49 kB), главный app-чанк `index` 52.69 kB, каждая
  страница — отдельный чанк 2–13 kB. **Предупреждение о размере ушло.**
- **Lint-нюанс:** `lazy()`-консты в `router.tsx` правило `react-refresh/only-export-components`
  (`warn`) принимает за определения компонентов рядом с не-компонентным экспортом `router`
  → 10 предупреждений. `router.tsx` — модуль конфигурации маршрутов, не HMR-граница
  компонента, поэтому добавлен file-level `eslint-disable` этого правила с пояснением
  (в духе уже существующего override для тестовых файлов в `eslint.config.js`).
- **Наблюдение (не входит в задачу):** `recharts` объявлен в зависимостях (ADR-11, под
  будущие графики), но **нигде не импортируется** — дашборд использует `ui/progress`.
  Поэтому отдельная группа под recharts не заводилась (была бы мёртвым правилом). Если
  графики появятся — вынести их в свою lazy-группу.
- **Канон:** не трогали — наблюдаемое поведение (маршруты, гейтинг, экраны) идентично;
  ленивая загрузка + разбиение чанков — деталь сборки. ADR не требуется (подтверждено
  условием задачи).
- **Файлы:** `frontend/src/routes/router.tsx` (React.lazy + Suspense вокруг LoginPage +
  eslint-disable); `frontend/src/components/layout/AppShell.tsx` (Suspense вокруг Outlet);
  `frontend/src/components/PageFallback.tsx` (новый); `frontend/vite.config.ts`
  (`rolldownOptions.output.codeSplitting`).
- **Прогон (в `frontend/`):** `pnpm lint` — 0 проблем; `pnpm type-check` — 0; `pnpm test` —
  **78 passed (16 файлов)** (без изменений — поведение то же); `pnpm build` — **без
  предупреждения о размере**. Дополнительно проверено в браузере (preview): `/` под
  неавторизованной сессией редиректит на `/login`, ленивый `LoginPage` монтируется через
  Suspense, консоль чистая (0 warn/error).

### MLC-016 — FE: точечная Zod runtime-валидация на критичных границах ответа — 2026-06-03
- **Решение (вариант A точечно, с учётом ADR-10.1):** не codegen и не Zod-на-всё, а
  **лёгкая runtime-валидация только на 2-3 самых критичных границах**. Ключевой фактор ROI,
  меняющий вердикт «Rejected» из ADR-10.1: `zod` v4 **уже прямая зависимость** фронта (идёт
  в паре с `react-hook-form`, ADR-11), а `vitest` настроен — то есть точечная валидация **не
  тянет новый toolchain** (возражение ADR-10.1 относилось к Zod *на каждой* границе + лишней
  оснастке). Вариант B (отклонить/отложить) отвергнут именно из-за этого: стоимость околонулевая,
  а выигрыш на ролевой границе — реальный (безопасность).
- **Где введена валидация (3 критичные границы, не везде):** (1) **`GET /api/v1/auth/me` +
  `POST /api/v1/auth/login`** — `CurrentUser.roles` управляет ролевым гейтингом `ProtectedRoute`;
  тихое расхождение тут = ошибка авторизации (fail-open/closed) → наивысшая ценность. (2)
  **`GET /api/v1/sessions/snapshot`** — данные снимка (`consumesLicense`, `durationSeconds`,
  дескрипторы) питают операционную картину over-limit/kill. (3) **Пагинированные списки**
  `{items,total,page,pageSize}` для tenants и infobases — через generic-фабрику
  `pagedResponseSchema(item)` (одна схема конверта покрывает оба эндпоинта). Остальные
  эндпоинты сознательно оставлены на прежнем `payload as T`.
- **Единый источник правды:** схемы живут рядом с типами фичей (`features/<feature>/types.ts`),
  типы выводятся из схем через `z.infer` — нет двойного определения. `features/sessions/types.ts`,
  `features/tenants/types.ts`, `features/infobases/types.ts` переведены с рукописных интерфейсов
  на `z.infer` (структурно идентичны — потребители не тронуты); новый `features/auth/types.ts`
  (раньше `CurrentUser` был инлайном в `useAuth.ts`; re-export из `useAuth` сохранён для
  существующих импортов). Тела **запросов** (`*Input`), `InfobaseDetail`, `ClusterIdAvailability`
  оставлены рукописными — валидируется только ответ на критичной границе.
- **Механизм (api.ts):** `api<T>()` сохранён; `RequestOptions<T>` получил опциональный
  `schema?: ResponseSchema<T>` (узкий структурный интерфейс `{ parse(data:unknown):T }` —
  `lib/api` намеренно НЕ импортирует zod, схемы приходят только через него). При наличии схемы
  на **успешном (2xx)** ответе вместо `payload as T` выполняется `schema.parse`; провал →
  **управляемая `ApiSchemaError`** (несёт `path` + исходные `issues`), а не «тихий» неверный
  тип. Ветка ошибок (`!response.ok`) и 401-хендлер не тронуты — там по-прежнему `ApiError`.
  Generic-фабрика конверта вынесена в `lib/apiSchema.ts` (изолирует zod от zod-агностичного
  `lib/api.ts`).
- **Канон:** `DECISIONS.md` ADR-10.1 — добавлен **Update (MLC-016)**: точечная валидация как
  осознанное сужение «no runtime validation» (zod уже в deps → нет нового toolchain; перечень
  границ; `z.infer`-источник правды; что сознательно НЕ сделано — Zod-на-всё и codegen
  `MLC-006(a)`). `05_UI_REQUIREMENTS.md` §2 — оговорка про runtime-валидацию критичных границ
  и `ApiSchemaError`.
- **Файлы:** `frontend/src/lib/api.ts` (`ResponseSchema`, `ApiSchemaError`, `schema`-параметр);
  `frontend/src/lib/apiSchema.ts` (новый, `pagedResponseSchema`); `frontend/src/features/auth/types.ts`
  (новый); `features/auth/useAuth.ts` (схема на me/login + re-export `CurrentUser`);
  `features/sessions/types.ts` + `useSessionsSnapshot.ts`; `features/tenants/types.ts` +
  `useTenants.ts`; `features/infobases/types.ts` + `useInfobases.ts`;
  `frontend/src/lib/__tests__/api.test.ts` (+3); `frontend/src/lib/__tests__/apiSchema.test.ts`
  (новый, +7); канон `docs/DECISIONS.md` (ADR-10.1 Update), `docs/05_UI_REQUIREMENTS.md` (§2).
- **Тесты (+10):** `api()` со схемой — валидная нагрузка проходит и типизируется; искажённая
  (`roles:42`) кидает `ApiSchemaError` (не `ApiError`, `path` верный, `issues` есть); без схемы
  поведение прежнее (сырой каст без выброса). `pagedResponseSchema` — валидный конверт принят,
  кривой элемент / отсутствующий `total` отклонены. `currentUserSchema` — валидный принят, `roles`
  неверного типа отклонён. `sessionsSnapshotResponseSchema` — валидный снимок принят, кривой
  `consumesLicense` отклонён. Прогон в `frontend/`: `pnpm lint` (0), `pnpm type-check` (0),
  `pnpm test` — **78 passed (16 файлов)** (было 68/15; +10 тестов, +1 файл).

### MLC-015 — FE: серверная пагинация списков + точечная проверка занятости кластер-базы — 2026-06-02
- **Что сделано (пагинация):** Списки клиентов и инфобаз переведены с «тянем до 200 + режем
  на клиенте» на **серверную пагинацию** поверх уже существовавшего бэкенд-контракта
  `{items,total,page,pageSize}`. Хуки `useTenants(page,pageSize)` / `useInfobases(tenantId,
  page,pageSize)` принимают страницу, кладут её в queryKey (префикс `["tenants"]`/`["infobases"]`
  сохранён — мутации инвалидируют все страницы разом) и используют `placeholderData:(prev)=>prev`
  (страница не моргает скелетоном при перелистывании). Дефолтный размер страницы — 25
  (`TENANTS_PAGE_SIZE` / `INFOBASES_PAGE_SIZE`). `TenantsPage`, `InfobasesPage` и
  per-tenant `TenantDetailPage` рендерят текущую страницу `data.items`, `total` берут из
  `data.total`. Фильтр по клиенту и тумблер «По клиенту» сохранены: смена фильтра сбрасывает
  на стр. 1; группировка применяется к **текущей странице** (документировано в `06_UI_DESIGN.md`).
- **Переиспользуемые контролы:** новый `components/PaginationBar.tsx` (поверх `ui/pagination`)
  — сводка «from–to из total» + номера страниц + индикатор «Обновление…»; рендерит `null`,
  если всё помещается на одной странице. Логика окна номеров вынесена в чистую
  `lib/pagination.ts::pageLinkRange` (с unit-тестом); `AuditPage` (где серверная пагинация
  уже была) переключён на тот же общий helper — локальная копия удалена (дедуп).
- **Выпадающие списки клиентов:** где нужен полный набор (фильтры, формы, карта `id→имя`,
  `AuditPage`), добавлен `useAllTenants()` (одна большая страница, pageSize=200). Клиентов на
  порядок меньше, чем инфобаз, поэтому это приемлемо; если их станет больше предела — отдельная
  задача (искомый/пагинированный селект). `AuditPage`/`InfobasesPage`/`TenantDetailPage`
  переведены на `useAllTenants`.
- **Точечная проверка занятости кластер-базы:** `InfobaseFormDialog` больше **не** грузит все
  инфобазы (`allInfobasesQuery` убран) ради скрытия занятых баз в пикере. Вместо этого добавлен
  лёгкий admin-эндпоинт `GET /api/v1/infobases/cluster-id-availability?clusterInfobaseId=…
  [&excludeId=…]` → `{taken, takenByTenantName?}` (под `/api/v1`, версионирование как у группы).
  Форма дёргает его при выборе/вводе валидного GUID (`useClusterIdAvailability`), показывает
  «уже привязана к клиенту «…»» на поле и не делает заведомо обречённый submit. Пикер теперь
  показывает **все** базы кластера. Контракт `409 INFOBASE_ALREADY_ASSIGNED` на create/update/
  reassign + индекс `IX_Infobases_ClusterInfobaseId` остаются authoritative backstop'ом (не трогали).
- **Канон:** `03_DOMAIN_MODEL.md` — `ClusterInfobaseId` (пикер показывает все + точечная
  проверка), новые binding-контракты «List paging» и «Cluster-id availability probe»;
  `06_UI_DESIGN.md` — раздел Pagination переписан под server-side для Audit/Clients/Infobases +
  `PaginationBar` + группировка текущей страницы; `05_UI_REQUIREMENTS.md` — оговорка про
  availability-probe в форме. OpenAPI — спека генерируется в рантайме (Swagger `/api/docs`),
  отдельного committed-файла нет; эндпоинт документируется автоматически.
- **Файлы:** `backend/.../Endpoints/InfobasesEndpoints.cs` (+`ClusterIdAvailabilityAsync`,
  маршрут), `.../InfobasesContracts.cs` (+`ClusterIdAvailabilityResponse`);
  `backend/tests/.../Endpoints/ClusterIdAvailabilityTests.cs` (новый, +3);
  `frontend/src/lib/pagination.ts` (новый) + `lib/__tests__/pagination.test.ts` (новый);
  `frontend/src/components/PaginationBar.tsx` (новый); `features/tenants/useTenants.ts`
  (paged + `useAllTenants`); `features/infobases/useInfobases.ts` (paged +
  `useClusterIdAvailability`); `features/infobases/types.ts` (+`ClusterIdAvailability`);
  `features/{tenants/TenantsPage,tenants/TenantDetailPage,infobases/InfobasesPage,audit/AuditPage}.tsx`;
  `features/infobases/InfobaseFormDialog.tsx`; `features/infobases/__tests__/InfobaseFormDialog.test.tsx`
  (+1); `i18n/ru.json` (`common.pagination.*`, `infobases.errors.clusterAlreadyAssignedNamed`).
- **Тесты:** backend `dotnet test … --filter "Category!=Smoke"` — **233 passed, 0 failed**
  (было 230; +3: занятая/свободная база, исключение собственной через `excludeId`). frontend
  `pnpm lint`/`type-check` (0) + `pnpm test` — **68 passed (15 файлов)** (было 63; +4
  `pageLinkRange`, +1 точечная проверка занятости в форме).

### MLC-014 + MLC-017 — FE: единый `ConflictBody` и i18n-чистый 401-редирект через router — 2026-06-02
- **Почему вместе:** обе мелкие FE-задачи правят одни и те же файлы (`lib/api.ts` + те же
  диалоги + `App.tsx`), поэтому выполнены одним заходом.
- **MLC-014 (дедуп `ConflictBody`):** в `frontend/src/lib/api.ts` рядом с `ApiError`
  экспортированы единый `interface ConflictBody { code?: string; detail?: string }` и хелпер
  `readConflictBody(error: ApiError): ConflictBody | null` (читает `error.body`, возвращает
  `null` на пустом теле). Четыре диалога (`features/infobases/{InfobaseFormDialog,
  ReassignInfobaseDialog}.tsx`, `features/tenants/{TenantFormDialog,DeleteTenantDialog}.tsx`)
  переключены на импорт из `lib/api`, локальные `interface ConflictBody` удалены, разбор 409
  идёт через `readConflictBody(error)`. Логика обработки 409 (коды → поле формы/тост) не
  менялась — поведение сохранено, существующие тесты диалогов из MLC-007 зелёные.
- **MLC-017 (i18n-чистый 401 + router-редирект):** (1) В `api()` ветка 401 больше не бросает
  русский литерал «Не авторизован» — `throw new ApiError(401, "HTTP 401", null)`; статус 401
  несёт сам сигнал, текст для экрана берётся из i18n на месте показа (LoginPage по
  `status===401` → `auth.invalidCredentials`) — литерал нигде не отображался и до этого.
  (2) В `App.tsx` `onUnauthorized`-хендлер переведён с `window.location.assign("/login")`
  (полная перезагрузка) на SPA-навигацию через router-инстанс
  (`router.navigate("/login", { replace: true })` с гардом `router.state.location.pathname
  !== "/login"`). Поскольку SPA-навигация **не** перезагружает страницу (а прежний reload
  неявно сбрасывал кэш React Query), добавлен явный `queryClient.clear()` — сценарий
  «401 → уход на /login и очистка кэша» сохранён. Гонок/двойных редиректов нет: повторные
  401 идут на тот же `/login` с `replace` (идемпотентно), гард отсекает лишнюю навигацию,
  `clear()` повторно безвреден.
- **Канон:** не трогали — `05_UI_REQUIREMENTS.md` §2 описывает наблюдаемый контракт
  («Unauthenticated requests … the SPA redirects to the login screen»), который сохранён;
  смена механизма (reload → router-навигация) и формы внутреннего `ApiError.message` —
  деталь реализации, наблюдаемый исход тот же.
- **Файлы:** `frontend/src/lib/api.ts` (`ConflictBody` + `readConflictBody`, 401 без
  литерала); `frontend/src/App.tsx` (router-редирект + `queryClient.clear()`);
  `frontend/src/features/infobases/{InfobaseFormDialog,ReassignInfobaseDialog}.tsx`;
  `frontend/src/features/tenants/{TenantFormDialog,DeleteTenantDialog}.tsx`;
  `frontend/src/lib/__tests__/api.test.ts` (+3).
- **Тесты (+3):** `api()` — 401 даёт `ApiError` со `status===401`, `message` без кириллицы и
  не равен «Не авторизован», `onUnauthorized`-хендлер вызван; `readConflictBody` — читает
  `{code,detail}` и отдаёт `null` на пустом теле. Прогон в `frontend/`: `pnpm lint` (0),
  `pnpm type-check` (0), `pnpm test` — **63 passed (14 файлов)** (было 60).

### MLC-012 — Прод-хардненинг транспорта за конфиг-флагами (ADR-22) — 2026-06-02
- **Согласовано с оператором (AskUserQuestion):** топология может быть и за реверс-прокси,
  и без него (в основном без — проброс порта), оператор — сисадмин. Поэтому выбран
  **флаг-gated** подход, не безусловное включение: дефолты остаются dev-безопасными, прод
  ужесточается через `appsettings.Production.json`/ENV. Подтверждено: (1) `EnforceHttps`
  по умолчанию `false` + инструкция; (2) Swagger закрыт в проде по умолчанию + override-флаг.
- **Что сделано (код):** (1) **HSTS + HTTPS-redirect** добавлены в пайплайн сразу после
  `UseExceptionHandler` (до `UseAuthentication`), но **за флагом**: `app.UseHsts()` +
  `app.UseHttpsRedirection()` исполняются только когда `Security:EnforceHttps=true` **и**
  окружение не Development. Решение вынесено в чистый помощник
  `TransportSecurity.ShouldEnforceHttps(isDevelopment, config)`. Безусловно не включаем:
  single-node может стоять за терминирующим TLS-прокси, который сам делает redirect/HSTS —
  дублирование дало бы двойной редирект/петли (приложение на localhost видит уже
  расшифрованный http). (2) **Swagger UI** (`/api/docs`) обёрнут в гейт
  `TransportSecurity.ShouldEnableSwagger` = `Development || Security:EnableSwagger`: в
  Development всегда (на нём держится ручная синхронизация TS-типов — ADR-10.1), в проде
  закрыт по умолчанию, override-флаг возвращает для внутреннего admin-only периметра.
  (3) **Строка подключения**: базовый `appsettings.json` **не тронут** (`Encrypt=False` —
  локальный SQL без TLS-сертификата работает как прежде); прод-override живёт в новом
  **`appsettings.Production.json`** (шаблон) с `Encrypt=True` для `Default` и `Hangfire`
  (`TrustServerCertificate=True` оставлен — single-node SQL обычно с self-signed-сертом на
  localhost: канал шифруется, валидация цепочки пропускается; для полной валидации —
  доверенный серт + `TrustServerCertificate=False`). (4) **`AllowedHosts`** в прод-шаблоне
  сужен с `*` до `panel.example.local` (оператор подставляет реальный FQDN). В базовый
  `appsettings.json` добавлена секция `Security` с явными dev-дефолтами (`EnforceHttps:false`,
  `EnableSwagger:false`). Cookie `Secure=Always` вне Development (ADR-7) не трогали — оно
  независимо от флагов.
- **Почему помощник `TransportSecurity` (а не инлайн):** полный boot `Program` тянет SQL
  (Hangfire `SqlServerStorage` + fail-fast bootstrap-миграции), поэтому WebApplicationFactory-
  smoke потребовал бы живой SQL и сертификат. Чистые решения гейтинга вынесены в
  `internal static TransportSecurity` (виден тестам через существующий `InternalsVisibleTo`),
  что даёт SQL-free smoke по таблице истинности — в стиле проекта (тесты вызывают логику
  напрямую, без WebApplicationFactory).
- **Канон:** `OPERATIONS.md` — новый раздел «Transport hardening» (TLS на сервисе vs на
  прокси с инструкцией по `EnforceHttps`; `Encrypt=True` и нюанс `TrustServerCertificate`;
  гейт Swagger; сужение `AllowedHosts`; ENV-override `Security__*`/`ConnectionStrings__*`) +
  уточнение шага деплоя (publish теперь содержит шаблон `appsettings.Production.json` — на
  redeploy исключать из копирования, не затирать отредактированный оператором).
  `DECISIONS.md` — **ADR-22 «Transport hardening is config-gated»** (решение, отклонённые
  альтернативы: безусловный redirect/HSTS ломает dev и петлит за прокси; правка базовой
  строки на `Encrypt=True` ломает локальный SQL; удаление Swagger — он reference-контракт
  ADR-10.1; revision signal — стандартизация на TLS-на-сервисе → дефолт `EnforceHttps:true`).
- **Файлы:** `backend/src/MitLicenseCenter.Web/Program.cs` (флаг-gated HSTS/redirect +
  гейт Swagger + `using MitLicenseCenter.Web`); `backend/src/MitLicenseCenter.Web/TransportSecurity.cs`
  (новый, чистые решения); `backend/src/MitLicenseCenter.Web/appsettings.json` (секция
  `Security`); `backend/src/MitLicenseCenter.Web/appsettings.Production.json` (новый шаблон:
  `Encrypt=True`, `Security`, суженный `AllowedHosts`);
  `backend/tests/.../Web/TransportSecurityTests.cs` (новый, +6); канон `docs/OPERATIONS.md`,
  `docs/DECISIONS.md` (ADR-22).
- **Тесты (+6):** таблица истинности гейтинга на in-memory `IConfiguration` без БД:
  `EnforceHttps` — off в Development даже при флаге `true`; off в Production по умолчанию;
  on в Production при флаге; Swagger — on в Development по умолчанию; off в Production по
  умолчанию; on в Production при override-флаге. Прогон: `dotnet test … --filter
  "Category!=Smoke"` — **230 passed, 0 failed** (было 224). Сборка Release: 0 ошибок,
  0 предупреждений.

### MLC-013 — Принятый риск: пароль кластера в cmdline rac.exe (ADR-21) — 2026-06-02
- **Выбранное решение:** задокументировать как **осознанно принятый остаточный риск** —
  «исправление» нерентабельно и невозможно в рамках самого `rac.exe`. Кода не трогали.
- **Что сделано:** (1) Введён **ADR-21 — «Accepted risk — 1C cluster password on the
  `rac.exe` command line»** (`docs/DECISIONS.md`): решение (пароль идёт флагом
  `--cluster-pwd` в `BuildArgsWithAuth`, виден другим процессам/локальным админам на время
  жизни процесса), причина неизбежности (поддерживаемые версии `rac.exe` принимают пароль
  **только** через флаг — нет stdin/файла/env), компенсирующие факторы (single-node — нет
  сетевой экспозиции cmdline; admin-only хост — читатель чужой cmdline уже локальный админ
  с полным контролем кластера/MSSQL/DPAPI; DPAPI-шифрование в покое, расшифровка только при
  spawn; периметр + 2FA на edge по ADR-15), отклонённые альтернативы (stdin/файл/env —
  не поддержаны; анонимный режим — только для незащищённого кластера; long-lived RAS-socket
  Strategy B — снижает частоту spawn, но не убирает флаг) и сигнал к пересмотру (появление
  не-cmdline-канала пароля в `rac.exe` → переключиться; смена топологии single-node →
  re-review адаптеров). (2) В `04_INFRASTRUCTURE.md` §1 на bullet «Authentication» добавлена
  оговорка про принятый риск со ссылкой на ADR-21.
- **Файлы:** `docs/DECISIONS.md` (ADR-21); `docs/04_INFRASTRUCTURE.md` (§1, bullet
  Authentication). Кода/тестов не трогали.

### MLC-011 — Vertical-slice data access в Web зафиксированы как осознанный выбор (ADR-20) — 2026-06-02
- **Выбранное решение:** задокументировать вертикальные срезы как **осознанный
  архитектурный выбор**, а не рефакторить. Рефакторинг в Application use-cases вынесен
  отдельной открытой задачей `MLC-011(a)` (объём = новая работа, не правка доков). Кода не
  трогали.
- **Что сделано:** Введён **ADR-20 — «Vertical-slice data access in Web (minimal API +
  `AppDbContext`)»** (`docs/DECISIONS.md`): решение (Web-эндпоинты инжектят `AppDbContext`/
  Identity напрямую и держат валидацию/уникальность/каскад/аудит в хендлере; `Application` —
  тонкий слой интерфейсов и контрактов, без use-case-хендлеров), **что НЕ ослабляется**
  (anti-corruption-граница к 1С/IIS из `02` core-принцип #1 и ADR-5/16 — без изменений: Web
  никогда не ходит в `rac.exe`/IIS/`Microsoft.Web.Administration` напрямую; послабление —
  только доступ к **собственной** доменной БД через `AppDbContext`/Identity, где нет внешней
  системы для коррупции; главный запрет `02` цел), отклонённая альтернатива (полный onion с
  use-case-хендлером на эндпоинт — церемония без выигрыша при одном транспорте/одном авторе;
  тестовый шов уже есть — EF InMemory + SQLite-контракты MLC-008), причина (масштаб 5–20
  пользователей, один транспорт, нет переиспользования правил между транспортами → нечего
  дедуплицировать) и **условие пересмотра** (появление второго потребителя правил — второй
  транспорт gRPC/CLI/массовый импорт или worker вне HTTP → вынести правила в Application
  use-cases как `MLC-011(a)`; не Locked — промоушн супершедит ADR без церемонии отзыва).
- **Файлы:** `docs/DECISIONS.md` (ADR-20). Кода/тестов не трогали.

### MLC-010 — SettingsSnapshot: bulk-загрузка вне лока + single-flight (hot-path) — 2026-06-02
- **Что сделано:** Убран sync-over-async под локом и N запросов на обновление кэша.
  (1) В `ISettingsStore` добавлен bulk-метод `GetAllAsync(ct) →
  IReadOnlyDictionary<string,string?>`: одним `ToListAsync` тянет все строки `dbo.Settings`
  и расшифровывает секреты тем же `IDataProtector` (purpose `mlc.settings.v1`), что и
  пер-ключевой `GetAsync` — plaintext без маскировки (это hot-path для адаптеров/jobs).
  (2) `SettingsSnapshot.EnsureLoaded` переписан: прогретый кэш (TTL ≈ 30s) читается
  **без лока** через `Volatile.Read` ссылки на неизменяемый `CacheState` (словарь после
  публикации не мутируется → нет «рваных» чтений). Обращение к БД идёт **вне лока**:
  первый «холодный»/просроченный читатель под коротким локом публикует общий
  `TaskCompletionSource` и становится единственным загрузчиком, остальные ждут **тот же**
  Task — **single-flight**, поэтому конкурентные читатели не грузят дважды и не
  блокируются друг на друге во время DB-вызова. Готовый словарь подменяет `_state` под
  коротким локом (double-check по TTL на входе и по `_version` при публикации).
- **Сохранённая семантика:** TTL ≈ 30s, расшифровка секретов, потокобезопасность,
  whitelist-приоритет каталога (кэшируются строго ключи `SettingDefinitions.All`, ключ без
  строки → null). `Invalidate()` сохранён и теперь бампит `_version`, гася in-flight
  загрузку, начатую до него (её результат не публикуется — SetAsync мог записать новое
  значение в БД уже после чтения загрузчиком), → следующее чтение перезагружает свежие
  данные. Контракт `ISettingsSnapshot` (`GetString`/`GetInt`/`Invalidate`) для вызывающих
  не менялся — публичные методы остаются синхронными. Блокирующее ожидание осталось только
  на холодном чтении (интерфейс синхронный) и минимизировано: один загрузчик, один запрос.
- **Ordering-нюанс (важно):** первый вариант с `_inFlight ??= LoadAsync()` ломался на
  синхронно-завершающемся `GetAllAsync` (EF InMemory / `Task.FromResult`): `LoadAsync`
  обнулял `_inFlight` до того, как `??=` присваивал ему завершённый Task, и кэш переставал
  перезагружаться. Поэтому загрузчик отделён от ожидающих явным `TaskCompletionSource`
  (`RunLoad`), а `_inFlight` чистит только загрузчик после публикации.
- **Канон:** не трогали — это внутренняя оптимизация, наблюдаемый контракт
  `ISettingsSnapshot` и каталог Settings (`04_INFRASTRUCTURE.md`) без изменений; новый
  bulk-метод `ISettingsStore` в сигнатурах канона не описан.
- **Файлы:** `backend/src/MitLicenseCenter.Application/Settings/ISettingsStore.cs`
  (контракт `GetAllAsync`); `backend/src/MitLicenseCenter.Infrastructure/Settings/SettingsStore.cs`
  (реализация bulk-расшифровки); `backend/src/MitLicenseCenter.Infrastructure/Settings/SettingsSnapshot.cs`
  (single-flight, lock-light hot-path); `backend/tests/.../Settings/SettingsStoreEncryptionTests.cs`
  (+2); `backend/tests/.../Settings/SettingsSnapshotTests.cs` (новый, +9).
- **Тесты (+11):** store — `GetAllAsync` возвращает расшифрованный секрет + plain + null
  для незаданного, и совпадает с пер-ключевым `GetAsync`; snapshot (фейковый store с
  счётчиком загрузок + `MutableClock`) — `GetString`/`GetInt` отдают значения каталога;
  повторные чтения в пределах TTL грузят БД **один раз**; чтение за TTL перезагружает;
  чтение под TTL — нет; `Invalidate()` форсит перезагрузку и подхватывает новые значения;
  16 конкурентных холодных читателей (загрузка заблокирована внутри store) вызывают
  ровно **одну** загрузку (single-flight). Прогон: `dotnet test … --filter
  "Category!=Smoke"` — **224 passed, 0 failed** (было 213).

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

### MLC-020 — Дедуп расчёта потребления лицензий → доменный калькулятор — 2026-06-03

- **Проблема:** правило «сколько лицензий потребляет арендатор»
  (`Where(ConsumesLicense).GroupBy(TenantId).Count()`), определение over-limit и порядок
  выбора кандидатов на kill были реализованы **независимо в трёх местах**
  (`ReconciliationJob`, `KillEnforcer`, `DashboardEndpoints`). Центральная доменная логика
  продукта без единого дома: усложнение правила (веса по app-id, per-infobase лимиты,
  исключения) потребовало бы синхронной правки трёх мест, а рассинхрон в важнейшем контуре
  (дашборд показывает одно — энфорсер гасит по-другому) — тихий и дорогой.
- **Решение:** извлечён чистый доменный калькулятор
  `MitLicenseCenter.Application.Sessions.LicenseConsumption` (static, без EF/инфраструктуры,
  только LINQ по входным данным) — единый дом трёх правил. Рефакторинг **1:1**, наблюдаемое
  поведение не изменилось.
- **Проектные решения:**
  - **Дом — Application, не Domain.** Калькулятор оперирует `SnapshotSessionEntry` /
    `SnapshotPayload`, которые живут в `Application/Sessions`, а `Application → Domain` (не
    наоборот). Перенос снапшот-типов в Domain — вне рамок задачи; калькулятор остаётся
    чистым статическим хелпером в духе уже принятых в проекте.
  - **Объём — полный (3 метода)**, выбран как «единый дом» логики потребления/энфорсмента.
  - **`IsActive` остаётся на EF-границе** в `KillEnforcer` (`.Where(t => t.IsActive)`):
    калькулятор получает словарь лимитов уже только активных тенантов. Чистота калькулятора
    сохранена, БД-фильтрация — в инфраструктуре; правило `limit > 0 && consumed > limit`
    живёт в калькуляторе.
- **Публичный контракт (Application):** `static class LicenseConsumption` +
  `readonly record struct OverLimitTenant(Guid TenantId, int Consumed, int Limit)`.
  Сигнатуры:
  - `Dictionary<Guid,int> CountByTenant(IEnumerable<SnapshotSessionEntry>)` — потребление по
    тенанту; используется во **всех трёх** местах.
  - `List<OverLimitTenant> FindOverLimit(IReadOnlyDictionary<Guid,int> consumptionByTenant,
    IReadOnlyDictionary<Guid,int> activeTenantLimits)` — over-limit тенанты; порядок
    результата = порядку перечисления `consumptionByTenant` (важно для cap
    `MaxKillsPerCycle`). Используется `KillEnforcer`.
  - `List<SnapshotSessionEntry> KillCandidates(IEnumerable<SnapshotSessionEntry>, Guid
    tenantId)` — сессии тенанта с лицензией, newest-first (стабильная сортировка по
    `StartedAtUtc`). Используется `KillEnforcer`.
  Контракт отмечен здесь; в канон (`docs/`) сигнатуры не вносим — как bulk-метод в MLC-010.
  Канон не правили (внутренний рефакторинг).
- **Fidelity 1:1:** `OverLimitTenant` — позиционный `record struct` (авто-`Deconstruct`),
  поэтому существующий `foreach (var (tenantId, consumed, limit) in overLimitTenants)` в
  `KillEnforcer` работает без изменений. Hot-tier promotion в `ReconciliationJob`
  (пороговое `percent >= threshold`) и top-5 ranking в `DashboardEndpoints` (percent для
  отображения) **не трогали** — заменён только источник словаря потребления.
- **Файлы:** `backend/src/MitLicenseCenter.Application/Sessions/LicenseConsumption.cs`
  (новый); `backend/src/MitLicenseCenter.Infrastructure/Jobs/ReconciliationJob.cs`;
  `backend/src/MitLicenseCenter.Infrastructure/Jobs/KillEnforcer.cs`;
  `backend/src/MitLicenseCenter.Web/Endpoints/DashboardEndpoints.cs`;
  `backend/tests/MitLicenseCenter.Tests.Unit/Sessions/LicenseConsumptionTests.cs` (новый).
- **Тесты:** 13 чистых unit-тестов калькулятора (без EF/NSubstitute) — `CountByTenant`
  (только `ConsumesLicense`, группировка, пропуск тенанта без потребления, пустой вход),
  `FindOverLimit` (over/at/under limit, `limit<=0`, неактивный тенант вне словаря,
  сохранение порядка), `KillCandidates` (фильтр тенанта/лицензии, newest-first,
  стабильность при равных `StartedAtUtc`). Существующие job-тесты (`KillEnforcer*`,
  `ReconciliationJobConcurrencyGuard`) — зелёные без правок (регрессионный гейт 1:1).
- **Прогон:** `dotnet test --filter "Category!=Smoke"` — 249 passed, 0 failed;
  `scripts/build.ps1` — зелёный (backend build 0 warnings, FE lint/type-check/test 78
  passed, FE build).

### MLC-021 — Web-хелперы: uniqueness-backstop + каталог описаний аудита + резолв initiator — 2026-06-03

- **Проблема:** в мутирующих Web-эндпоинтах (`InfobasesEndpoints`, `TenantsEndpoints`,
  `PublicationsEndpoints`) дословно повторялся бойлерплейт трёх видов: (1) резолв initiator
  `httpContext.User.Identity?.Name ?? "unknown"` (12 вхождений; в `TenantsEndpoints` ещё и
  инлайнился повторно внутри строки описания); (2) `try { SaveChanges } catch
  (DbUpdateException) when (DbUniqueViolation.Identify…)` → `Conflict(Problems.*)` (5 блоков);
  (3) ручная сборка русских строк аудита прямо в `audit.LogAsync(...)`. Следствие
  vertical-slice (ADR-20), но дублирование живёт **внутри одного транспорта** — убирается
  тонкими Web-хелперами без use-case-слоя. Cost if ignored: M (новая аудируемая сущность =
  копирование паттерна; разрозненные строки аудита дрейфуют и непроверяемы как единица).
- **Решение:** три тонких Web-хелпера, рефакторинг строго **1:1** (контракт API не изменён —
  те же ProblemCodes/409, те же строки и состав записей аудита). Application use-case слой
  **не вводился** (`MLC-011(a)` остаётся отложенным; ADR-20 в силе).
- **Проектные решения (ответы на постановочные вопросы):**
  - **Backstop с per-call маппингом (ADR-19).** Ключевой нюанс: один индекс
    `IX_Infobases_TenantId_Name` → `NAME_DUPLICATE_IN_TENANT` на create/update, но →
    `INFOBASE_NAME_TAKEN_IN_TARGET` на reassign. Поэтому маппинг **per-call**, не глобальная
    таблица. Хелпер — extension на `AppDbContext`:
    `Task<ProblemDetails?> SaveWithUniquenessBackstopAsync(this AppDbContext db,
    CancellationToken ct, params (UniqueIndexViolation Index, Func<ProblemDetails> Problem)[]
    mappings)`. Возвращает `null` при успехе и `ProblemDetails` при смапленном конфликте;
    эндпоинт сам оборачивает в `TypedResults.Conflict(...)`, сохраняя свой точный union-тип
    результата. `ProblemDetails` строится **лениво** (`Func`) — только на фактическом
    конфликте, а не на каждом happy-path-сохранении. Неузнанное (`None`) либо не перечисленное
    в `mappings` нарушение **пробрасывается** дальше (re-throw) → глобальный
    `UseExceptionHandler` вернёт 500. `DbUniqueViolation.Identify` остался единственным
    распознавателем — не трогали.
  - **Каталог описаний аудита** — статический класс `AuditDescriptions` в `Web/Endpoints/`,
    по методу на формулировку, русский текст и подстановка имён сущностей инкапсулированы.
    Тексты перенесены дословно. Раздельно сохранены **различающиеся** шаблоны одного
    `AuditActionType.PublicationUpdated`: `PublicationUpdatedForInfobase` (в составе агрегата
    Infobase — «…обновлена **для инфобазы «X»**…») и `PublicationUpdated` (прямое
    редактирование — без «для инфобазы»). `PublicationReconciled` сохранён буквально: без « »
    вокруг метки и со словом «**оператором**» (не «администратором»), формат «…: статус
    {prev} → {new}.». Метка публикации = `SiteName + VirtualPath` передаётся аргументом,
    пунктуацию задаёт шаблон. Каталог наполнен **полностью** (включая строки `DeleteAsync`
    Infobase/Tenant), чтобы устранять дрейф целиком, а не наполовину.
  - **Резолв initiator** — extension `ResolveInitiator(this HttpContext)` в
    `Web/Endpoints/EndpointHelpers.cs`; в трёх целевых файлах вызывается один раз и
    переиспользуется. `SettingsEndpoints` (вне scope MLC-021) не трогали.
- **Фиделити 1:1:** `DeleteAsync` (Infobase/Tenant) backstop не используют (там нет
  insert/uniqueness) — их `SaveChangesAsync` оставлен как есть; изменены только initiator +
  строки аудита. Поведение `when`-фильтров точно воспроизведено: успех → null; смапленное
  нарушение → тот же ProblemCodes 409; неузнанное → re-throw.
- **Файлы:** `backend/src/MitLicenseCenter.Web/Endpoints/EndpointHelpers.cs` (новый),
  `…/Endpoints/AuditDescriptions.cs` (новый); правки в `…/Endpoints/InfobasesEndpoints.cs`,
  `…/Endpoints/TenantsEndpoints.cs`, `…/Endpoints/PublicationsEndpoints.cs`;
  `backend/tests/MitLicenseCenter.Tests.Unit/Endpoints/AuditDescriptionsTests.cs` (новый).
  Канон (`docs/`) не правили — контракт не изменился, ADR-19/ADR-20 описывают сохранённое
  поведение.
- **Тесты:** новый `AuditDescriptionsTests` (5 фактов) фиксирует точные строки каждого
  шаблона как единицу. `DbUpdateExceptionBackstopTests` (коды 409 + re-throw на FK),
  `InfobaseReassignTests`, `InfobaseCascadeDeleteTests`, `PublicationsReconcileTests`,
  `TenantDeletionGuardTests`, `InfobaseUniquePerTenantTests` — зелёные без правок
  (регрессионный гейт 1:1). Отдельный юнит-тест backstop-хелпера не добавляли: он уже
  покрыт сквозь эндпоинты.
- **Прогон:** `dotnet test --filter "Category!=Smoke"` — 254 passed, 0 failed;
  `scripts/build.ps1` — зелёный (backend build 0 warnings, dotnet format чистый, FE
  lint/type-check/test 78 passed, FE build).

### MLC-022 — Единый источник правил валидации Infobase/Publication (FE↔BE) — 2026-06-03

- **Проблема:** один набор правил валидации Infobase/Publication был продублирован в трёх
  местах: (1) FE — `InfobaseFormDialog.tsx` (константы `PLATFORM_VERSION_PATTERN`,
  `GUID_PATTERN`, Zod-фабрика `buildSchema`); (2) BE-дубль №1 — `InfobasesEndpoints.
  AppendPublicationFieldErrors`; (3) BE-дубль №2 — `PublicationsEndpoints.UpdateAsync`
  (построчно тот же набор правил публикации, отличался только префиксом ключей полей).
  Лимиты 200/50/260/8000 — в DataAnnotations DTO. Источника правды нет: новое поле/правило =
  правка 2–3 мест, рассинхрон тихий. Cost if ignored: M (формы растут).
- **Проверенные факты (для трактовки «1:1»):** backend **не** прогоняет DataAnnotations в
  runtime (нет endpoint-фильтра валидации) — реальная runtime-валидация только ручная
  (non-empty + regex + virtual-path-правила + physical-path-абсолютность); max-длины в runtime
  BE не режутся (только DTO-аннотации для swagger + nvarchar-констрейнт БД). FE применяет
  `max` к name/dbServer/dbName/siteName (200) и physicalPath (260), к virtualPath/platformVersion
  max не применяет (regex/правила). Регекс уже частично был расшарен через
  `InfobasesEndpoints.IsValidPlatformVersion`.
- **Решение:** поведение-сохраняющий рефакторинг (дедуп + централизация), **без codegen**
  (codegen — отдельная отложенная `MLC-025`). Ни одно accept/reject не изменено.
  - **BE — единый источник** `MitLicenseCenter.Web/Endpoints/InfobaseValidationRules.cs`
    (новый `public static partial class`): `const int` лимиты (Name/DatabaseServer/DatabaseName/
    SiteName/VirtualPath = 200, PlatformVersion = 50, PhysicalPath = 260, VrdCustomXml = 8000),
    `[GeneratedRegex] PlatformVersionRegex()`, `IsValidPlatformVersion`, и единый
    `AppendPublicationFieldErrors(errors, prefix, …)`. Параметр `prefix` задаёт префикс ключей
    полей: `"Publication."` для вложенной публикации инфобазы (`InfobasesEndpoints`) и `""` для
    плоских ключей прямого `PUT /publications/{id}` (`PublicationsEndpoints`). Тексты-сообщения
    перенесены дословно (один экземпляр). `PublicationsEndpoints.UpdateAsync` потерял инлайн-блок
    валидации (~30 строк) → вызывает общий хелпер; `InfobasesEndpoints` потерял свой regex/
    `IsValidPlatformVersion`/`AppendPublicationFieldErrors`. DTO-аннотации (`InfobasesContracts`,
    `PublicationsContracts`) `StringLength(…)` → `StringLength(InfobaseValidationRules.*MaxLength)`
    (значения те же, `const int` валиден в атрибуте).
  - **FE — единый источник** `frontend/src/features/infobases/validation.ts` (новый):
    экспорт `PLATFORM_VERSION_PATTERN`, `GUID_PATTERN`, `STATUSES`, лимиты-константы
    (`*_MAX_LENGTH`), фабрика `buildInfobaseFormSchema(t)` (перенос `buildSchema` **дословно**,
    литералы лимитов заменены на константы там, где они уже применялись), тип
    `InfobaseFormValues`. `InfobaseFormDialog.tsx` импортирует их; локальные определения и
    неиспользуемый импорт `z`/`InfobaseStatus` удалены. **Поведение и сообщения 1:1.**
    `VIRTUAL_PATH_MAX_LENGTH`/`PLATFORM_VERSION_MAX_LENGTH` существуют как документированный
    источник (схему не меняли — длина platformVersion связана regex'ом, virtualPath режется на
    уровне БД/DTO); потребляются parity-тестом.
- **Защита от дрейфа без codegen:** parity-тесты на **обеих** сторонах с идентичной golden-
  таблицей версии платформы и пинами констант к литералам прозы-спеки `03_DOMAIN_MODEL.md`:
  BE — расширен `InfobasesValidationTests.cs` (theory перенацелена на `InfobaseValidationRules.
  IsValidPlatformVersion`; новый факт `Validation_rules_match_documented_spec` пинит
  `PlatformVersionRegex().ToString()` и лимиты); FE — новый `__tests__/validation.test.ts`
  (та же golden-таблица + пины `*_MAX_LENGTH`/`PLATFORM_VERSION_PATTERN.source` + кейсы
  virtualPath). Кто меняет regex/лимит на своей стороне — ломает свой же тест (пин к спеке).
- **Канон:** `03_DOMAIN_MODEL.md` остаётся человекочитаемой прозой-спекой; добавлен один блок-
  указатель «единый источник правил валидации» (где централизованы FE/BE, что закреплено
  parity-тестами). Формулировки самих правил не трогали. `DECISIONS.md` не правили (контракт и
  поведение не изменились).
- **Файлы:** `backend/src/MitLicenseCenter.Web/Endpoints/InfobaseValidationRules.cs` (новый);
  правки `…/Endpoints/InfobasesEndpoints.cs`, `…/Endpoints/PublicationsEndpoints.cs`,
  `…/Endpoints/InfobasesContracts.cs`, `…/Endpoints/PublicationsContracts.cs`;
  `backend/tests/MitLicenseCenter.Tests.Unit/Endpoints/InfobasesValidationTests.cs`;
  `frontend/src/features/infobases/validation.ts` (новый),
  `frontend/src/features/infobases/__tests__/validation.test.ts` (новый),
  `frontend/src/features/infobases/InfobaseFormDialog.tsx`; `docs/03_DOMAIN_MODEL.md`.
- **Фиделити 1:1:** существующий `InfobaseFormDialog.test.tsx` (маппинг 409, edit-режим) и
  backend endpoint-тесты — зелёные без правок (регрессионный гейт). `ValidateInfobase`
  (infobase-специфичная, не дублировалась) оставлена в `InfobasesEndpoints` без изменений.
- **Прогон:** backend `dotnet test --filter "Category!=Smoke"` — 252 passed, 0 failed; frontend
  `pnpm lint` / `type-check` / `test` (97 passed) — зелёные; `scripts/build.ps1` — зелёный
  end-to-end (backend build 0 warnings, FE lint/type-check/test 97 + build, «Все шаги пройдены
  успешно»).

### MLC-023 — Декомпозиция InfobaseFormDialog (787 строк) — 2026-06-03

- **Проблема:** `frontend/src/features/infobases/InfobaseFormDialog.tsx` (787 строк) — самый
  большой и связный компонент проекта, главная форма продукта. В одном файле совмещались:
  режимы create+edit, `defaultValues`, эффект prefill настроек, три `touched`-рефа
  автоподстановки + `settingsApplied`, точечная проверка занятости кластер-базы (set/clear
  server-ошибки), раскрытие блока «Дополнительно», маппинг 409/404→ошибки полей и весь JSX.
  Каждый новый атрибут Infobase/Publication оседал здесь — росли стоимость изменения и риск
  регрессий в prefill-логике. Cost if ignored: M–L.
- **Решение:** поведение-сохраняющий рефакторинг (как MLC-018: декомпозиция, наблюдаемое
  поведение 1:1). Правила валидации **не** возвращены в форму — переиспользован извлечённый в
  MLC-022 `validation.ts` (`buildInfobaseFormSchema`, `STATUSES`, regex/лимиты). Ответственности
  разложены на 4 модуля:
  - **`useInfobaseForm.ts`** (новый хук) — вся не-презентационная логика, перенесённая из
    компонента **дословно**: чтение настроек + дефолты, `useForm` + `zodResolver` +
    `defaultValues` (ветки create/edit), три `touched`-рефа (`useRef(isEdit)`) +
    `settingsApplied` (`useRef(false)`), эффект prefill настроек (тот же guard
    `isEdit || settingsApplied.current || !settings`, те же `setValue`, та же
    `eslint-disable`-строка deps), `useWatch`, discovery-запросы (gated по `open`) + состояния +
    опции + refetch-колбэки, `useClusterIdAvailability` + эффект set/clear server-ошибки,
    `handleClusterChange`/`handleDatabaseNameChange`, `computedDefaultPath`,
    `advancedOpen`/`setAdvancedOpen`, `onSubmit` (предчек занятости, сборка input'ов, мутации,
    toast'ы, маппинг 409, `onInvalid`→раскрытие «Дополнительно» по `ADVANCED_ERROR_KEYS`).
    Закрытие диалога на успехе (`onOpenChange(false)`) перенесено внутрь хука — `onOpenChange`
    добавлен в аргументы (т.к. `form.handleSubmit` не пробрасывает результат во вью).
    `touched`-рефы наружу отданы как `markNameTouched`/`markVirtualPathTouched`/
    `markPhysicalPathTouched`.
  - **`PublicationFieldset.tsx`** (новый презентационный компонент) — всё тело блока
    «Дополнительно» (3 группы: Инфобаза name+status, СУБД databaseServer, Публикация в IIS:
    siteName/virtualPath/platformVersion/physicalPath + 2 чекбокса). Без состояния и эффектов:
    принимает `control`, discovery-пропсы для site/platformVersion, `computedDefaultPath` и
    `mark*Touched`-колбэки (вызываются в `onChange` перед `field.onChange` — 1:1 прежнее
    поведение). `STATUSES` импортируется из `validation.ts`.
  - **`mapConflictToField.ts`** (новый чистый helper) — классификация ошибки API → дескриптор
    ошибки поля (`{ field, messageKey, openAdvanced? }`): 409 `NAME_DUPLICATE_IN_TENANT`→name
    +openAdvanced, 409 `INFOBASE_ALREADY_ASSIGNED`→clusterInfobaseId, 404→tenantId, иначе→null.
    i18n/`setError`/toast и fallback (ApiError 400→`message`-toast, прочее→generic) остаются в
    хуке. Тестируется отдельно от рендера.
  - **`InfobaseFormDialog.tsx`** → тонкий вью (787→~230 строк): `useInfobaseForm` + разметка
    диалога, видимые поля (tenantId/clusterInfobaseId/databaseName), disclosure-кнопка и
    `<PublicationFieldset/>`. Публичный API компонента (пропсы) не изменён.
- **Сохранение поведения prefill/touched (1:1):** инициализация рефов (`isEdit` для трёх
  touched, `false` для `settingsApplied`), guard и `setValue` эффекта prefill, авто-раскрытие
  «Дополнительно» (`onInvalid` по `ADVANCED_ERROR_KEYS` + ветка `NAME_DUPLICATE`
  через `openAdvanced`) — перенесены дословно.
- **Тесты:** существующие `InfobaseFormDialog.test.tsx` (маппинг 409, точечная занятость,
  раскрытие «Дополнительно») и `ReassignInfobaseDialog.test.tsx` — зелёные **без правок**
  (регрессионный гейт: публичный API формы не менялся). Добавлены точечные:
  `__tests__/mapConflictToField.test.ts` (оба 409-кода, 404, неизвестный код/без тела/400/
  не-ApiError→null) и `__tests__/useInfobaseForm.test.tsx` (`renderHook`: create-prefill
  заполняет пустые поля после загрузки каталога; edit стартует со значений инфобазы и prefill
  их не перетирает; автоподстановка virtualPath/physicalPath из имени БД отключается точечно
  после `markVirtualPathTouched`).
- **Канон:** не трогали (`docs/` и бэкенд без изменений) — наблюдаемое поведение тождественно.
- **Файлы:** `frontend/src/features/infobases/useInfobaseForm.ts` (новый),
  `…/PublicationFieldset.tsx` (новый), `…/mapConflictToField.ts` (новый),
  `…/__tests__/useInfobaseForm.test.tsx` (новый), `…/__tests__/mapConflictToField.test.ts`
  (новый); правка `…/InfobaseFormDialog.tsx`.
- **Прогон:** frontend `pnpm lint` / `type-check` / `test` (108 passed, 19 файлов) — зелёные;
  `scripts/build.ps1` — зелёный end-to-end (backend 255 passed, FE lint/type-check/test 108 +
  build, «Все шаги пройдены успешно»).

### MLC-024 — App-id whitelist лицензий → dbo.Settings — 2026-06-03

- **Проблема:** набор client-типов 1С, потребляющих лицензию, был статическим `HashSet`
  в `RacExecutableRasClusterClient.cs` (использовался в `TryParseSession` для вычисления
  `ConsumesLicense`). По природе это настройка — ввод/переименование типа 1С требовал правки
  кода + редеплоя, хотя оператор должен мочь подстроить список без релиза. Cost if ignored: S.
- **Решение:** whitelist вынесен в `dbo.Settings` (`OneC.LicenseConsumingAppIds`), читается
  адаптером через `ISettingsSnapshot` (тот же TTL-кэш ≈30s, что у `ExePath`/`Endpoint`). Единый
  источник дефолта и парсинга — новый чистый хелпер `LicenseConsumingAppIds` (стиль `LicenseConsumption`
  MLC-020 / `InfobaseValidationRules` MLC-022):
  - **`LicenseConsumingAppIds.Default`** = `"1CV8,1CV8C,WebClient,Designer,COMConnection"` —
    единственное место хранения дефолта (на него же ссылается `SettingDefinitions.DefaultValue`,
    без дрейфа строки и кода).
  - **`LicenseConsumingAppIds.Parse(string?)`** — split по запятой (`RemoveEmptyEntries | TrimEntries`),
    case-insensitive (`OrdinalIgnoreCase`); пустой результат (null/whitespace/только разделители)
    откатывается на `Default`. Гарантирует поведение **1:1** с прежним статическим набором при
    незаданной настройке.
- **Каталог настроек (14-й ключ):** `SettingKey.OneCLicenseConsumingAppIds`
  (`"OneC.LicenseConsumingAppIds"`, wire-контракт) + запись в `SettingDefinitions.All`
  (`Kind=Text`, `IsSecret=false`, русское `Description`, `DefaultValue=LicenseConsumingAppIds.Default`).
  `Text` ⇒ `ValidateValue` без ограничений (любой список запятых проходит). Сидер автоматически
  засевает дефолт при первом старте; снапшот кэширует строго ключи каталога — новый ключ доступен
  без правок инфраструктуры.
- **Адаптер:** удалён static `LicenseConsumingAppIds`; в `ListActiveSessionsAsync` список читается
  один раз на вызов (не per-session — бюджет спавна не затронут) и прокидывается в `TryParseSession`
  (остался статическим/чистым, добавлен параметр `HashSet<string>`; `ConsumesLicense = set.Contains(appId)`).
- **Frontend (Option A — поле не появляется само):** `SettingsPage.tsx` рендерит из захардкоженного
  `SECTIONS`, не из каталога (это и фиксирует отложенная MLC-026). Поэтому ключ добавлен в секцию
  «cluster» + `FIELD_META` (text + placeholder с дефолтом) и i18n `settings.labels`/`settings.hints`
  в `ru.json`. Оператор правит whitelist через «Параметры».
- **Канон:** `docs/DECISIONS.md` — описание `ConsumesLicense` обновлено: дефолтный набор теперь
  operator-overridable через `OneC.LicenseConsumingAppIds` (present-tense).
- **Тесты:** новый `Settings/LicenseConsumingAppIdsTests.cs` (null/""/whitespace/только-разделители
  → дефолт; default-константа = 5 app-id; case-insensitive; trim; кастомный список полностью заменяет
  дефолт). Новый кейс в `RacExecutableRasClusterClientTests` (настройка `"BackgroundJob"` делает
  BackgroundJob лицензионным, а `1CV8` — нет: доказывает чтение из Settings и полную замену).
  Существующие RAS-тесты зелёные без правок (`BuildSettings` не стабит новый ключ → `null` → дефолт).
  Новый FE `settings/__tests__/SettingsPage.test.tsx` (рендер: поле и input появляются из каталога).
- **Файлы:** `backend/.../Application/Settings/LicenseConsumingAppIds.cs` (новый),
  `…/Domain/Settings/SettingKey.cs`, `…/Application/Settings/SettingDefinitions.cs`,
  `…/Infrastructure/Clusters/RacExecutableRasClusterClient.cs`,
  `backend/tests/.../Settings/LicenseConsumingAppIdsTests.cs` (новый),
  `…/Clusters/RacExecutableRasClusterClientTests.cs`; `frontend/src/features/settings/SettingsPage.tsx`,
  `…/settings/__tests__/SettingsPage.test.tsx` (новый), `frontend/src/i18n/ru.json`; `docs/DECISIONS.md`.
- **Прогон:** `dotnet test --filter "Category!=Smoke"` — 262 passed; frontend `pnpm test` —
  109 passed (20 файлов); `scripts/build.ps1` — зелёный end-to-end («Все шаги пройдены успешно»).

### MLC-030 (REF-02) — Архитектурные guard-тесты границ слоёв — 2026-06-03

- **Проблема:** направление зависимостей (Web → Infrastructure → Application → Domain) и главная
  anti-corruption граница к 1С/IIS (ADR-5/16, расширенная ADR-20: «Web никогда не трогает
  `rac.exe`/`ras.exe`/IIS/`Microsoft.Web.Administration` напрямую — только через Infrastructure-адаптер
  за Application-интерфейсом») держались **только дисциплиной и код-ревью**. Ничто в CI не падало,
  если бы кто-то заинжектил инфраструктурный адаптер или `new Process()` прямо в эндпоинт — регресс
  границы мог пройти молча. Это (по REF-02 плана `distributed-orbiting-snail.md`) — фундамент-страховка
  под все последующие рефакторинги, поэтому берётся первой. Cost if ignored: S–M.
- **Решение (только тесты, прод-код/архитектура/ADR не тронуты):** новый класс
  `Architecture/LayerBoundaryTests.cs` в `tests/MitLicenseCenter.Tests.Unit` на **NetArchTest.Rules**
  (анализ зависимостей сборок на уровне IL через Mono.Cecil — ловит использование запрещённого типа
  даже в теле метода; рефлексия по сигнатурам этого не видит). Три факта:
  1. **`Domain_has_no_dependency_on_other_layers`** — Domain-сборка не зависит от
     `MitLicenseCenter.Application`/`.Infrastructure`/`.Web`.
  2. **`Application_has_no_dependency_on_Infrastructure_or_Web`** — Application-сборка не зависит от
     `MitLicenseCenter.Infrastructure`/`.Web`.
  3. **`Web_does_not_reference_OneC_IIS_infrastructure_adapters_directly`** (ADR-5/16/20) — Web-сборка
     не зависит ни от одного из: `…Infrastructure.Clusters`, `…Publishing`, `…Discovery`, `…Jobs`,
     `Microsoft.Web.Administration`, `System.Diagnostics.Process`.
- **Почему правило 3 — на уровне типов, а не сборки:** Web **легитимно** ссылается на сборку
  Infrastructure (ADR-20 vertical slice к собственной БД панели) — `…Persistence` (`AppDbContext`),
  `…Identity` (AppUser/AppRole/Roles), `…Audit` (сущность `AuditLog` через `db.AuditLogs`),
  `…Settings` (`SettingsSeeder` в fail-fast bootstrap), корневой `…Infrastructure` (`AddInfrastructure`).
  Запрещены только адаптерные неймспейсы 1С/IIS и внешние инфраструктурные типы. Тонкость: запрещён
  **тип** `System.Diagnostics.Process`, а не весь неймспейс — `Program.cs` легитимно использует
  `System.Diagnostics.Activity` для `traceId`.
- **Правило 4 (опц., «Web не обходит DI/интерфейсы») сознательно не выделено:** единственный реальный
  способ обхода — напрямую использовать адаптерный тип/`Process`/`Microsoft.Web.Administration`, что уже
  запрещает правило 3 на уровне IL; отдельный тест без ложных срабатываний здесь невыразим
  (зафиксировано комментарием в файле).
- **Негативная проверка (локально, в коммит не включена):** временная вставка
  `System.Diagnostics.Process.GetProcesses()` в тело `MapDashboardEndpoints` — правило 3 краснеет и
  называет нарушителя (`MitLicenseCenter.Web.Endpoints.DashboardEndpoints`), доказывая IL-анализ тела
  метода; откат → снова зелёно.
- **Зависимость:** `NetArchTest.Rules` 1.3.2 — test-only (`PackageVersion` в `backend/Directory.Packages.props`
  + `PackageReference` в тест-csproj); прод-сборки её не тянут.
- **Файлы:** `backend/tests/.../Architecture/LayerBoundaryTests.cs` (новый),
  `backend/Directory.Packages.props`, `backend/tests/.../MitLicenseCenter.Tests.Unit.csproj`.
- **Прогон:** `dotnet test --filter "Category!=Smoke"` — 265 passed (3 новых guard-теста зелёные);
  `dotnet format --verify-no-changes` — без правок; frontend `pnpm lint`/`type-check`/`test`/`build` —
  зелёные (109 тестов, сборка ОК). Smoke-тесты `RacExecutableSmokeTests` требуют живого RAS и в этом
  гейте исключаются (как и в CI: `--filter "Category!=Smoke"`).

### MLC-029 (REF-01) — Дедуп маппинга `Publication` request→entity в `InfobasesEndpoints` — 2026-06-03

- **Проблема:** блок присваивания 7 полей публикации дословно дублировался между `CreateAsync`
  (инициализатор `new Publication { … }`, ~стр. 201–215) и `UpdateAsync` (присваивания
  `publication.* = …`, ~стр. 302–311) в `Web/Endpoints/InfobasesEndpoints.cs`. Совпадали и значения,
  и логика нормализации: `SiteName`/`VirtualPath`/`PlatformVersion` → `.Trim()`, `EnableOData`/
  `EnableHttpServices` как есть, `VrdCustomXml` → `IsNullOrWhiteSpace ? null : …`, `PhysicalPathOverride`
  → `IsNullOrWhiteSpace ? null : .Trim().TrimEnd('\\','/')`. Правка правила в одном месте легко
  забывалась в другом. По REF-01 плана `distributed-orbiting-snail.md` — самый дешёвый чистый выигрыш,
  берётся вторым после фундамента-страховки REF-02 (MLC-030). Cost if ignored: S.
- **Решение (intra-slice, ADR-20 не затрагивается — use-case-слой не вводился):** новый приватный
  статический хелпер `ApplyPublicationFields(Publication target, …)` в том же partial-классе, рядом с
  `AppendPublicationErrors`. Ядро принимает **дискретные значения** 7 полей и заполняет цель с той же
  нормализацией/порядком 1:1. Поскольку `CreatePublicationRequest` и `UpdatePublicationRequest` — разные
  типы, добавлены две тонкие перегрузки-адаптера (как уже сделано для `AppendPublicationErrors`),
  разворачивающие request в ядро. Хелпер закрывает **только** 7 общих полей: `CreateAsync` сохраняет
  своё `Id`/`InfobaseId`/`CreatedAt`, `UpdateAsync` — своё `UpdatedAt`.
- **Тонкость с `required`:** `SiteName`/`VirtualPath`/`PlatformVersion` помечены `required` в доменной
  `Publication`, поэтому инициализатор в `CreateAsync` обязан их задать формально — заданы как `null!`
  с комментарием; `ApplyPublicationFields` тут же перезаписывает их реальными значениями. Единый
  источник маппинга сохранён, поведение не меняется.
- **Контракт неизменен:** валидация (`ValidateInfobase` + `AppendPublicationErrors`), backstop-409
  (`SaveWithUniquenessBackstopAsync`), состав аудита (`InfobaseCreated`+`PublicationCreated` /
  `InfobaseUpdated`+`PublicationUpdated`) — без правок.
- **Файлы:** `backend/src/MitLicenseCenter.Web/Endpoints/InfobasesEndpoints.cs` (только этот файл;
  `InfobasesContracts.cs` менять не потребовалось).
- **Прогон:** `dotnet build -c Release` — 0 ошибок/предупреждений; `dotnet test --filter "Category!=Smoke"`
  — 265 passed (тесты эндпоинтов Infobases + guard-тесты MLC-030 зелёные → поведение 1:1);
  `dotnet format --verify-no-changes` — без правок. Smoke-тесты `RacExecutableSmokeTests` (2 шт.)
  требуют живого 1С RAS на 1540 и в этом окружении падают на отказе соединения — предсуществующее
  окружное ограничение, к правке отношения не имеющее; исключаются как и в CI (`--filter "Category!=Smoke"`).

### MLC-031 (REF-03) — Фабрика CRUD-mutation хуков на фронте — 2026-06-03

- **Проблема:** в каждой `features/*/use<X>.ts` повторялся один шаблон мутации —
  `useMutation({ mutationFn: (v) => api(...), onSuccess: () => qc.invalidateQueries({ queryKey }) })`
  для create/update/delete (канон — `features/infobases/useInfobases.ts`). Бойлерплейт `useQueryClient()`
  + ручной `invalidateQueries` дублировался в 10 хуках 5 фич; политика инвалидации размазана. По REF-03
  плана `distributed-orbiting-snail.md` — дешёвый FE-дедуп, берётся третьим (Phase 1). Cost if ignored: S.
- **Решение:** новый generic-хелпер `useInvalidatingMutation` в `frontend/src/lib/useInvalidatingMutation.ts`.
  Generic по типу переменных мутации (`CreateInput`, `{id, input}`, `string` для delete — все сигнатуры).
  Параметр `invalidate` принимает: один ключ (`infobasesQueryKey`), массив ключей
  (`[infobasesQueryKey, tenantsQueryKey]`) или функцию от переменных (`(id) => […]`), если ключ зависит
  от переменных. Нормализация «один ключ vs массив ключей» — `toKeyList`: QueryKey сам массив, поэтому
  массив ключей распознаётся по тому, что все элементы — массивы. Доп-логика в `onSuccess` сохранена
  необязательным параметром `(data, variables) => void`, вызывается после инвалидации.
- **Переведены 10 хуков (поведение 1:1):** `useCreateInfobase`/`useUpdateInfobase`/`useDeleteInfobase`
  (1 ключ), `useReassignInfobase` (2 ключа — `infobases`+`tenants`), `useCreateTenant`/`useUpdateTenant`/
  `useDeleteTenant`, `useUpdateSetting`, `useKillSession` (1 ключ), `useReconcile` (функция —
  `[publicationsQueryKey, driftStatusQueryKey(publicationId)]`, ключ drift-статуса зависит от id).
- **Намеренно не тронуты (не CRUD-invalidation паттерн):** `useLogin`/`useLogout` (политика —
  `setQueryData`/`qc.clear()`, не инвалидация), `useChangePassword`/`useCheckDrift` (без `onSuccess`).
  Фабрика не переусложнялась под их сигнатуры.
- **Контракт неизменен:** те же queryKey-префиксы, та же политика инвалидации (fire-and-forget `void`),
  сетевые вызовы `api(...)` без правок; query-хуки (`useQuery`) и серверная пагинация не затронуты.
  Тесты диалогов (`TenantFormDialog`/`ReassignInfobaseDialog`/`DeleteTenantDialog`) проверяют
  `invalidateQueries({ queryKey })` теми же ссылками-ключами → зелёные без правок.
- **Файлы:** `frontend/src/lib/useInvalidatingMutation.ts` (новый), `features/infobases/useInfobases.ts`,
  `features/tenants/useTenants.ts`, `features/publications/usePublications.ts`,
  `features/sessions/useKillSession.ts`, `features/settings/useSettings.ts`.
- **Прогон:** `scripts/build.ps1` зелёный целиком — backend 268 passed + `dotnet format` без правок;
  frontend `pnpm lint`/`type-check`/`test` (109 passed, CRUD-тесты MLC-007 без правок)/`build` ОК
  («Все шаги пройдены успешно»).

### MLC-034 (REF-06) — Консолидация аудит-бойлерплейта мутирующих эндпоинтов — 2026-06-04

- **Проблема:** каждый мутирующий Web-эндпоинт повторял один скелет логирования —
  `var initiator = httpContext.ResolveInitiator();` затем одна-несколько
  `await audit.LogAsync(action, initiator: …, description: AuditDescriptions.X(…, initiator), tenantId: …, ct: ct)`.
  Дублировался резолв инициатора + именованный плумбинг `initiator:`/`ct:`/`ConfigureAwait`; при парных
  записях (`InfobaseCreated`+`PublicationCreated` и т.п.) легко забыть вторую. По REF-06 плана
  `distributed-orbiting-snail.md` — берётся четвёртым (Phase 2), после REF-01 (MLC-029), чтобы не двигать
  `InfobasesEndpoints.cs` дважды. Cost if ignored: M.
- **Решение (intra-Web, ADR-20-safe — use-case-слой НЕ вводился):** новый extension-метод
  `HttpContext.AuditAsync(IAuditLogger audit, AuditActionType action, Func<string,string> description, Guid? tenantId, CancellationToken ct)`
  в `Web/Endpoints/EndpointHelpers.cs` (рядом с `ResolveInitiator`/`SaveWithUniquenessBackstopAsync`,
  родом из MLC-021). Фасад инкапсулирует `ResolveInitiator()` и плумбинг `initiator`/`ct`, оставляя
  `AuditActionType.X` и `AuditDescriptions.X` явными и грепаемыми в строке вызова. Описания встраивают
  имя инициатора, поэтому форма — **делегат-описание**: `init => AuditDescriptions.X(…, init)`, фасад
  резолвит initiator и отдаёт его в фабрику. Парные записи остаются **раздельными** вызовами
  `AuditAsync` — состав/порядок журнала читается по коду, а не выводится из «умного» комбинированного метода.
- **Свёрнуто 9 каноничных сайтов (состав/порядок/условность аудита 1:1):** `InfobasesEndpoints`
  Create (`InfobaseCreated`→`PublicationCreated`), Update (`InfobaseUpdated`→`PublicationUpdated`),
  Reassign (`InfobaseReassigned`), Delete (`PublicationDeleted` **условно** `if (publication is not null)`
  → `InfobaseDeleted`, оба ДО удаления); `TenantsEndpoints` Create/Update/Delete (`TenantDeleted` ДО
  `Remove`); `PublicationsEndpoints` Update (`PublicationUpdated`, `tenantId = infobase?.TenantId`),
  reconcile (`PublicationReconciled`, nullable `tenantId`). Неиспользуемые после свёртки локали
  `var initiator` удалены; `infobaseName`/`tenantId`/`publicationLabel`/`name` (нужны до удаления) — оставлены.
- **Намеренно вне объёма (другой шаблон — не трогали, чтобы не сместить immutable-журнал):**
  `SessionsEndpoints.KillAsync` — инициатор `User.Identity?.Name ?? "Unknown"` (заглавная «U», ≠
  `ResolveInitiator()`'s «unknown»), inline-описание, `AuditReason.ManualByAdmin`; `SettingsEndpoints.UpdateAsync`
  — initiator переиспользуется для `store.SetAsync`, inline-описание без инициатора в тексте, без `tenantId`;
  `AuthEndpoints` (Login/Logout/ChangePassword) — инициатор из `user.UserName`/guarded имени, без
  fallback «unknown», `LoginAsync` вообще без `HttpContext`. Делегат-форма им не подходит, складывание
  изменило бы содержимое журнала.
- **Контракт неизменен:** тот же набор `AuditActionType`, те же `AuditDescriptions.*`, тот же `tenantId`,
  тот же порядок и условность; `IAuditLogger.LogAsync` (Application) не менялся (`reason` остаётся default
  `null` на этих сайтах); валидация/409/коды без правок. Граница слоёв (MLC-030) цела — фасад живёт в Web,
  зовёт `IAuditLogger` (Application-интерфейс).
- **Файлы:** `backend/src/MitLicenseCenter.Web/Endpoints/EndpointHelpers.cs` (новый `AuditAsync` + usings
  `Application.Auditing`/`Domain.Audit`), `InfobasesEndpoints.cs`, `TenantsEndpoints.cs`,
  `PublicationsEndpoints.cs`.
- **Прогон:** `scripts/build.ps1` зелёный целиком — backend build 0 ошибок/0 предупреждений, `dotnet test`
  268 passed (аудит/эндпоинт-тесты: `InfobaseCascadeDeleteTests`, `InfobaseReassignTests`,
  `PublicationsReconcileTests`, `TenantDeletionGuardTests` + регресс «вне объёма» `SettingsValidationTests`/
  `SessionsKillEndpointTests` + guard-тесты MLC-030 → состав журнала 1:1), `dotnet format` без правок;
  frontend `lint`/`type-check`/`test` (109 passed)/`build` ОК («Все шаги пройдены успешно»).

### MLC-033 (REF-05) — Обобщённый conflict→descriptor маппер + общий хвост submit форм — 2026-06-04

- **Проблема:** паттерн «диалог формы + разбор `ConflictBody` (409 `code`) → действие» был введён точечно
  в MLC-023 как `features/infobases/mapConflictToField.ts`, но та же логика «409 + проверка `code` →
  действие» оставалась заинлайненной в нескольких диалогах. По REF-05 плана `distributed-orbiting-snail.md`
  — берётся пятым (Phase 2), после REF-03 (`useInvalidatingMutation`, MLC-031). Cost if ignored: M.
- **Разведка (карта обработки конфликтов):** реально общий — лишь приём «409 + таблица `code` → дескриптор»,
  повторявшийся в 4 диалогах (`TenantFormDialog`, `useInfobaseForm`×2 кода, `ReassignInfobaseDialog`,
  `DeleteTenantDialog`). Дополнительно — дословно совпадавший «хвост» submit-catch двух форм
  (`400` → серверное сообщение, прочее → generic-тост). Каркас-компонент `<FormDialog>` отклонён как
  переусложнение (формы Tenant и Infobase расходятся радикально); объём согласован с пользователем как
  «маппер + хвост форм, без компонента-каркаса».
- **Решение (надстройка над `lib/api`, контракт `ConflictBody`/`readConflictBody` не тронут):** новый
  модуль `frontend/src/lib/apiErrors.ts` с двумя экспортами —
  `matchConflictCode<T>(error, table): T | null` (чистый классификатор: `table[code]` при `ApiError`
  `status===409` и непустом `code`, иначе `null`) и `toastFormSubmitError(error, t)` (общий хвост:
  `400` → `toast.error(error.message || t("errors.generic"))`, прочее → generic).
- **Переведено 5 сайтов (поведение/сообщения/поля-цели 1:1):** `mapConflictToField` (409-ветка свёрнута в
  `matchConflictCode<ConflictFieldError>`, **404-кейс** `tenantId` остаётся отдельной веткой — он не 409;
  экспорт `ConflictFieldError`/сигнатура не менялись); `useInfobaseForm` submit-хвост → `toastFormSubmitError`
  (с сохранением `openAdvanced`+`setError`); `TenantFormDialog` (inline 409 `NAME_DUPLICATE` →
  `matchConflictCode` + `form.setError`, хвост → `toastFormSubmitError`); `DeleteTenantDialog`
  (`TENANT_HAS_INFOBASES` → `toast`); `ReassignInfobaseDialog` (`INFOBASE_NAME_TAKEN_IN_TARGET` →
  локальный `setError`). Ставшие лишними импорты `ApiError`/`readConflictBody` убраны.
- **Намеренно вне объёма (иная природа — в form-абстракцию не загоняли):** `KillSessionDialog`
  (404 без code-таблицы, своя ветка «уже завершён» + close), `ReconcilePublicationDialog` (показывает
  серверный `detail`/`title`, локальный `ConflictBody` с `title` ≠ контрактный — code-таблицы нет),
  `DeleteInfobaseDialog` (разбора конфликтов нет). Общего code-маппинга у них нет.
- **Тест нового примитива:** `frontend/src/lib/__tests__/apiErrors.test.ts` — `matchConflictCode`
  (409 совпавший/неизвестный/без тела/без `code`, не-409 статусы, не-`ApiError`) и `toastFormSubmitError`
  (400 с message / пустой message / прочее). Существующие диалоговые тесты (MLC-007/023:
  `mapConflictToField`, `TenantFormDialog`, `DeleteTenantDialog`, `ReassignInfobaseDialog`,
  `InfobaseFormDialog`, `useInfobaseForm`) остаются регрессией, что переписывание не сменило поведение.
- **Файлы:** НОВЫЕ `frontend/src/lib/apiErrors.ts`, `frontend/src/lib/__tests__/apiErrors.test.ts`;
  правки `features/infobases/mapConflictToField.ts`, `features/infobases/useInfobaseForm.ts`,
  `features/tenants/TenantFormDialog.tsx`, `features/tenants/DeleteTenantDialog.tsx`,
  `features/infobases/ReassignInfobaseDialog.tsx`.
- **Прогон:** `scripts/build.ps1` зелёный целиком — backend `dotnet test` 268 passed, frontend
  `lint`/`type-check`/`test` (118 passed, +1 файл `apiErrors.test.ts`)/`build` ОК («Все шаги пройдены
  успешно»).

### MLC-032 (REF-04) — Декомпозиция крупных FE-страниц (Audit/Publications/Sessions) — 2026-06-04

- **Проблема:** три страницы-монолита смешивали оркестрацию данных, разметку таблицы и диалоги:
  `AuditPage` (368 строк), `PublicationsPage` (356), `SessionsPage` (290). По REF-04 плана
  `distributed-orbiting-snail.md` — берётся шестым (Phase 2), после REF-03 (`useInvalidatingMutation`,
  MLC-031) и REF-05 (MLC-033). Cost if ignored: M. Образец декомпозиции — MLC-023
  (`InfobasesPage` контейнер + `InfobaseRow`/`InfobaseTableHeader` презентация + `useInfobaseForm` хук).
- **Подход (чистое извлечение, не переписывание):** каждая страница разнесена на **контейнер**
  (header + error-banner + проводка диалогов), **оркестрационный хук** (`use<Page>` — запросы/мутации,
  URL-фильтры, пагинация, polling, состояние диалогов) и **презентационные части** (`<Feature>Table` с
  внутренней строкой-компонентом, `<Feature>FiltersBar`). Разметка/JSX, i18n-ключи, порядок колонок,
  поведение фильтров/пагинации/диалогов перенесены дословно 1:1; контракты API и query-ключи не тронуты.
  Страницы делались по одной, type-check/lint после каждой — регрессия локализуется.
- **Sessions** (NEW: `useSessionsPage.ts`, `SessionsFiltersBar.tsx`, `SessionsTable.tsx`): хук держит
  `parseParams`/`setFilter` (URL `q`+`infobaseId`), `infobaseById`-карту, `filtered`, состояние
  `KillSessionDialog`; `formatDuration` переехал в `SessionsTable` (используется только строкой `SessionRow`).
- **Publications** (NEW: `usePublicationsPage.ts`, `PublicationsFiltersBar.tsx`, `PublicationsTable.tsx`):
  хук инкапсулирует URL-фильтры (клиент+drift), **polling проверки дрейфа** (single-flight через
  `pollIntervalRef` + cleanup-effect, тосты started/completed/timeout, инвалидация `publicationsQueryKey`),
  состояние `ReconcilePublicationDialog`, `hasAnyPublications` для пустого состояния; `DRIFT_VARIANT` и
  `columnCount` (производное от `isAdmin`) переехали в `PublicationsTable`.
- **Audit** (NEW: `auditUrlState.ts`, `useAuditPage.ts`, `AuditTable.tsx`, `AuditPagination.tsx`): чистая
  URL/фильтр-логика (`parseFiltersFromUrl`/`filtersToUrl`/`buildBackendFilters` + date→ISO хелперы)
  вынесена в `auditUrlState.ts`; хук держит запросы (журнал/клиенты/ретенция), пагинацию
  (`total`/`totalPages`/`currentPage`), логику баннера ретенции (`nowUtc` зафиксирован на mount),
  `applyFilters`/`goToPage`; `actionBadgeClass` и `AuditRow` переехали в `AuditTable`, блок пагинации —
  в `AuditPagination`. Ранее существовавший `AuditFiltersBar` оставлен как есть.
- **Намеренно не трогал:** общие куски между страницами не вводил (преждевременная абстракция —
  таблицы/строки/фильтры трёх фич расходятся структурно и по колонкам); `AuditFiltersBar`,
  `ReconcilePublicationDialog`, `KillSessionDialog`, `urlState.ts`/`retention.ts` публикаций/аудита —
  без правок; именованные экспорты `AuditPage`/`PublicationsPage`/`SessionsPage` из тех же путей
  сохранены (lazy-импорты `routes/router.tsx` целы), code-splitting на чанк-на-страницу сохранён.
- **Поведение 1:1:** существующие чистые тесты (`audit/retention.test.ts`, `publications/urlState.test.ts`)
  остаются регрессией; ни один тест не импортировал перенесённые внутренние функции
  (`parseFiltersFromUrl`/`actionBadgeClass`/`formatDuration`/`DRIFT_VARIANT`) — проверено grep'ом.
  Извлечение чисто механическое (идентичный JSX), preview не потребовался.
- **Файлы:** НОВЫЕ `features/sessions/{useSessionsPage.ts,SessionsFiltersBar.tsx,SessionsTable.tsx}`,
  `features/publications/{usePublicationsPage.ts,PublicationsFiltersBar.tsx,PublicationsTable.tsx}`,
  `features/audit/{auditUrlState.ts,useAuditPage.ts,AuditTable.tsx,AuditPagination.tsx}`; переписаны
  контейнеры `features/sessions/SessionsPage.tsx` (290→~95), `features/publications/PublicationsPage.tsx`
  (356→~80), `features/audit/AuditPage.tsx` (368→~100).
- **Прогон:** `scripts/build.ps1` зелёный целиком — backend `dotnet test` 268 passed, `dotnet format`
  без правок; frontend `lint`/`type-check`/`test` (118 passed)/`build` ОК (каждая страница — отдельный
  чанк) — «Все шаги пройдены успешно».

### MLC-035 (REF-07) — Группировка плоского `Web/Endpoints` по фиче — 2026-06-04

- **Проблема:** 23 файла (`*Endpoints.cs` + `*Contracts.cs` + общие хелперы) лежали плоско в
  `backend/src/MitLicenseCenter.Web/Endpoints/`; при росте числа фич каталог терял навигируемость.
  По REF-07 плана `distributed-orbiting-snail.md` — берётся седьмым (Phase 2), последним активным
  пунктом рефакторинг-трека, после REF-01/REF-06 (MLC-029/034), чтобы не перемещать файлы дважды.
  Cost if ignored: M (низкий риск, но churn).
- **Стратегия namespace (определена по факту конфига).** Проверено: в `.editorconfig` правила
  `dotnet_style_namespace_match_folder` / IDE0130 **нет**; IDE0130 по умолчанию severity = suggestion —
  ниже порога `dotnet format --verify-no-changes --severity warn` и ниже `TreatWarningsAsErrors`
  (`backend/Directory.Build.props`, `EnforceCodeStyleInBuild=true`). Поэтому выбран **минимальный churn:
  плоский namespace** — файлы перемещены в подпапки, но `namespace MitLicenseCenter.Web.Endpoints`
  сохранён во всех (в C# папка ≠ namespace). Следствие: using-и, регистрация эндпоинтов в `Program.cs`
  и ссылки в тестах **не тронуты вовсе**; контент файлов не менялся ни в одном.
- **Подход (чистое перемещение, `git mv`).** 23 файла раскрыты как pure renames (история git
  сохранена), ноль правок содержимого. Раскладка по фиче + общий `Shared/`:
  - `Infobases/` — InfobasesEndpoints, InfobasesContracts
  - `Tenants/` — TenantsEndpoints, TenantsContracts
  - `Publications/` — PublicationsEndpoints, PublicationsContracts
  - `Sessions/` — SessionsEndpoints, SessionsContracts
  - `Settings/` — SettingsEndpoints, SettingsContracts
  - `Auth/` — AuthEndpoints, AuthContracts
  - `Audit/` — AuditEndpoints, AuditContracts
  - `Discovery/` — DiscoveryEndpoints
  - `Dashboard/` — DashboardEndpoints, DashboardContracts
  - `Health/` — HealthEndpoints
  - `Shared/` — EndpointHelpers, Problems, DbUniqueViolation, AuditDescriptions, InfobaseValidationRules
    (кросс-фичевые: `AuditDescriptions` описывает записи журнала, порождаемые мутирующими эндпоинтами
    Infobases/Tenants/Publications; `InfobaseValidationRules` используется Infobases/Publications/Tenants).
- **Намеренно не трогал:** `TransportSecurity.cs` лежит в корне `Web/` (не в `Endpoints/`) — вне объёма,
  оставлен на месте. Классы не сливались/не переименовывались, код не «причёсывался» — только раскладка.
  Поведение, маршруты, контракты, состав/порядок аудита, регистрация эндпоинтов — 1:1.
- **Поведение 1:1:** guard-тесты MLC-030 (`Architecture/LayerBoundaryTests.cs`, проверяют
  Infra-неймспейсы, не внутреннюю раскладку Web) и тесты эндпоинтов остались зелёными без правок —
  namespace не менялся, ссылки целы.
- **Файлы:** перемещены 23 файла `Web/Endpoints/*.cs` → подпапки-фичи + `Shared/` (renames, контент
  неизменен); правок исходников/тестов/`Program.cs`/`.csproj` (SDK-style glob) — ноль.
- **Прогон:** `scripts/build.ps1` зелёный целиком — backend `dotnet restore`/`build`/`test`/`format`
  (`--verify-no-changes`, без правок); frontend `lint`/`type-check`/`test` (118 passed)/`build` ОК —
  «Все шаги пройдены успешно».

### MLC-037 (PERF-01) — Инструментирование горячего пути: метрики цикла + спавнов `rac.exe` — 2026-06-04

- **Проблема:** перф горячего пути был неизмерим — метрик нет вовсе (ни `System.Diagnostics.Metrics`,
  ни EventCounters), единственный замер `Stopwatch`→`SnapshotPayload.TookMs` логировался на `Debug`
  (в проде заглушён). Счётчика спавнов `rac.exe` не было — бюджет ADR-3.3 (~26 проц/мин) нельзя
  подтвердить/опровергнуть. Первый и фундаментный пункт перф-трека (план
  `compressed-giggling-grove.md`, PERF-01): разблокирует измеримость остального и поставляет
  триггер-замер для `MLC-036`.
- **Что сделано (только наблюдение, поведение 1:1).** Введён лёгкий слой `Meter` через `IMeterFactory`
  (идиоматичный DI .NET 10; хост уже даёт `IMeterFactory` — правок `Program.cs` не потребовалось):
  - `Infrastructure/Diagnostics/RacMetrics.cs` — Meter **`MitLicenseCenter.Rac`**: Counter
    `rac.exe.spawns` (`{spawn}`, тег `command`) + Histogram `rac.exe.invocation.duration` (`ms`, теги
    `command`,`outcome`). Свойство `Enabled` — гард: при отсутствии слушателя тег команды не
    вычисляется (near-zero overhead).
  - `Infrastructure/Diagnostics/RacCommandTag.cs` — чистый аллокейшен-фри хелпер: из аргументов
    `rac.exe` выводит интернированную константу `cluster.list`/`session.list`/`session.terminate`/
    `infobase.summary.list`/`other` (endpoint-токен и `--опции` пропускаются естественно).
  - `Infrastructure/Diagnostics/ReconciliationMetrics.cs` — Meter **`MitLicenseCenter.Reconciliation`**:
    Histogram `reconciliation.cold.duration` + `reconciliation.hot.duration` (`ms`), Counter
    `reconciliation.kills` (`{session}`), ObservableGauge `reconciliation.hot_tenants` (`{tenant}`,
    читается из `IHotTierRegistry` на сборке метрики, не на горячем пути).
- **Точки инструментирования (1:1 поведение).** `SystemProcessRacRunner.RunAsync` — единственная
  точка спавна: `Stopwatch.GetTimestamp()` + одна точка записи под гардом `_metrics.Enabled`
  (оба `return` сведены, `outcome` = `ok`/`failed`/`timeout`; внешняя отмена `ct` пробрасывается без
  записи). `ReconciliationJob.RunColdAsync` / `HotTierPollingService` — `RecordColdCycle`/`RecordHotCycle`
  на **существующих** `Stopwatch` (тот же `sw`, что и `TookMs`). `KillEnforcer` — `AddKills(totalKills)`
  внутри существующего `if (totalKills>0)`. Регистрация двух singleton'ов в
  `Infrastructure/DependencyInjection.cs`.
- **Тесты.** `Tests.Unit/Diagnostics/RacCommandTagTests.cs` (7) — все четыре реальные формы команд
  (с endpoint и без, с auth-флагами; `--cluster=` не путается с глаголом `cluster`) + `other`/пусто.
  `Tests.Unit/Diagnostics/RacMetricsTests.cs` (2) — `MeterListener` подтверждает инкремент счётчика и
  запись гистограммы с тегами; `Enabled=false` без слушателя. Хелпер `Tests.Unit/Diagnostics/TestMetrics.cs`.
  Конструкторы `KillEnforcer` (5 тестов) и `SystemProcessRacRunner` (smoke) обновлены под доп-зависимость.
  Весь backend-сьют зелёный (277 тестов), включая host-building интеграционные — `IMeterFactory`
  резолвится из коробки.
- **Замер «до→после» (DoD).** Локально: `OneC.RAS.ExePath` направлен на заглушку с ненулевым exit
  (спавны без живого RAS); `dotnet-counters collect` (окно ~104 c) по обоим Meter'ам. Все инструменты
  **видны**: `rac.exe.spawns[command=cluster.list]`, `rac.exe.invocation.duration` (P50/95/99 ≈ 63–66 ms),
  `reconciliation.cold.duration` (≈ 64–65 ms), `reconciliation.hot_tenants=0`. Сумма спавнов за окно =
  **6** = детерминированная каденция **4 health-ping (30 c) + 2 cold-цикла (60 c)**. Кросс-проверка с
  логом: cold-спавны (через `ResolveClusterUuidAsync`) дают и `Warning` (`rac.exe cluster list…`), и
  записи `reconciliation.cold.duration`; health-ping (`PingAsync`) неуспех **не логирует** — значит
  счётчик `rac.exe.spawns` есть **полный** спавн-бюджет (надмножество логов) и единственный надёжный
  источник для ADR-3.3. Hot/kill-инструменты на idle-кластере ожидаемо нулевые — проявятся под
  нагрузкой seed-харнесса PERF-03.
- **Процедура снятия задокументирована** в `docs/OPERATIONS.md` → новый подраздел «Наблюдаемость
  перфа» (инструменты/единицы/теги, команда `dotnet-counters`, кросс-проверка, пример baseline).
- **Locked-границы соблюдены:** `rac.exe` — единственный адаптер (ADR-16/3.3), новый адаптер не введён;
  каденция/over-kill (ADR-6 / `[DisableConcurrentExecution]` / MLC-001) не тронуты; только встроенный
  `System.Diagnostics.Metrics` + `dotnet-counters`, без внешних систем (ADR-15); ADR-20/single-node/
  RU-only не затронуты.
- **Файлы:** новые `Infrastructure/Diagnostics/{RacMetrics,RacCommandTag,ReconciliationMetrics}.cs`,
  тесты `Tests.Unit/Diagnostics/{RacCommandTagTests,RacMetricsTests,TestMetrics}.cs`; правки
  `Infrastructure/Clusters/SystemProcessRacRunner.cs`, `Infrastructure/Jobs/{ReconciliationJob,
  HotTierPollingService,KillEnforcer}.cs`, `Infrastructure/DependencyInjection.cs`, `docs/OPERATIONS.md`,
  6 тест-сайтов (5×KillEnforcer + RacExecutableSmokeTests).
- **Прогон:** `scripts/build.ps1` зелёный целиком — backend `restore`/`build`/`test` (277)/`format`
  (`--verify-no-changes`, без правок); frontend `lint`/`type-check`/`test` (118)/`build` ОК —
  «Все шаги пройдены успешно».

### MLC-039 (PERF-03) — Нагрузочный seed-харнесс: проверка роста баз/сессий/аудита — 2026-06-04

- **Проблема:** метрики MLC-037 есть, но **нет воспроизводимого способа создать рост** — на
  idle-кластере hot/kill-инструменты нулевые, «держит ли рост» неизмеримо. Второй пункт перф-трека
  (план `compressed-giggling-grove.md`, PERF-03): даёт стенд, на котором меряются MLC-037 и валидируются
  PERF-05/06/07/09.
- **Что сделано (только dev/test-tooling, прод-код не тронут — задача аддитивная).** Новый
  **dev/test-only** консольный проект `backend/tools/MitLicenseCenter.Tools.PerfHarness` (папка `/tools/`
  в `MitLicenseCenter.slnx`), `IsPublishable=false`, **Web на него не ссылается**:
  - **seed-режим** (`PerfHarness seed --tenants N --infobases M --audit K --sessions S
    --over-limit-fraction F --seed`) — засев dev-БД через **реальный** `AppDbContext`, поэтому FK,
    уникальные индексы (`IX Tenants.Name`, `IX_Infobases_TenantId_Name`,
    `IX_Infobases_ClusterInfobaseId`) и 1:1-публикация соблюдаются самой моделью; миграции не трогаются.
    Аудит вставляется батчами по 10k с отключённым change-tracking (K=1e6 без разрыва памяти). Пишет
    `scenario.json` (S синтетических сессий + over-limit тенанты + список инфобаз для discovery).
  - **rac-stub-режим** (любые иные аргументы) — фейковый `rac.exe`, на который указывает
    `OneC.RAS.ExePath`. Классифицирует `cluster list`/`session list`/`session terminate`/`infobase
    summary list` (тем же приёмом, что тест-`BuildRunner`) и рендерит синтетику из `scenario.json` в
    формате `RacOutputParser`. **Stateless** (те же сессии на каждый вызов) → over-limit тенанты остаются
    over-limit → устойчивый kill-поток. `session terminate` → exit 0 (killed). Вывод ASCII → OEM-декод
    в `SystemProcessRacRunner` корректен. **Заглушка стоит за существующим
    `SystemProcessRacRunner`/`IRacProcessRunner` как внешний субпроцесс** — новый кластерный адаптер
    НЕ введён (ADR-16), и `rac.exe.spawns` снимаются **реально** (метрика живёт в точке спавна).
  - Чистая логика (`SeedDataGenerator`, `RacStub.Classify/Render`) вынесена в статические методы —
    покрыта 11 unit-тестами в `Tests.Unit/PerfHarness/` (инварианты графа, детерминизм по seed, K строк
    аудита, маршрутизация команд, round-trip session-list через `RacOutputParser`).
- **Скрипты:** `scripts/perf-seed.ps1` (обёртка над seed; UTF-8 BOM; `$LASTEXITCODE`-судейство;
  инвариантное форматирование доли) и `scripts/perf-counters.ps1` (обёртка `dotnet-counters
  monitor/collect`). Процедура прогона + ростовые точки — в `docs/OPERATIONS.md` (рядом с §MLC-037).
- **Ростовые точки:** baseline (N=20, M=50, K=100k, S=500) → ×10 (N=200, M=500, K=1e6, S=5000),
  over-limit ~30%.
- **Замер «до→после» (DoD).** Прогон на выделенной dev-БД `MitLicenseCenter_Perf` (миграции
  `db-reset.ps1`), `OneC.RAS.ExePath` → заглушка, реальный backend (Release) + `dotnet-counters collect`
  по обоим Meter'ам. Hot/kill-инструменты из нулевых стали ненулевыми и масштабируются:

  | Метрика | Baseline (S=500) | ×10 (S=5000) |
  | --- | --- | --- |
  | `reconciliation.hot_tenants` (gauge) | **6** | **60** (ровно ×10) |
  | `reconciliation.cold.duration` P50 | ~154 ms | ~196 ms |
  | `reconciliation.hot.duration` P50 | ~149 ms | ~196 ms |
  | `rac.exe.invocation.duration` (session.list) P50 | ~75 ms | ~89 ms |
  | `rac.exe.spawns[cluster.list]` /мин | ~32 | ~30 |
  | `rac.exe.spawns[session.list]` /мин | ~16 | ~16 |
  | `rac.exe.spawns[session.terminate]` /мин | ~14 | ~12 |
  | `reconciliation.kills` /мин | ~14 | ~12 |

  Вывод замера: длительности цикла растут с числом сессий/баз, а **спавн-бюджет каденционно-ограничен**
  (не зависит от объёма данных) — фактический спавн остаётся в рамках ADR-3.3 и под ростом. Это и есть
  демонстрация, что харнесс позволяет мерить рост.
- **Прод-сборка не включает харнесс (DoD).** `dotnet publish src/MitLicenseCenter.Web -c Release` →
  в выходе (89 файлов) `MitLicenseCenter.Tools.PerfHarness.dll` **отсутствует**.
- **Locked-границы соблюдены:** новый кластерный адаптер не введён (ADR-16/3.3 — заглушка за существующим
  раннером); каденция/over-kill (`MaxKillsPerCycle`, ADR-6/MLC-001) не тронуты; ADR-20/single-node
  (один кластер)/RU-only не затронуты; seed уважает FK и уникальные индексы; миграции не менялись.
- **Файлы:** новые `backend/tools/MitLicenseCenter.Tools.PerfHarness/{*.csproj,Program,Seeder,SeedGraph,
  RacStub,Scenario}.cs`, `backend/tests/MitLicenseCenter.Tests.Unit/PerfHarness/PerfHarnessTests.cs`,
  `scripts/{perf-seed,perf-counters}.ps1`; правки `backend/MitLicenseCenter.slnx` (+`/tools/`),
  `backend/tests/.../MitLicenseCenter.Tests.Unit.csproj` (ProjectReference), `docs/OPERATIONS.md`.
- **Прогон:** `scripts/build.ps1` зелёный целиком — backend `restore`/`build` (0 warnings,
  `TreatWarningsAsErrors`)/`test` (288 — +11 PerfHarness)/`format` (без правок); frontend
  `lint`/`type-check`/`test` (118)/`build` ОК — «Все шаги пройдены успешно».

### MLC-038 (PERF-02) — Профиль наблюдаемости EF + захват baseline запросов — 2026-06-05

- **Проблема:** в проде EF command-логирование = `Warning` — сгенерированный SQL не виден, медленный
  запрос не диагностируется. Утверждения «на `AuditLogs` возможен sort/lookup без составного индекса» и
  «correlated COUNT в `/tenants`» оставались догадкой. Третий пункт перф-трека (план
  `compressed-giggling-grove.md`, PERF-02): сделать паттерны EF-запросов измеримыми, чтобы PERF-06
  (индекс аудита) и PERF-07 (batch в `DriftCheckJob`) опирались на план запроса, а не на глаз.
- **Что сделано (наблюдение, сами запросы не тронуты).**
  - Новый чистый предикат-хелпер `Infrastructure/Diagnostics/EfQueryProfiling.cs` (по образцу
    `TransportSecurity`, MLC-012): `IsEnabled`/`IsSensitiveEnabled`/`ResolveLogPath` + файловый
    приёмник `BuildSink` (append под локом → файл + Console).
  - Гейт в `Infrastructure/DependencyInjection.cs` (лямбда `AddDbContext`): при включённом флаге —
    `options.LogTo(sink, [RelationalEventId.CommandExecuted], Information)`; `EnableSensitiveDataLogging`
    только при **отдельном** явном opt-in (`IsSensitiveEnabled` ⇒ требует и профиль, и свой флаг). Свой
    приёмник `LogTo` не зависит от секции `Logging` — единственный гейт это флаг.
  - `appsettings.Development.json`: документирующий блок `Diagnostics` (оба флага `false`).
    `appsettings.json`/`appsettings.Production.json` и уровни `Logging` **не тронуты** → прод 1:1.
  - 7 unit-тестов `Tests.Unit/Diagnostics/EfQueryProfilingTests.cs` (таблица истинности гейта:
    дефолт off, sensitive невозможен без профиля, путь по умолчанию/override).
- **Замер «до→после» (DoD).** Perf-БД `MitLicenseCenter_Perf` (харнесс MLC-039): baseline (100k аудита)
  и ×10 (1M аудита). Backend (Debug) с `Diagnostics__EfQueryProfiling=true`, логин admin → четыре
  эндпоинта, `ef-profile.log` усекался перед каждым вызовом. План аудита — `SET STATISTICS XML ON`
  напрямую по БД (Actual Execution Plan: операторы, `ActualLogicalReads`, `MissingIndexes`).
  - **Сгенерированный SQL** (идентичен на обоих объёмах): `/tenants` — COUNT + страница с
    **коррелированным** `SELECT COUNT(*) FROM [Infobases] WHERE [TenantId]=[t].[Id]` (один стейтмент,
    не N round-trips); `/infobases` — COUNT + два INNER JOIN (`Tenants`,`Publications`); `/audit` —
    COUNT + `ORDER BY [Timestamp] DESC,[Id] DESC OFFSET/FETCH` с опц. фильтрами; `/sessions/snapshot` —
    **0 EF-команд** (читает in-memory `IActiveSessionSnapshotStore`).
  - **Warm-тайминги (EF DbCommand, мс):** все sub-ms кроме `/audit` без фильтра COUNT **5→43** мс и
    `/audit?tenantId=` страница **10→8** мс (данные в buffer cache → кэшонезависимая метрика = logical
    reads плана).
  - **План `AuditLogs`** (индексы: три одноколоночных + `PK(Id)`; составного нет):

  | Запрос | baseline 100k | ×10 1M |
  | --- | --- | --- |
  | Страница без фильтра (`ORDER BY Timestamp DESC`) | Top + Scan `IX_Timestamp` + lookup — **130** reads | то же — **129** (не растёт) |
  | Страница `TenantId`-only | Clustered Scan + **Sort** — **2241** | `IX_TenantId` Seek + **Sort** + lookup — **7480** + **Missing Index impact 69.3%** |
  | Страница `ActionType`+`TenantId`+range (селективно) | Index Seek — **12** | — **6** |
  | `COUNT(*)` без фильтра | Index Scan — **499** | — **4461** (линейно) |

- **Вывод (вход в PERF-06/07).** Дорогой и растущий паттерн — **фильтр-список аудита по неселективному
  `TenantId` + `ORDER BY Timestamp DESC, Id DESC`**: Sort + key lookup, logical reads 2241→7480, SQL сам
  выдаёт missing-index (ключ `TenantId`, impact 69%). **PERF-06:** составной индекс
  `(TenantId, Timestamp DESC, Id DESC)` (и аналог `(ActionType, …)`) убирает Sort и lookup. `COUNT(*)`
  без фильтра растёт линейно (присуще offset-пагинации; watch-item). Список без фильтра уже эффективен
  (`IX_Timestamp` + `Top`). `/tenants` correlated COUNT — не N+1 (подтверждено). PERF-07 (`DriftCheckJob`)
  — фоновый джоб, снимается тем же профилем при прогоне дрейфа.
- **Прод 1:1 при выключенном флаге (DoD).** Рантайм-проверка: backend без `Diagnostics__*` —
  стартует и обслуживает, в stdout **0** строк `Executed DbCommand`/`RelationalEventId.CommandExecuted`
  (включая логин+`/audit`), `ef-profile.log` не создаётся.
- **Locked-границы:** запросы не менялись (только наблюдение); ADR-20 (vertical-slice, без use-case-слоя),
  single-node, ADR-16/3.3, ADR-21, RU-only не затронуты; уровень логов прода (`Database.Command=Warning`)
  не тронут.
- **Файлы:** новые `backend/src/MitLicenseCenter.Infrastructure/Diagnostics/EfQueryProfiling.cs`,
  `backend/tests/MitLicenseCenter.Tests.Unit/Diagnostics/EfQueryProfilingTests.cs`; правки
  `backend/src/MitLicenseCenter.Infrastructure/DependencyInjection.cs`,
  `backend/src/MitLicenseCenter.Web/appsettings.Development.json`, `docs/OPERATIONS.md`.
- **Прогон:** `scripts/build.ps1` зелёный целиком — backend `restore`/`build` (0 warnings,
  `TreatWarningsAsErrors`)/`test` (295 — +7 EfQueryProfiling)/`format` (без правок); frontend
  `lint`/`type-check`/`test` (118)/`build` ОК.

### MLC-040 (PERF-04) — Readiness-проба зависимостей: `/api/v1/health/ready` — 2026-06-05

- **Проблема:** `GET /api/v1/health` анонимен и возвращал только `{status, version, utcNow}` — не
  проверял ни одной зависимости. «Процесс жив» было неотличимо от «БД недоступна / RAS в Сбое /
  Hangfire-сторадж лёг»: оператор и нагрузочные замеры (перф-трек) не имели машинно-читаемого сигнала
  готовности. Последний пункт Phase 1 перф-трека (план `compressed-giggling-grove.md`, PERF-04).
- **Развилки (решены).**
  - **Liveness vs readiness — разделены.** `/health` остался дешёвым liveness **1:1** (процесс отвечает
    ⇒ жив; без зависимостей — контракт уже могут дёргать мониторинг/оркестратор). Добавлен отдельный
    `GET /api/v1/health/ready` с пробами.
  - **Гейтинг (решение куратора):** **только БД гейтит `not_ready`/`503`**. RAS-`Сбой` и Hangfire-`down`
    → `degraded` на `200` (single-node: снимать узел из-за RAS бессмысленно — это уронит и сам Dashboard,
    где оператор видит ошибку RAS).
  - **Транспорт (решение куратора):** `503` при `not_ready`, `200` при `ready`/`degraded`.
  - **Анонимность/раскрытие (ADR-4.1 / MLC-009):** endpoint анонимный (нужен probe-инструментам без
    аутентификации), но **санитизирован** — только грубые суб-статусы, без путей/имён серверов/текстов
    исключений и `RasHealthSnapshot.LastErrorMessage`; полное исключение пробы → в журнал сервера
    (source-gen logger, как в Discovery).
- **Что сделано.**
  - Контракт `GET /api/v1/health/ready` (`AllowAnonymous`, тег `Health`, v1.0, тот же `versionSet`):
    `{ status: ready|degraded|not_ready, utcNow, checks: { database: ok|down, ras: ok|degraded|unknown,
    hangfire: ok|down } }`.
  - Три read-only пробы под общим таймаутом 2с (linked-CTS / `WhenAny`):
    **БД** — `db.Database.CanConnectAsync` (единственная зависимость, гейтящая `not_ready`/`503`);
    **RAS** — чистое чтение снапшота `IRasHealthReader.GetSnapshot()` (тот же 30с-пробер, что и карточка
    Dashboard) → `ok`/`degraded`(Сбой)/`unknown`(первые 30с); **никакого нового спавна `rac.exe`**
    (ADR-16/3.3, спавн-бюджет); **Hangfire** — `JobStorage.GetMonitoringApi().GetStatistics()` (синхрон
    уведён в пул и ограничен таймаутом).
  - Чистый агрегатор `ReadinessEvaluator.Evaluate(db, ras, hangfire) → (overall, http)` вынесен из
    хендлера → вся матрица решений покрыта юнит-тестами без реально лежащих зависимостей.
  - Санитизация: тело несёт только enum-строки; `LogDatabaseProbeFailed`/`LogHangfireProbeFailed`
    (source-gen) пишут полное исключение на сервер.
  - **13 тестов** (`Endpoints/ReadinessEvaluatorTests.cs` — 8 матричных `[Theory]`;
    `Endpoints/HealthReadyEndpointTests.cs` — 5: happy-path `ready`/200; RAS-`Сбой`→`degraded`/200 +
    проверка что текст ошибки RAS **не утекает** в тело; RAS-`unknown`→`ready`; Hangfire-`down`→
    `degraded`/200; disposed-контекст→`down`/`not_ready`/503).
- **Замер «до→после» (DoD).** Backend (Release) в Development, живой SQL (`Server=.`), `dotnet-counters`
  на `MitLicenseCenter.Rac`:
  - **БД доступна:** `/health/ready` → `200`, `checks.database=ok`, `hangfire=ok` (Hangfire SQL-objects
    установлены, сторадж читается); `ras=degraded` (в dev `OneC.RAS.ExePath` не задан → пробер фиксирует
    Сбой), overall `degraded`/`200` — суб-статусы корректно различаются.
  - **БД недоступна:** `503`, `status=not_ready`, `database=down` — подтверждено детерминированным
    юнит-тестом (disposed-контекст; на живом инстансе не воспроизводится без остановки общего SQL-сервиса
    — fail-fast ADR-18 не даёт стартовать против опущенной БД).
  - **RAS-`Сбой`:** живьём `ras=degraded` при `database=ok` → `degraded`/`200` (не гейтит).
  - **Спавны `rac.exe` (ключевой DoD):** **650+ health-запросов** (`/health/ready`×400 в одном окне +
    предыдущие серии) при работающем коллекторе → инструмент `rac.exe.spawns` **не дал ни одной записи**
    (Meter молчит ⇒ ноль инкрементов). Контроль: в том же окне `System.Runtime` дал десятки сэмплов —
    коллектор заведомо живой, т.е. пустота по `Rac` = именно ноль спавнов, а не сломанный сбор. Лог
    подтверждает: пробер падает на «`OneC.RAS.ExePath` не задан» ещё до спавна. Health-запросы спавнов
    не порождают — структурно (хендлер читает только снапшот), эмпирически (счётчик 0) и в юнит-тестах
    (substitute `IRasHealthReader`, без реального клиента).
  - **Liveness 1:1:** `/health` → `200 {status:"ok", version, utcNow}`, без изменений.
- **Locked-границы:** ADR-16/3.3 (RAS только через готовый снапшот, ни одного нового `rac.exe`),
  ADR-18 fail-fast (тронут только runtime-роут, стартовый контракт не менялся), ADR-22 (добавлен только
  роут, middleware/HSTS/redirect не тронуты), ADR-20/single-node/RU-only — не затронуты; liveness `/health`
  не редактировался.
- **Файлы:** новые `backend/src/MitLicenseCenter.Web/Endpoints/Health/HealthContracts.cs`,
  `…/Health/ReadinessEvaluator.cs`,
  `backend/tests/MitLicenseCenter.Tests.Unit/Endpoints/ReadinessEvaluatorTests.cs`,
  `…/Endpoints/HealthReadyEndpointTests.cs`; правки `…/Endpoints/Health/HealthEndpoints.cs`,
  `docs/04_INFRASTRUCTURE.md`, `docs/OPERATIONS.md`.
- **Прогон:** `scripts/build.ps1` зелёный целиком — backend `restore`/`build` (0 warnings,
  `TreatWarningsAsErrors`)/`test` (**308** — +13 readiness)/`format` (без правок); frontend
  `lint`/`type-check`/`test` (118)/`build` ОК.

### MLC-041 (PERF-05) — Кросс-вызовный кэш резолва UUID кластера — 2026-06-05

- **Проблема:** `RacExecutableRasClusterClient.ResolveClusterUuidAsync` спавнил отдельный
  `rac.exe cluster list` **перед каждым** `session list` / `session terminate` /
  `infobase summary list` — кросс-вызовного кэша UUID не было. Клиент `Scoped`, а
  `HotTierPollingService`/`ReconciliationJob` создают новый scope на каждый тик → field-кэш не пережил
  бы тик. Итог: kill-путь 2 спавна вместо 1; hot-поллинг удваивал спавны (`cluster list` + `session list`
  каждый тик). Найдено через замерную базу Phase 1 (метрики MLC-037, харнесс MLC-039: спавн-бюджет
  каденционно-ограничен → срез спавнов на вызов = прямой выигрыш). PERF-05 плана
  `compressed-giggling-grove.md`.
- **Опора:** single-node (Locked) → ровно один кластер (`records[0]` из `cluster list`), UUID стабилен
  между вызовами → кэш тривиально безопасен.
- **Развилки (решены).**
  - **Где жить кэшу — singleton, не scoped-поле.** Чтобы убрать `cluster list` на hot-поллинге *между
    тиками* (каждый тик = новый scope), кэш обязан пережить scope → новый singleton `IClusterUuidCache`
    (как `IRacProcessRunner`), инжектится в scoped-адаптер.
  - **Ключ = `(ExePath, Endpoint)` без creds.** `cluster list` идёт через `BuildArgs` (без auth),
    идентичность кластера от creds не зависит → creds в ключ не входят (минимизация инвалидации).
  - **Инвалидация stale-UUID — на non-zero exit, не на матч строки.** Локализованный текст rac.exe
    («unknown cluster») хрупок; non-zero exit cluster-scoped команды — наблюдаемый сигнал и так, а
    `Invalidate` безопасна (в худшем случае один лишний `cluster list` на следующем вызове, самоисцеление).
  - **`AlreadyGone` НЕ инвалидирует.** Маркер «Сеанс … не найден» — успешный идемпотентный no-op, не
    ошибка кластера → кэш переживает (иначе каждый kick уже-ушедшего сеанса ронял бы кэш).
- **Что сделано.**
  - Новый `IClusterUuidCache` (+ `readonly record struct ClusterUuidKey(ExePath, Endpoint)`) в
    `Application/Clusters/`; реализация `ClusterUuidCache` (Infrastructure, **singleton**) — один слот под
    `lock`, БЕЗ удержания лока через `await` (спавн `cluster list` вне лока; редкий двойной резолв на
    cold-старте допустим и самоисцеляется). Метод публикации назван `Store` (не `Set` — CA1716).
  - `ResolveClusterUuidAsync`: сначала `TryGet(key)` (хит → 0 спавнов), иначе резолв + `Store`
    **только на успехе** (неуспех/пустой список/пустой `cluster` не кэшируются). `BuildClusterKey`
    пересобирает ключ из TTL-снапшота настроек на каждом вызове.
  - Safety-net `Invalidate(BuildClusterKey(exePath))` в трёх ветках `ExitCode != 0`
    (`session list` / `session terminate` финальный fail / `infobase summary list`); в `AlreadyGone`
    инвалидации нет.
  - Смена `OneC.RAS.Endpoint`/`ExePath` инвалидирует автоматически: `SettingsStore.SetAsync` →
    `Invalidate()` снапшота → новый `GetString` → пересобранный ключ не совпадает → промах → перерезолв.
    Отдельного хука на событие смены настроек не нужно.
  - DI: `services.AddSingleton<IClusterUuidCache, ClusterUuidCache>()` рядом с `IRacProcessRunner`.
- **Замер «до→после» (DoD).** Детерминированный воспроизводимый харнесс сценария MLC-039
  (`ClusterUuidCacheSpawnMeasurementTests`): реальный адаптер + counting-`IRacProcessRunner`,
  классификация спавнов тем же `RacCommandTag`, что и метрика `rac.exe.spawns` (MLC-037). «До»
  воспроизведено тем же бинарём адаптера с `NullClusterUuidCache` (`TryGet`→false на каждый вызов =
  доформенное поведение байт-в-байт). Сценарий hot=15 тиков (свежий scoped-адаптер на тик, общий кэш) +
  cold-цикл (2 list: snapshot+re-fetch) + 5 kills (sustained over-limit):

  | tag | before | after |
  |---|---|---|
  | `cluster.list` | 22 | **1** |
  | `session.list` | 17 | 17 |
  | `session.terminate` | 5 | 5 |
  | **TOTAL spawns** | **44** | **23** |
  | spawns / kill | 2.0 | **1.0** |
  | spawns / hot-tick | 2.0 | **1.0** |

  `cluster.list`-спавны рухнули 22→1; kill-путь 2→1/kill; hot-поллинг 2→1/тик (×2). `session list`/
  `terminate` 1:1 — кэш режет только `cluster list`.
- **Жёсткие границы.** Идемпотентный kill 1:1 — `KillEnforcer` re-fetch + сверка дескриптора
  (`ClusterInfobaseId`/`AppId`/`StartedAt`) не тронут; кэш влияет только на аргумент `--cluster=`,
  не на сверку; маркер «Сеанс … не найден» → `AlreadyGone`, без инвалидации (покрыто тестом). Новый
  адаптер/транспорт НЕ введён (ADR-16/3.3; `ClusterUuidCache` — внутренний кэш того же `rac.exe`-адаптера,
  не RAS Strategy B = MLC-036). Поведение при недоступном RAS 1:1 (`cluster list` падает → null до кэша,
  ничего не кэшируется). Потокобезопасность — singleton под `lock`, дёргается из cold-джоба и hot-сервиса.
  ADR-21/20/single-node/RU-only не затронуты.
- **Тесты (+7).** `RacExecutableRasClusterClientTests` (+4): кэш-хит не спавнит повторный `cluster list`;
  инвалидация при смене endpoint; инвалидация при ошибке последующей команды; `AlreadyGone` не
  инвалидирует. `ClusterUuidCacheSpawnMeasurementTests` (+3): таблица до→после (регресс-гард на
  `cluster.list`/TOTAL), kill-путь 2→1, hot-тик 2→1. Существующие 18 адаптерных + smoke — без регрессий
  (конструктор получил 4-й параметр; в тестах — фабричный helper `BuildClient`).
- **Канон (present-tense).** `DECISIONS.md` ADR-3.3 (строка `ListActiveSessionsAsync` + «Spawn cadence»),
  `04_INFRASTRUCTURE.md` §1 (get-sessions + спавн-бюджет), шапка-комментарий
  `RacExecutableRasClusterClient.cs` — отражают кросс-вызовный кэш и ×2-срез спавн-каденции.
- **Файлы:** новые `backend/src/MitLicenseCenter.Application/Clusters/IClusterUuidCache.cs`,
  `…/Infrastructure/Clusters/ClusterUuidCache.cs`,
  `backend/tests/…/Clusters/ClusterUuidCacheSpawnMeasurementTests.cs`; правки
  `…/Infrastructure/Clusters/RacExecutableRasClusterClient.cs`, `…/Infrastructure/DependencyInjection.cs`,
  `…/tests/…/Clusters/RacExecutableRasClusterClientTests.cs`, `…/Clusters/RacExecutableSmokeTests.cs`,
  `docs/DECISIONS.md`, `docs/04_INFRASTRUCTURE.md`.
- **Прогон:** `scripts/build.ps1` зелёный целиком — backend `restore`/`build` (0 warnings,
  `TreatWarningsAsErrors`)/`test` (**315** — +7)/`format` (без правок); frontend
  `lint`/`type-check`/`test` (118)/`build` ОК.

### MLC-042 (PERF-06) — Составной индекс под фильтр+сортировку `AuditLogs` — 2026-06-05

- **Проблема:** `AuditLogs` — единственная неограниченно растущая таблица (живёт до ретенции,
  default 365 дней). Снятый план запроса (MLC-038/PERF-02) доказал: фильтрованный список `/audit`
  по неселективному `TenantId` + `ORDER BY Timestamp DESC, Id DESC` (`AuditEndpoints.ListAsync`)
  вынуждает **Sort + key lookup**, logical reads растут с таблицей (100k 2241 → 1M 7480), а
  SQL-оптимизатор сам просит missing index (impact ~69%). Индексы были три одноколоночных
  (`IX_AuditLogs_Timestamp`, `_ActionType`, `_TenantId`) + кластерный `PK_AuditLogs(Id)`; составного
  под «фильтр + сортировка по Timestamp с tie-break по Id» не было. PERF-06 плана
  `compressed-giggling-grove.md`.
- **Решение:** один составной индекс `IX_AuditLogs_TenantId_Timestamp_Id`
  `(TenantId ASC, Timestamp DESC, Id DESC)` (`AppDbContext` `HasIndex(...).IsDescending(false, true,
  true)`, миграция `MLC042AuditLogCompositeIndex`). Ключ совпадает с порядком запроса → **Sort
  исчезает** (хранение = порядок), фильтр по `TenantId` идёт **Index Seek**, а key lookup за
  остальными колонками (`Reason`/`Initiator`/`Description`/`ActionType`) ограничен **размером
  страницы** (Top 50), а не числом совпадений → logical reads перестают расти с таблицей.
- **Развилки (решены замером, не по умолчанию).**
  - **Сколько индексов — только `TenantId`-вариант.** `ActionType`-only список планом **не**
    подтверждён дорогим: на 1M он едет по упорядоченному `IX_AuditLogs_Timestamp` с ранним `Top`
    (оператор `Top`, **не** Sort; нет missing-index-подсказки; reads ~1100, на порядок ниже
    `TenantId`-only 8244). Симметричный `(ActionType, …)` не введён — лишний индекс удорожил бы
    **каждый** INSERT аудита без доказанной пользы (минимально достаточный набор).
  - **Судьба одноколоночных.** `IX_AuditLogs_TenantId` (создавался конвенцией FK) — **удалён** той
    же миграцией: составной с лидирующим `TenantId` полностью покрывает FK/equality-seek (план после:
    TenantId-COUNT идёт Index Seek по композиту). Это держит **число индексов на запись нейтральным**
    (4 → 4: −1 single, +1 composite). `IX_AuditLogs_Timestamp` — **оставлен** (обслуживает retention
    `DELETE TOP (5000)` по Timestamp и список без фильтра). `IX_AuditLogs_ActionType` — **оставлен**
    (см. выше, композит не вводился).
  - **INCLUDE-колонки (covering) — НЕ добавлены.** Доминирующую стоимость (Sort) снимает уже сам
    ключ; остаточный lookup ограничен страницей и не растёт. `Description` — `nvarchar(max)`;
    включение раздуло бы индекс и ударило по частому INSERT. Covering-выигрыш не оправдан.
- **Замер «до→после» (Perf-БД `MitLicenseCenter_Perf`, тот же seed-харнесс MLC-039; «до» снято на
  старой схеме, «после» — после применения миграции на тех же данных; STATISTICS IO + Actual Plan):**

  | Запрос (`OFFSET 0 FETCH 50`) | 100k до | 100k после | 1M до | 1M после |
  | --- | --- | --- | --- | --- |
  | список **`TenantId`-only** (целевой) | **2239** | **166** | **8244** | **165** |
  | список **`ActionType`-only** | 1113 | 1113 | 1099 | 1108 |
  | список **без фильтра** | 165 | 165 | 156 | 165 |
  | **`COUNT(*)` `TenantId`-only** | 16 | 27 | 21 | 19 |

  - **Целевой запрос:** план `до` = TopN **Sort** + Nested Loops + Index Seek `IX_AuditLogs_TenantId`
    (21) + **Clustered Index Seek / key lookup всех совпадений (7703)** + MissingIndex impact 69.9%;
    план `после` = **`Top`** (Sort нет) + Index Seek `IX_AuditLogs_TenantId_Timestamp_Id` (3) + key
    lookup, ограниченный 50 строками (126) = 165. **8244 → 165 (~50×)**, и не растёт с таблицей
    (`до` растёт 2239→8244, `после` плоско ~165).
  - **Список без фильтра — 1:1, не регресс:** план `после` по-прежнему `Index Scan` по
    `IX_AuditLogs_Timestamp` + `Top` (156→165 — шум key-lookup'а 50 строк, тот же план).
  - **Удаление `IX_AuditLogs_TenantId` безопасно:** `TenantId`-COUNT после идёт Index Seek по
    композиту (16→27 на 100k / 21→19 на 1M — оба тривиальны; композит шире на Timestamp+Id, отсюда
    ±; COUNT — watch-item offset-пагинации, индексом не адресуется).
  - **INSERT-стоимость осознана:** число индексов на запись не выросло (своп single↔composite), без
    INCLUDE.
- **Граница объёма (не трогалось):** `COUNT(*)` **без** фильтра растёт ~линейно (присуще
  offset-пагинации, не адресуется индексом) — watch-item, покрывающих индексов ради COUNT не
  плодили.
- **Гоча миграции:** `dotnet ef migrations add` сгенерил `.cs` в UTF-8 BOM + CRLF; нормализованы
  три файла (новый `_MLC042AuditLogCompositeIndex.cs`, его `.Designer.cs`,
  `AppDbContextModelSnapshot.cs`) → UTF-8 без BOM, LF (эталон — существующие миграции);
  `dotnet format --verify-no-changes` в `build.ps1` прошёл.
- **Файлы:** `…/Persistence/AppDbContext.cs` (HasIndex составного),
  `…/Persistence/Migrations/20260605004020_MLC042AuditLogCompositeIndex.cs` (+ `.Designer.cs`),
  `…/Persistence/Migrations/AppDbContextModelSnapshot.cs`, `docs/03_DOMAIN_MODEL.md` (§4 индексы
  AuditLog, present-tense).
- **Жёсткие границы:** запросы `AuditEndpoints.ListAsync` 1:1 (менялась только схема индексов);
  retention-семантика (`DELETE TOP (5000)` по Timestamp) и enum int-стабильность
  (`AuditActionType`/`AuditReason`) не тронуты; Locked-ADR-20/16/3.3/single-node/RU-only не
  затронуты.
- **Прогон:** `scripts/build.ps1` зелёный целиком — backend `restore`/`build`/`test` (**316**,
  контрактные SQLite-тесты persistence зелёные — новый индекс не ломает схему)/`format` (без правок);
  frontend `lint`/`type-check`/`test` (**118**, 21 файл)/`build` ОК.

### MLC-043 (PERF-07) — Батч-загрузка публикаций в `DriftCheckJob` (N+1 → 1) — 2026-06-05

- **Проблема:** `DriftCheckJob.RunAllAsync` грузил все `Publication.Id` одним запросом, затем
  `foreach (id) → CheckOneCoreAsync(id)` делал **отдельный** `FirstOrDefault`-запрос на каждую
  публикацию — N+1 round-trips на проход джоба (раз в 5 мин, через ту же scoped-БД). Снимается
  замерной базой Phase 1 (EF-профиль MLC-038, харнесс MLC-039). PERF-07 плана
  `compressed-giggling-grove.md`.
- **Опора:** проверка дрейфа читает публикацию по фиксированному набору полей (Site/VirtualPath/
  PhysicalPathOverride для `ReadActualState`; PlatformVersion/OData/Http для `Compare`; InfobaseId
  для tenant-строки аудита; предыдущие `LastDriftStatus`/`Details` для перехода) — всё проецируемо
  одним запросом; тяжёлый `VrdCustomXml` (nvarchar(max)) проверке не нужен.
- **Развилки (решены).**
  - **Форма загрузки — проекция в лёгкий `record`, не entity.** EF не проецирует в tracked
    entity-тип; `AsNoTracking().Select(p => new DriftSnapshot(...))` грузит **все** публикации одним
    запросом строго нужных колонок (без `VrdCustomXml`, без tracking-снимков на весь объём).
  - **Запись результата 1:1, провайдер-независимо.** `ExecuteUpdate` отпал — EF InMemory-провайдер
    (на нём крутятся drift/reconcile unit-тесты) его не транслирует. Выбран targeted-UPDATE по `Id`
    через change-tracker: если сущность уже трекается в текущем scope (reconcile-endpoint в том же
    `DbContext`) — мутируем её; иначе `Attach` транзиентного `desired` и помечаем `Modified` **только
    три** drift-колонки. На relational это `UPDATE … SET LastDrift{Status,CheckAt,Details} WHERE Id=@id`
    — байт-в-байт прежний эффект `SaveChanges`; на проде (MSSQL) — корректный частичный апдейт.
  - **`Local`-проверка перед `Attach`.** Защищает от конфликта identity, когда строка уже трекается
    (тест-сид `Add`+`SaveChanges`; reconcile читает `AsNoTracking`, поэтому в проде attach-ветка).
- **Что сделано.**
  - `RunAllAsync`: `LoadSnapshotsAsync(p => true)` → один проекционный `AsNoTracking`-запрос →
    `foreach` по материализованному списку → `ProcessOneAsync(snapshot)`. Внешний `try/catch`
    (rethrow `OperationCanceledException`, generic → `LogDriftRunFailed`) и `throttle` — без изменений.
  - `CheckOneAsync` (on-demand путь reconcile/check-drift) переведён на тот же снимок для **одной**
    публикации (`LoadSnapshotsAsync(p => p.Id == id)`), затем `ProcessOneAsync` — единая логика, без
    дублирования.
  - `ProcessOneAsync`: строит транзиентный `desired` для `ReadActualState`/`Compare` (те же входные
    поля, что читал tracked-entity), пишет результат targeted-UPDATE'ом (`Local`-аware mutate/attach),
    tenant-lookup и audit-строка — **только** при `IsAuditableTransition` (как раньше). Порядок:
    capture prev → compare → write 3 поля → решить аудит → `SaveChanges` → аудит. 1:1 с прежним.
  - `per-publication ct.ThrowIfCancellationRequested()` сохранён; устойчивость к ошибке отдельной
    публикации **1:1** — сверено с текущим поведением: внешний `try/catch` обёрнут вокруг всего
    `foreach`, одна сбойная публикация и раньше обрывала проход (новый per-item `try/catch` не
    добавлялся, чтобы не менять семантику).
- **Замер «до→после» (DoD).** Реальные SQL round-trip'ы посчитаны `DbCommandInterceptor` на
  relational-провайдере (SQLite — та же EF-трансляция, что у MSSQL; число запросов
  провайдер-независимо), на засеянном объёме N=25 публикаций. «До» воспроизведено in-line на тех же
  данных (старая форма `Select(Id).ToList` + `foreach FirstOrDefault`):

  | загрузочных SELECT/проход | before | after |
  |---|---|---|
  | публикации | **26** (= N+1) | **1** |

  Запись (N targeted-UPDATE) и tenant/audit-запросы (только на переходах) — вне дельты, не менялись.
- **Идентичность результатов дрейфа.** 20 существующих drift+reconcile unit-тестов
  (`DriftCheckTransitionAuditTests`, `PublicationsReconcileTests`) зелёные без правок: статусы
  (InSync/Drift/Missing/Error), `LastDriftDetails`, аудит-переходы (210 только на смене статуса в
  не-InSync; Drift→Drift same-details = 0 строк; Drift→InSync джобом не аудитится; tenantId на Missing)
  — все 1:1 (ADR-4.1).
- **Жёсткие границы.** `VrdPathResolver`/`PublicationDriftDetector`/`IsAuditableTransition` и семантика
  записи `LastDrift*` не тронуты — менялась только форма загрузки и запись. ADR-4.1 drift-контракт 1:1.
  ADR-20 (vertical-slice, без use-case-слоя), ADR-16/3.3, single-node, RU-only не затронуты. Smoke
  не сломан (`DriftCheckJob` — Windows-IIS-зависимый адаптер не трогался; джоб-логика провайдер-агностична).
- **Тесты (+1).** `DriftCheckBatchQueryTests` — регресс-гард: один проход `RunAllAsync` грузит публикации
  ровно **1** запросом (N+1→1), echo-stub IIS даёт InSync (изолирует загрузку+запись от аудита).
  Хелпер `TestHelpers.SqliteTestDb.Create` расширен опц. `params IInterceptor[]` (для counting-перехватчика).
- **Канон (present-tense).** `docs/OPERATIONS.md` «Наблюдаемость перфа» — пункт PERF-07 переведён в
  закрытое состояние с результатом 26→1.
- **Файлы:** правки `backend/src/MitLicenseCenter.Infrastructure/Jobs/DriftCheckJob.cs`,
  `backend/tests/MitLicenseCenter.Tests.Unit/Endpoints/TestHelpers.cs`, `docs/OPERATIONS.md`; новый
  `backend/tests/MitLicenseCenter.Tests.Unit/Jobs/DriftCheckBatchQueryTests.cs`.
- **Прогон:** `scripts/build.ps1` зелёный целиком — backend `restore`/`build` (0 warnings,
  `TreatWarningsAsErrors`)/`test` (**316** — +1)/`format` (без правок); frontend
  `lint`/`type-check`/`test` (118)/`build` ОК — «Все шаги пройдены успешно».

### MLC-044 — Hot-тир тоже enforce'ит (near-realtime kill ≤5с) + быстрый экран — 2026-06-05

- **Проблема.** Kill превышающих сессий выполнял **только** cold-цикл: `ReconciliationJob.RunColdAsync`
  в конце звал `IKillEnforcer.EnforceAsync` (троттлинг `Polling.ColdIntervalSeconds`, дефолт 25с).
  Быстрый hot-цикл (`HotTierPollingService`, `Polling.HotIntervalSeconds`, дефолт 4с) только обновлял
  in-memory снимок для UI — **не enforce'ил**. Следствия: (1) реакция на превышение у клиента, уже
  находящегося в hot-тире (at-risk, ≥90%), = до ~25с вместо ≤5с; (2) внутреннее противоречие канона —
  ADR-6 и `02_ARCHITECTURE_REQUIREMENTS.md` обещали «enforcement window ≤ 5s для at-risk», а ADR-6.1
  и код говорили «kill только на cold-цикле, hot не убивает». Постановка куратора перф-этапа
  (`compressed-giggling-grove.md`); вне PERF-каталога.
- **Часть A — hot тоже enforce'ит.** `HotTierPollingService`: тело одного тика вынесено в
  `internal RunCycleOnceAsync(cluster, enforcer, ct)` (детерминированный seam для теста, `ExecuteAsync`
  резолвит `IClusterClient` + `IKillEnforcer` из scope и зовёт его в while-loop). После единственного
  `ListActiveSessionsAsync` тик строит overlay (UI, как раньше) и затем вызывает
  `EnforceAsync(hotPayload, freshSessions, ct)` строго по hot-тенантам (базис = `freshEntries`, чисто
  свежие денорм. строки hot-тенантов; over-limit `Consumed>Limit` ⊆ hot `≥90%`, поэтому non-hot строки
  kills не дают). Кадр kill теперь = hot-каденция (~4с) для уже-горячих, реализуя обещанное ADR-6 окно.
- **Переиспользование fresh-списка (спавн-бюджет, ADR-3.3).** `IKillEnforcer.EnforceAsync` получил
  параметр `IReadOnlyList<ClusterSession>? freshSessions`: `null` → enforcer делает свой re-fetch
  (cold-путь, профиль спавнов 1:1); hot передаёт **уже полученный тиком список** → второго
  `ListActiveSessionsAsync` нет. Семантика kill (re-fetch+сверка `(ClusterInfobaseId,AppId,StartedAt)`,
  аудит только на `Killed||AlreadyGone`, newest-first, `MaxKillsPerCycle=20`, ранний выход при
  `overLimitTenants.Count==0`) — **1:1**, переиспользована as-is.
- **Защита от over-kill (MLC-001) — общий замок.** До MLC-044 одновременность enforcement держал
  Hangfire-атрибут `[DisableConcurrentExecution]` на cold-джобе. Hot — `BackgroundService`, атрибут на
  него не действует → два пути enforcement (cold Hangfire + hot BackgroundService) могли пойти
  одновременно и дважды убить превышение. Введён singleton `IEnforcementGate`/`EnforcementGate`
  (`SemaphoreSlim(1,1)`, `AcquireAsync`→`IDisposable`-scope, идемпотентный double-dispose). **Оба пути
  берут замок** (caller-held): cold — вокруг `EnforceAsync(payload, null)`; hot — **до** fetch'а, на всё
  «fetch+overlay+enforce». Замок берётся до hot-фетча намеренно: единственный список тика читается под
  замком, и cold не может вклиниться между fetch и kill (иначе переиспользуемый список устарел бы →
  over-kill). Single-node, один процесс → in-process замка достаточно (распределённый Hangfire-лок
  покрывает лишь cold-vs-cold).
- **Часть B — быстрый экран.** FE: `useSessionsSnapshot` и `useDashboardSummary` — `refetchInterval`
  15с→**5с** (согласовано с hot-каденцией ~4с). Снимок дешёвый (in-memory, 0 EF, 0 спавнов `rac.exe`);
  dashboard-summary — несколько COUNT + in-memory RAS-health. Рост вызовов ×3 на двух дешёвых
  эндпоинтах при 5–20 пользователях пренебрежим. Прочие интервалы (publications 60с, drift 2/30с) не тронуты.
- **Замер «до→после» (DoD).** Детерминированный харнесс в духе MLC-041
  (`HotEnforcementMeasurementTests`, печатает таблицу), реальный путь `RunCycleOnceAsync`+`EnforceAsync`,
  прокси спавнов `rac.exe` = вызовы `IClusterClient` (тёплый UUID-кэш MLC-041: 1 спавн на `ListActive…`
  = `session.list`, 1 на `KillSession…` = `session.terminate`):

  | метрика | before | after |
  |---|---|---|
  | kill latency (at-risk) | ≈ `ColdIntervalSeconds` (≈25с) | ≤ `HotIntervalSeconds` (≈4с) |
  | `session.list` / hot-тик | 1 | **1** (переиспользован, НЕ 2) |
  | `session.terminate` / hot-тик при `Consumed==Limit` | 0 | **0** (ранний выход) |
  | hot steady-state спавны | — | ~15/мин ≤ ~26/мин (ADR-3.3) |

  `session.terminate` транзиентен: при sustained over-limit идёт N/тик, но в проде RAS убирает убитые
  → `Consumed==Limit` → 0 (подтверждено early-return-кейсом).
- **Тест отсутствия over-kill (ключевой).** `HotColdEnforcementOverKillTests`: stateful fake-кластер
  (потокобезопасный список; `KillSessionAsync` реально удаляет / иначе `AlreadyGone`; считает реальные
  удаления и terminate-вызовы), tenant limit=5 over на 4, заранее promote'нут в hot, общий
  `EnforcementGate`/snapshot-store/registry/потокобезопасный аудит, отдельные изолированные InMemory-БД
  для cold/hot-путей (read-only лимиты). `await Task.WhenAll(cold.RunColdAsync, hot.RunCycleOnceAsync)`
  → убито **ровно 4** (не 8), осталось 5 живых, terminate-вызовов **4** (второй вошедший видит
  `Consumed==Limit` и не вызывает terminate), аудит **4** записи (`SessionKilled`/`LimitExceeded`/`System`,
  без двойного аудита). Плюс `EnforcementGateTests` (взаимоисключение: ≤1 в секции; второй ждёт релиза;
  double-dispose идемпотентен).
- **Жёсткие границы.** Идемпотентный kill-протокол 1:1 (переиспользован, не изменён). Manual-kill
  `POST /sessions/{id}/kill` замок не берёт — убивает одну явно выбранную сессию (не считает over-limit),
  при гонке → `AlreadyGone`, не «enforcement loop». Новый кластерный адаптер **не введён** (ADR-16/3.3;
  не RAS Strategy B = MLC-036). Аудит-семантика (`SessionKilled`/`LimitExceeded`/initiator `System`,
  без двойного аудита) без изменений. ADR-20 vertical-slice, single-node, RU-only — соблюдены. EF-миграции
  нет (замок и enforce — чистый код, схема не меняется). Stub MLC-039 не модифицирован.
- **Канон (present-tense, расхождение ADR-6 ↔ ADR-6.1 закрыто).** `DECISIONS.md` ADR-6.1 переписан
  (hot живёт в BackgroundService **и enforce'ит**; общий `IEnforcementGate`; переиспользование fetch;
  убрана фраза «does not kill»), ADR-6 дополнен ссылкой на двутирный enforcement. `02_ARCHITECTURE_REQUIREMENTS.md`:
  Reconciliation Loop п.3 (Act) + «Concurrency Control» (замок общий для Hangfire cold-джоба и hot-BackgroundService).
  `04_INFRASTRUCTURE.md` §5: enforcement на обоих тирах под общим замком; hot переиспользует fetch (нет
  extra `session list`-спавна; terminate транзиентен); spawn-бюджет ADR-3.3 не изменился.
- **Файлы.** Новые: `Application/Jobs/IEnforcementGate.cs`, `Infrastructure/Jobs/EnforcementGate.cs`,
  тесты `Jobs/EnforcementGateTests.cs`, `Jobs/HotColdEnforcementOverKillTests.cs`,
  `Jobs/HotEnforcementMeasurementTests.cs`. Правки: `Application/Jobs/IKillEnforcer.cs`,
  `Infrastructure/Jobs/{KillEnforcer,ReconciliationJob,HotTierPollingService}.cs`,
  `Infrastructure/DependencyInjection.cs`, 5 существующих `Jobs/KillEnforcer*Tests.cs` (call-site `,null,`),
  FE `features/sessions/useSessionsSnapshot.ts`, `features/dashboard/useDashboardSummary.ts`, канон
  (DECISIONS/02/04), `PROJECT_BACKLOG.md`.
- **Прогон.** `scripts/build.ps1` зелёный целиком — backend `restore`/`build` (0 warnings,
  `TreatWarningsAsErrors`)/`test` (**322** — +6)/`format` (без правок); frontend
  `lint`/`type-check`/`test` (**119**)/`build` ОК — «Все шаги пройдены успешно».

---

### MLC-047 — Управление жизненным циклом IIS из веб-панели (recycle/start/stop пула, start/stop/restart сайта, iisreset) — 2026-06-06

- **Постановка (пользователь).** На странице «Публикации» — массовый (не per-row) блок управления IIS:
  кнопки перезапуска IIS и пула. В plan mode уточнены развилки: цель пула — **список пулов** (discovery),
  объём — **полный набор** (recycle/start/stop пула, start/stop/restart сайта, `iisreset`). Согласовано
  (`.claude/plans/polymorphic-percolating-waffle.md`).
- **Выбранный подход.** Новый Application-порт `IIisLifecycleService` (а не расширение
  `IIisPublishingService` — другая ось: хостинг vs публикация, server-scope vs per-publication; ADR-4
  read-only не размывается). Реализация `OneCIisLifecycleService` (Infrastructure, `[SupportedOSPlatform]`):
  пул/сайт через `ServerManager` (runtime-команды, без `CommitChanges`; idempotent start/stop по состоянию;
  объект не найден → `KeyNotFoundException`), `iisreset` (restart/`/stop`/`/start`) — спавн
  `…\System32\iisreset.exe` по образцу `OneCWebinstPublisher` (OEM/CP866-декод как `rac.exe`, 90s, kill-tree). Разрушительные операции
  сериализованы новым `IIisResetConcurrencyGate` (singleton `SemaphoreSlim(1,1)`). ADR-24 (+ уточнение
  ADR-4). Anti-corruption граница ADR-20 без изменений — `LayerBoundaryTests` уже покрывает новый адаптер.
- **Backend.** `Application/Publishing/{IIisLifecycleService,IIisResetConcurrencyGate}.cs`;
  `Infrastructure/Publishing/{OneCIisLifecycleService,IisResetConcurrencyGate}.cs` +
  `Testing/StubIisLifecycleService.cs`; DI-регистрация (scoped адаптер под `CA1416`, singleton замок);
  `Web/Endpoints/Iis/{IisEndpoints,IisContracts}.cs` (группа `/api/v1/iis/*`, discovery `Viewer`, мутации
  `Admin`, имя цели `[FromBody]`, union-результаты с маппингом 404/409, аудит на успехе, `tenantId=null`,
  серверный `Confirm`-гейт на `recycle`/`reset`/`stop`); `Program.cs` (`MapIisEndpoints`); аудит
  `AuditActionType` `220..228` + формулировки в `AuditDescriptions`; `Problems`/`ProblemCodes`
  (`IIS_OPERATION_FAILED`, `IIS_CONFIRM_REQUIRED`; `IIS_ACCESS_DENIED` переиспользован). Миграций нет
  (enum `HasConversion<int>` новых значений не требует).
- **Frontend.** `features/publications/iis/`: `iisTypes.ts`, `useIisManagement.ts` (queries
  `["iis","pools"]`/`["iis","sites"]` + 7 мутаций через `useInvalidatingMutation`), `IisConfirmDialog.tsx`
  (токен-подтверждение по образцу `PublishPublicationDialog`), `IisStateBadge.tsx`,
  `IisAppPoolsList.tsx`/`IisSitesList.tsx`, `IisManagementCard.tsx` (карточка **над списком публикаций**:
  бейдж статуса IIS (W3SVC) + `iisreset` restart + кнопка-переключатель stop/start (стоп — красный, старт —
  зелёный, как у пулов/сайтов) + списки пулов/сайтов; разрушительные → обычный confirm-диалог
  (`IisConfirmDialog`, без ввода токена; серверный `Confirm`-гейт — бэкстоп), start — сразу);
  статус сервера через новый порт-метод `GetServerStateAsync` (`ServiceController("W3SVC")`,
  пакет `System.ServiceProcess.ServiceController`), эндпоинт `GET /api/v1/iis/server`;
  подключение в `PublicationsPage.tsx`; i18n
  `publications.iis.*` + `audit.actions.Iis*`; `features/audit/types.ts` (union + `AUDIT_ACTION_TYPES`).
- **Прогон.** Backend `dotnet test` — **362** зелёных (+23 `IisEndpointsTests`). Frontend
  `type-check`/`lint`/`test` (**134**) ОК. Блок проверен в браузере (preview): карточка над таблицей,
  статус IIS + переключатель restart/stop/start, списки пулов/сайтов с состоянием, без ошибок в консоли.

---

### MLC-046 — Публикации: массовые операции (bulk publish + bulk change-platform) — 2026-06-05

- **Постановка (пользователь).** Поверх одиночных операций MLC-045 — массовый режим на странице
  «Публикации»: выбрать несколько публикаций чекбоксами и (1) опубликовать пачкой через `webinst`,
  (2) сменить им платформу пачкой (правка `web.config`, `default.vrd` не трогается). В проде ожидается
  ~100 публикаций, главный критерий — качество и надёжность. Согласовано в plan mode
  (`.claude/plans/lazy-puzzling-dusk.md`): развилки — где жить логике пачки, степень параллелизма,
  гейт перезатирания — закрыты ответами пользователя (надёжность важнее, параллелизм 2–3, единое
  подтверждение со списком).
- **Выбранный подход.** Фронт оркеструет **существующие** одиночные эндпоинты
  `POST /publications/{id}/publish|change-platform` пулом с малым параллелизмом (3): пачка = N
  идемпотентных одиночных вызовов. Надёжность = переиспользование протестированного пути MLC-045
  (гейт, аудит 212/213, refresh статуса, обработка ошибок) — без новой серверной orchestration-логики.
  Отвергнуты: один синхронный bulk-эндпоинт (~100×60с не влезает в HTTP-запрос, прогресс не виден до
  конца, новые контракты/тесты) и Hangfire-джоб (лишняя сложность без потребности — зафиксирован как
  **отложенная опция** в ADR-4, триггер — unattended/плановый mass-publish). Новых эндпоинтов,
  контрактов, миграций, enum-значений, правил валидации — **нет**.
- **Backend.** Единственное изменение — замок параллелизма спавнов webinst:
  `Application/Publishing/IWebinstConcurrencyGate.cs` + `Infrastructure/Publishing/WebinstConcurrencyGate.cs`
  (singleton `SemaphoreSlim(3,3)`, идемпотентный `Releaser` по образцу `EnforcementGate`). Инжектится в
  `OneCWebinstPublisher`, берётся `using (await gate.AcquireAsync(ct))` **вокруг `RunAsync`** (только
  спавна). На одиночный publish поведение 1:1 (свободный слот берётся мгновенно). Цель — кэп
  одновременных webinst независимо от вызывающего (два оператора / будущие потребители), реальная
  граница спавн-бюджета (семья ADR-3.3), а не клиентский лимит. Change-platform (лёгкая правка
  `web.config`) **не** гейтится. Регистрация singleton в DI рядом с `IWebinstPublisher`. Тест
  `WebinstConcurrencyGateTests` (3): кэп ≤ MaxConcurrency и полная утилизация под нагрузкой, блокировка
  сверх вместимости, идемпотентность double-dispose.
- **Frontend** (`features/publications/*` + shadcn `checkbox`). Generic пул-хук `useBulkOperation.ts`:
  пул заданного параллелизма, на каждый item — `runItem(id)`, статусы `pending/running/ok/error/skipped`,
  отмена (прекращает запуск новых; in-flight доезжают, остаток → `skipped`), `onComplete(states)` с
  итоговым снимком (без side-effect внутри setState — локальная мапа результатов). Пул-вызовы — прямой
  `api()` (тело — объект). `bulkErrors.ts` — маппинг `ApiError`→короткая RU-строка (409 detail /
  404 / 422-400). `bulkGating.ts` — pure `publicationsNeedingOverwriteConfirm` (Source≠Webinst &&
  Published). `BulkProgressView.tsx` — прогресс-бар (`components/ui/progress`) + сводка + построчный
  статус с деталью ошибки. `BulkPublishDialog.tsx` (единое подтверждение перезатирания со списком
  gated; все идут `confirm:true`) / `BulkChangePlatformDialog.tsx` (одна целевая версия из
  `usePlatformVersions` на всю выборку). `PublicationsBulkBar.tsx` — бар действий (виден при ≥1
  выбранном, admin-only). Таблица: лидирующая колонка-чекбокс (admin), header = выбрать все
  отфильтрованные (indeterminate), `data-state=selected`. `usePublicationsPage`: `selectedIds`
  (Set по id, переживает смену фильтра), `selectedPublications` из полного списка, toggle/all/clear,
  `deselectSucceeded` (снимает успешные — упавшие остаются для повтора). Диалоги во время прогона не
  закрываются; по завершении инвалидируют `publicationsQueryKey`. i18n — секция `publications.bulk`
  (RU-only). Тесты: `useBulkOperation` (5: все ОК, частичный успех, кэп параллелизма, отмена→skipped,
  onComplete-снимок), `bulkGating` (3), `bulkErrors` (5).
- **Аудит.** Без изменений: каждая публикация пишет свою запись `PublicationPublished=212` /
  `PublicationPlatformChanged=213` (пачка = N одиночных вызовов). Enum int-значения не трогались.
- **Известный компромисс (в каноне).** Прогон привязан к открытой вкладке (наблюдаемый деплой);
  прерывание безопасно (идемпотентность + снятие успешных из выделения → перевыбрать упавшие и
  повторить). На ~100 публикациях ≈ десятки минут (60с/webinst, 3 одновременно). Зафиксировано в
  ADR-4 / `04_INFRASTRUCTURE` / `05_UI_REQUIREMENTS` / `OPERATIONS`.
- **Проверка.** `scripts/build.ps1` (Release) зелёный целиком: **BE 329** (+3 теста замка),
  `dotnet format --verify-no-changes` чисто, **FE 132** (+13: пул-хук/gating/ошибки), lint/type-check/
  build — ОК. Канон present-tense: DECISIONS ADR-4 (+пакетный режим, отвергнут синхронный bulk),
  04 (concurrency cap webinst), 05 (bulk operations UI), OPERATIONS (поведение/throughput/tab-caveat).
  ADR-20/16/3.3/single-node/RU-only не затронуты. Миграций нет.

### MLC-045 — Публикации: webinst + смена платформы через web.config, отказ от drift-enforcement — 2026-06-05

- **Постановка (пользователь).** Текущая модель публикаций (PR 3.5 / ADR-4.1) — «эталон в БД →
  surgical-patch `default.vrd` → 5-мин drift-job → ручной reconcile» — не совпала с реальным процессом
  эксплуатации. Нужно: (1) новые ещё не опубликованные базы быстро публиковать через `webinst` (тонкую
  донастройку оператор при необходимости делает в конфигураторе), с **пометкой происхождения**; (2)
  смену версии платформы делать правкой **только** `web.config` (путь к `wsisapi.dll`), не перезаписывая
  `default.vrd`; (3) enforcement-reconcile и сравнение с эталоном — убрать. Согласовано в режиме
  планирования (`.claude/plans/typed-bubbling-beacon.md`): drift → **read-only статус**;
  OData/HTTP/VrdCustomXml — **убрать из панели** (webinst их не включает ключами, только через
  `-descriptor` vrd-шаблон); connstr для webinst — **авто** из настройки кластера + имя ИБ.
- **Факты, проверенные на стенде.** `webinst.exe` 8.5.1.1302 (`-?` справка): умеет только
  `-publish`/`-delete`, `-iis`/`-apache2x`, `-wsdir`/`-dir`/`-connstr`/`-descriptor`/`-confPath`/`-osauth`
  — **отдельных флагов OData/HTTP нет** (только через `-descriptor`). Вывод webinst — **UTF-16LE** (не
  CP866 как rac.exe). Путь к exe версионно-зависим (`…\1cv8\<версия>\bin\webinst.exe`). Версия платформы
  в современных сборках лежит в `web.config`, не в `default.vrd` (что и подтверждал комментарий старого
  `VrdPatcher`: для них vrd-патч был no-op).
- **Адаптер webinst (ADR-20).** `Application/Publishing/IWebinstPublisher.cs` +
  `Infrastructure/Publishing/OneCWebinstPublisher.cs`: путь к exe из `PlatformVersion` через
  `WebinstExeResolver`/`OneCInstallRoots` (тот же скан, что версии платформы — отдельная настройка пути
  не нужна); запуск процесса по образцу `SystemProcessRacRunner` (`ArgumentList`, таймаут 60с,
  `Kill(entireProcessTree)`), декод вывода **UTF-16LE**; аргументы (`WebinstArgs`, pure)
  `-publish -iis -wsdir <vdir> -dir <физ.путь> -connstr "Srvr=<кластер>;Ref=<имя ИБ>;"`; адрес кластера
  из новой настройки `OneC.Cluster.Server` (fallback — host из `OneC.RAS.Endpoint`). При ненулевом
  exit — сырой вывод в лог, наружу санитизированный detail (MLC-009).
- **Смена платформы (web.config, без webinst).** `IIisPublishingService.ChangePlatformAsync` в
  `OneCIisPublishingService`: правит **только** version-сегмент пути к `wsisapi.dll` в `web.config`
  (fallback — `default.vrd` для старых сборок), атомарно (temp + `File.Replace`); regex переиспользован
  из удалённого `VrdPatcher` → вынесен в pure-хелпер `WsisapiVersionRewriter`. `default.vrd`
  содержательно не трогается.
- **Read-only статус.** `PublicationActualState` переопределён (site/vdir/web.config exists + версия из
  wsisapi-пути, без OData/HTTP/VrdContent); pure-хелпер `PublicationStatusEvaluator` маппит факт в
  `Unknown/Published/NotPublished/Error`. `DriftCheckJob`→**`PublicationStatusRefreshJob`** (read-only,
  без сравнения с эталоном и **без аудита**); `DriftThrottleState`→`StatusRefreshThrottleState`. Удалены
  `PublicationDriftDetector`, `VrdPatcher`, `IDriftCheckJob`, `PublicationDriftStatus`. Рекуррент-джоб
  переименован `drift-check`→`publication-status-refresh` (cron из `Drift.IntervalMinutes`,
  `RemoveIfExists("drift-check")` при старте).
- **Эндпоинты.** `check-drift`/`drift-status`/`reconcile` удалены; добавлены
  `POST /publications/{id}/check` (синхронно читает факт, пишет `LastCheck*`),
  `POST /publications/{id}/publish` (webinst; **гейт**: не-`Webinst` и уже опубликованная → 409
  `PUBLISH_CONFIRM_REQUIRED` без `Confirm=true`; успех → `Source=Webinst`, аудит `PublicationPublished=212`;
  webinst-сбой → 409 `PUBLISH_FAILED`), `POST /publications/{id}/change-platform` (валидирует regex +
  установленность версии через `IPlatformVersionDiscovery`; IIS-сбой → 409 `IIS_RECONCILE_FAILED`/
  `IIS_ACCESS_DENIED`; успех → `PlatformVersion` обновлён, аудит `PublicationPlatformChanged=213`).
  `GET`/`PUT /{id}` сохранены (минус удалённые поля).
- **Домен/БД.** `Publication`: убраны `EnableOData`/`EnableHttpServices`/`VrdCustomXml`; добавлен
  `Source` (`PublicationSource` Unknown/Webinst/Configurator); drift-поля →
  `LastCheckStatus`(`PublicationPublishStatus`)/`LastCheckAt`/`LastCheckDetails`. Аудит-enum:
  `PublicationPublished=212`/`PublicationPlatformChanged=213` (210/211 — historical, frozen-int, не
  пишутся). Миграция `MLC045WebinstPublishing`: drop `EnableOData`/`EnableHttpServices`/`VrdCustomXml`/
  `LastDriftDetails`, rename `LastDriftStatus`→`LastCheckStatus`/`LastDriftCheckAt`→`LastCheckAt`, add
  `Source`/`LastCheckDetails`, `UPDATE … SET LastCheckStatus=0, LastCheckAt=NULL` (сброс в Unknown — смена
  семантики enum). EF-эвристика смапила переименования неверно (drift-статус→Source, VrdCustomXml→
  LastCheckDetails) — `Up`/`Down` переписаны вручную; файлы нормализованы (без BOM, LF — гоча).
  Настройка `OneC.Cluster.Server` (whitelist + сидер).
- **Frontend.** Таблица: убраны колонки OData/HTTP, добавлена «Источник» (бейдж webinst/Конфигуратор/—),
  «Состояние»→статус публикации; действия «Проверить сейчас» (read-only, без поллинга — `check`
  синхронный) / «Опубликовать» (`PublishPublicationDialog`, подтверждение токеном + предупреждение при
  `source!=Webinst`) / «Сменить платформу» (новый `ChangePlatformDialog`, select версий из
  `usePlatformVersions`). `usePublications`: `useCheckStatus`/`usePublish`/`useChangePlatform` вместо
  `useCheckDrift`/`useReconcile`. Форма инфобазы: убраны поля OData/HTTP из `validation.ts`/`types.ts`/
  `PublicationFieldset`/`useInfobaseForm` (parity с backend). i18n `publications.*` переписан
  (status/source/publish/changePlatform). URL-фильтр `driftStatus`→`status`.
- **connstr-аутентификация — рассмотрена и отклонена (2026-06-08, решение пользователя/куратора).**
  Ранее зафиксированный «открытый риск» (webinst мог бы требовать учётку в connstr) **закрыт без
  реализации**: в официальной документации 1С механизма передачи пароля кластера/ИБ в `webinst` нет,
  поэтому путь не реализуется и не вводится. webinst публикует без auth — это финальное решение, не
  временный MVP. Настройки `OneC.Cluster.AdminUser/Password` остаются исключительно за `rac.exe`
  (RAS-адаптер, флаги `--cluster-user`/`--cluster-pwd`) и к webinst отношения не имеют.
- **Жёсткие границы.** `webinst.exe`/`Microsoft.Web.Administration` — только в Infrastructure за
  интерфейсами (ADR-20; NetArchTest зелёный). Enum int-стабильность соблюдена (210/211 сохранены как
  historical). Single-node/RU-only не затронуты.
- **Канон (present-tense).** `DECISIONS.md`: ADR-4 переписан (webinst + web.config + read-only статус),
  **ADR-4.1 → [REVOKED]**, locked-constraint про drift заменён на «publication status». `04_INFRASTRUCTURE.md`
  §2 переписан (webinst/web.config/status, +настройка `OneC.Cluster.Server`, +execute на webinst.exe в
  правах), джоб-описание. `03_DOMAIN_MODEL.md` Publication + enum-слоты. `05_UI_REQUIREMENTS.md` §3.3 (три
  действия). `01_PROJECT_CONTEXT.md`, `ROADMAP.md`, `00_INDEX.md`, `OPERATIONS.md` IIS-раздел.
- **Файлы.** Новые: `Domain/Publications/{PublicationSource,PublicationPublishStatus}.cs`,
  `Application/Publishing/IWebinstPublisher.cs`, `Application/Jobs/IPublicationStatusJob.cs`,
  `Infrastructure/Publishing/{OneCWebinstPublisher,WebinstArgs,WebinstExeResolver,WsisapiVersionRewriter,
  PublicationStatusEvaluator}.cs`, `Infrastructure/Jobs/{PublicationStatusRefreshJob,StatusRefreshThrottleState}.cs`,
  миграция `…_MLC045WebinstPublishing.cs`, тесты `Publishing/{WsisapiVersionRewriter,PublicationStatusEvaluator,
  WebinstArgs,WebinstExeResolver}Tests.cs`, `Endpoints/PublicationsOperationsTests.cs`. Удалены:
  `VrdPatcher.cs`, `PublicationDriftDetector.cs`, `DriftCheckJob.cs`, `DriftThrottleState.cs`,
  `IDriftCheckJob.cs`, `PublicationDriftStatus.cs`, тесты `VrdPatcherTests`/`PublicationDriftDetectorTests`/
  `PublicationsReconcileTests`/`DriftCheckTransitionAuditTests`/`DriftCheckBatchQueryTests`,
  FE `ReconcilePublicationDialog.tsx`. Правки: домен/EF/DI/Program/endpoints/contracts/audit/problems
  (backend), вся фича `features/publications/*` + `features/infobases/{validation,types,PublicationFieldset,
  useInfobaseForm}` + `i18n/ru.json` (frontend), tool `PerfHarness/SeedGraph.cs`, канон.
- **Прогон.** `scripts/build.ps1` зелёный целиком — backend `build` (0 warnings, `TreatWarningsAsErrors`)/
  `test` (**326** — +4 нетто: +33 новых, −29 удалённых)/`format` (без правок на дефолтной severity);
  frontend `lint`/`type-check`/`test` (**119**)/`build` ОК — «Все шаги пройдены успешно».
- **Живая проверка (стенд: backend элевированно + реальный IIS + 1С 8.5.1.1302).** Миграция применилась
  против реального MSSQL (схема `Publications` ровно новая); маршруты `check`/`publish`/`change-platform`
  → 401, удалённые `reconcile`/`check-drift`/`drift-status` → 404. Read-only статус-job против реального
  IIS определил 3 публикации как `Published`, версию `8.5.1.1302` прочитал **из web.config** (подтверждает
  посылку «версия в web.config»). UI: колонки «Источник»/«Статус» + 3 кнопки. End-to-end под Admin:
  «Проверить сейчас» → 200; «Сменить платформу» → 200 + аудит 213 (web.config-only, `default.vrd` не
  тронут); «Опубликовать» → реальный webinst переписал `default.vrd`+`web.config`, `Source`→`Webinst`,
  аудит 212, гейт-токен подтверждения и предупреждение о перезаписи работают. **Найден и исправлен баг
  фронта**: `usePublish`/`useChangePlatform` передавали в `api()` уже `JSON.stringify(body)`, а хелпер сам
  сериализует и ставит `Content-Type` → двойная сериализация → `[FromBody]` не биндился → 500. Исправлено
  на передачу объекта; перепрогон FE `type-check`/`lint`/`test` (119) зелёный, UI-операции подтверждены 200.

---

### MLC-048 — Сбор time-series использования лицензий (фундамент трека «Отчёты») — 2026-06-06

- **Постановка (куратор, трек «Отчёты»).** Раздел «Отчёты»; первый отчёт — использование лицензий
  (concurrent-сеансов) во времени. Главное ограничение: системе нечем наполнять график — история
  потребления не хранится (активные сессии живут только в памяти, `ActiveSessionSnapshotStore`;
  персистится лишь `AuditLogs` = события, не замеры). Поэтому time-series вводится **с нуля**. MLC-048 —
  входная задача цепочки 048→049→050: хранение + точка съёма + агрегация + ретеншен. Полная спека —
  `.claude/plans/concurrent-purring-kahn.md`; план реализации — `.claude/plans/mlc-048-compiled-gosling.md`.
- **ADR-25** (`DECISIONS.md`, после ADR-24). Метрика = мгновенное `Consumed` (лицензие-потребляющие
  сессии) + `Limit` (`MaxConcurrentLicenses`) на момент замера. Бакеты 15 мин, агрегат min/max/avg
  (**max обязателен** — у concurrent-лицензий пик упирается в лимит; `avg` = `double`; `Limit` = последнее
  наблюдённое в бакете). Съём в cold `ReconciliationJob` через singleton-аккумулятор; ретеншен через
  настройку+джобу; один тир без rollup; сущность — телеметрия в Infrastructure (прецедент `AuditLog`),
  не доменный агрегат; `TenantId` FK `SetNull`.
- **Сущность/БД.** `Infrastructure/Reporting/LicenseUsageSnapshot` (`IEntity`: `Guid Id`, `Guid? TenantId`,
  `DateTime BucketStartUtc`, `int ConsumedMin/ConsumedMax`, `double ConsumedAvg`, `int Limit`). DbSet +
  конфиг inline в `AppDbContext.OnModelCreating` (паттерн `AuditLog`): `dbo.LicenseUsageSnapshots`, индекс
  `(TenantId, BucketStartUtc)`, FK на `Tenant` `OnDelete(SetNull)`. Миграция `MLC048LicenseUsageSnapshots`
  (нормализована: UTF-8 без BOM, LF).
- **Аккумулятор.** `Application/Reporting/ILicenseUsageAccumulator` (singleton, регистрация как
  `ColdThrottleState` — состояние переживает scoped-инвокации джобы) + `Infrastructure` impl
  `LicenseUsageAccumulator` (thread-safe `lock`): per-tenant running min/max/sum/count в текущем 15-мин
  бакете (floor по тикам); на пересечении границы возвращает агрегаты прошлого бакета и сбрасывается;
  откат часов назад игнорируется; частичный бакет при рестарте теряется (best-effort).
- **Слой-граница (решение по ходу).** NetArchTest запрещает Application→Infrastructure, поэтому интерфейс
  аккумулятора **не** возвращает entity `LicenseUsageSnapshot` (как `IAuditLogger` оперирует примитивами,
  не сущностью `AuditLog`). Application определяет нейтральные `LicenseUsageSample` (вход) и
  `LicenseUsageBucket` (выход); `ReconciliationJob` маппит bucket→entity при персисте. Эскиз спеки
  `IReadOnlyList<LicenseUsageSnapshot>` уточнён под границу — прозрачно (не [Doc divergence] канона).
- **`TenantId` = `Guid?` (решение по ходу).** Эскиз спеки давал `Guid TenantId`, но затребованный
  «по образцу `AuditLog`» `SetNull` требует nullable-колонку. Взят `Guid?` (как `AuditLog.TenantId`).
- **Врезка в `ReconciliationJob.RunColdAsync`.** После tier-цикла собирается семпл по **всем активным**
  тенантам цикла (distinct по тенанту; `Consumed = consumptionByTenant.GetValueOrDefault(id, 0)` — идлы=0,
  чтобы min/avg были честными; `Limit` = `MaxConcurrentLicenses`), переиспользуя данные цикла —
  **нового спавна `rac.exe` нет** (ADR-3.3/16). Готовые бакеты (если аккумулятор вернул) персистятся
  `_db.LicenseUsageSnapshots.AddRange + SaveChangesAsync` **вне** enforcement-замка (телеметрия не
  блокирует kill-путь).
- **Ретеншен.** `SettingKey.LicenseUsageRetentionDays` + запись в `SettingDefinitions` (Number, деф. 365,
  Min 30/Max 3650 — автосидер подхватил). Джоба `LicenseUsageRetentionJob`/`ILicenseUsageRetentionJob`:
  cron `30 3 * * *` (смещён от audit `0 3`), батч 5000 + commit-per-batch, **без аудит-записи**
  (housekeeping). **Решение (с куратором):** удаление — provider-portable `ExecuteDelete` (SELECT id'шников
  Take(5000) → `ExecuteDeleteAsync` WHERE Id IN), а не raw `DELETE TOP` образца `AuditRetentionJob` —
  транслируется и на прод-MSSQL, и на SQLite → ретеншен **покрыт юнит-тестом** (raw-T-SQL образец на SQLite
  непроверяем, потому `AuditRetentionJob` юнит-теста и не имеет).
- **Тесты (15 новых).** `LicenseUsageAccumulatorTests` (floor 15-мин, min/max/avg, флаш на границе,
  мультитенант, идл=0, last-limit, откат часов); `ReconciliationJobUsageSamplingTests` (семпл по активным
  тенантам, персист возвращённых бакетов / ничего внутри бакета — со stub-аккумулятором);
  `LicenseUsageRetentionJobTests` (SQLite: удаляет старше cutoff, батчинг >5000, no-op без старых);
  `LicenseUsageSnapshotPersistenceTests` (SQLite: схема/таблица/индекс в модели, FK SetNull). BE **377**
  зелёные (+0 регрессий, NetArchTest-границы держатся), `dotnet format` чист, миграция нормализована.
- **Канон present-tense.** ADR-25 (`DECISIONS.md`); `04_INFRASTRUCTURE.md` (каталог +1 ключ → **17**, новая
  телеметрия-таблица + джобы сбора/ретеншена в §5); `OPERATIONS.md` (каденция съёма ≈25с, ретеншен,
  потеря частичного бакета при рестарте = допустимо); счётчики «14 настроек» → 17 в `ROADMAP.md`/`00_INDEX.md`
  (попутно исправлена **предсуществующая** недосчитанность: каталог был 16 ключей до этой задачи, доки
  говорили 14). Индекс «Закрыто» дополнен пропущенным `MLC-047`.
- **Зависимости.** Разблокирует `MLC-049` (Reports API) и `MLC-050` (UI). ADR-20/16/3.3/single-node/RU-only
  не затронуты.

### MLC-049 — Reports API (license-usage сводка + drill-down) — 2026-06-06

- **Постановка (куратор, трек «Отчёты»).** Read-only API поверх собранной `MLC-048` таблицы
  `dbo.LicenseUsageSnapshots`, питающий будущий UI (`MLC-050`). Вторая задача цепочки 048→049→050.
  Полная спека — `.claude/plans/concurrent-purring-kahn.md` (раздел MLC-049); план реализации —
  `.claude/plans/plan-immutable-castle.md`. Согласовано в plan-режиме: форма контракта + логика
  агрегации сводки + обработка nullable `TenantId`.
- **Папка-фича.** `Web/Endpoints/Reports/` (`ReportsEndpoints` + `ReportsContracts`, плоский namespace
  `MitLicenseCenter.Web.Endpoints` как у прочих после MLC-035). `MapGroup("/api/v{version:apiVersion}/reports")`,
  `.HasApiVersion(1,0)`, `.WithTags("Reports")`, оба эндпоинта `RequireAuthorization(Roles.Viewer)`;
  регистрация `app.MapReportsEndpoints(versionSet)` в `Program.cs` рядом с `MapDashboardEndpoints`.
  Прямая инъекция `AppDbContext` (vertical slice ADR-20) + `TimeProvider` (для дефолта `to=now`; уже в DI,
  инжектится в `ReconciliationJob`/`TenantsEndpoints`). Везде `AsNoTracking`, фильтр `BucketStartUtc ∈
  [from,to]`, сортировка по `BucketStartUtc` ASC.
- **Контракт (единый на оба эндпоинта).** `LicenseUsageSeriesResponse { IReadOnlyList<LicenseUsageBucketPoint>
  Buckets, DateTime FromUtc, DateTime ToUtc, int PeakConsumed, int PeakLimit, DateTime? PeakAtUtc,
  double AverageConsumed }`; `LicenseUsageBucketPoint { DateTime BucketStartUtc, double ConsumedAvg,
  int ConsumedMax, int Limit }`. Одна форма → FE рисует тем же компонентом. `FromUtc`/`ToUtc` — эффективный
  диапазон после дефолта/клампа.
- **`GET /reports/license-usage` (сводка по всем).** DB-side `GroupBy(x => x.BucketStartUtc)` → на бакет
  `ConsumedMax`/`ConsumedAvg`/`Limit` = Σ по тенантам бакета. Период-сводка (`PeakConsumed` = max по бакетам
  от суммарного `ConsumedMax`; `PeakAtUtc`/`PeakLimit` — самый ранний бакет пика через `MaxBy`;
  `AverageConsumed` = avg по бакетам) считается из готового ряда в памяти.
- **Решение А (согласовано): осиротевшие записи `TenantId=null` ВКЛЮЧАЮТСЯ в сводку.** Фильтра по `TenantId`
  нет вовсе — история платформы не «усыхает» при удалении тенанта (`SetNull` MLC-048 выбран именно ради
  этого; прецедент `AuditLog`). Сумма по бакету — обзорная цифра, не истинный одновременный пик платформы
  (тенанты пикуют в разные суб-бакетные моменты) — задокументировано в каноне.
- **`GET /reports/license-usage/{tenantId:guid}` (drill-down).** `Where(TenantId == tenantId)` + диапазон,
  хранимые значения 1:1 в `LicenseUsageBucketPoint`. Null-тенант строки не достаются (guid ≠ null);
  несуществующий tenantId → пустой ряд (не 404).
- **Диапазон `from`/`to` (хелпер `ResolveRange`, решение согласовано).** Дефолт (оба опущены): `to=now`,
  `from=now-7д`. Кламп ширины: `> 31д` двигает `from` вперёд к `to-31д` (молча, эффективный диапазон в
  ответе). `to < from` → `ValidationProblem` (ключ `to`, как `AuditEndpoints`). Пустой ряд = `200` с
  `Buckets:[]` (не ошибка — под empty-state FE).
- **Тесты (9 новых).** `LicenseUsageReportsTests` (образец `DashboardSummaryTests`, статические методы
  напрямую + `TestHelpers.NewInMemoryDb`/`FixedClock`): пустая БД → пустой ряд + дефолтный диапазон;
  суммирование тенантов на бакет + пик по бакетам; осиротевшие строки входят в сводку; drill-down фильтрует
  по `{tenantId}` (значения как есть); чужой/несущ. tenant → пусто; порядок бакетов ASC; фильтр диапазона
  отсекает строки вне окна; `to<from` → `ValidationProblem`; кламп ширины двигает `from`. Полный CI
  (`build.ps1`) зелёный.
- **Канон present-tense.** `03_DOMAIN_MODEL.md` §«Persistence & API Contracts (binding)» — контракт
  reports-API (форма, сумма по тенантам, осиротевшие записи в сводке, дефолт/кламп диапазона, пустой ряд =
  не ошибка); `OPERATIONS.md` §«Сбор истории…» — отсылка к read-API. **ADR не трогали** — read-API в рамках
  принятого ADR-25.
- **Зависимости.** Разблокирует `MLC-050` (Frontend, раздел «Отчёты»). ADR-25/20/16/3.3/single-node/RU-only
  не затронуты.

### MLC-050 — Frontend: раздел «Отчёты» (использование лицензий) — 2026-06-06

- **Постановка (куратор, трек «Отчёты»).** Финальная задача цепочки 048→049→050: раздел `/reports`
  поверх Reports API из `MLC-049`. Полная спека — `.claude/plans/concurrent-purring-kahn.md` (раздел
  MLC-050); план реализации — `.claude/plans/plan-concurrent-cocke.md`. В plan-режиме согласованы UX-решения:
  вид графика (area max + линия avg + пунктир-лимит), drill-down отдельным блоком, заметная оговорка про
  обзорность суммы под сводным графиком, показ эффективного периода из ответа.
- **Фича `frontend/src/features/reports/` (паттерн MLC-032: контейнер + оркестрационный хук + презентация).**
  `types.ts` (зеркало контракта, camelCase + `ReportsFilters`/`ReportsRange`); `reportsUrlState.ts`
  (parse/build URL `from`/`to`/`tenant`, date-only→ISO как `auditUrlState`); `useLicenseUsage.ts` —
  TanStack Query `useLicenseUsage(range)` → `/api/v1/reports/license-usage` (always-on) и
  `useLicenseUsageByTenant(tenantId, range)` → `…/{tenantId}` (`enabled:!!tenantId`), query через
  `URLSearchParams` (границы ставятся только если заданы — дефолт/кламп на сервере); `useReportsPage.ts`
  (разбор URL, оба запроса, `useAllTenants` для селектора, `applyFilters`/`setTenant`).
- **Презентация.** `LicenseUsageChart` — **первое применение recharts** в проекте (`ComposedChart` в
  `ResponsiveContainer`: `Area consumedMax` заливкой + `Line consumedAvg` + `Line limit` пунктиром, ось
  времени `dd.MM HH:mm` через date-fns/ru, скелет при загрузке); один компонент на оба режима (контракт
  одинаков). `LicenseUsageSummary` (эффективный период из `fromUtc`/`toUtc`, стат-тексты, график, оговорка),
  `ReportsDetail` (Select клиента → ряд тенанта + его стат-тексты, плейсхолдер без выбора), `ReportsStats`
  (пик с долей от лимита и моментом + среднее), `ReportsEmptyState` («данные накапливаются» — пустой ряд =
  `200`, не ошибка; образец empty-state `AuditTable`), `ReportsFiltersBar` (период `from`/`to` + сброс),
  `ReportsPage` (контейнер + карточка ошибки с refetch).
- **Роут/навигация/i18n.** `/reports` lazy в `router.tsx` под общим `ProtectedRoute` (**не** admin-only);
  пункт `LineChartIcon` в группе «Операции» (`Sidebar.tsx`, после «Публикации»); ключи `nav.reports` +
  блок `reports.*` в `ru.json` (i18next only-ru) — тексты графика/сводки/детализации/empty-state/оговорки.
- **Бандл (vite.config).** recharts тянет victory-vendor (d3) + redux-toolkit + immer (~370 кБ) — иначе
  единый `vendor` перевалил бы за 500 кБ (порог, который схема MLC-018 держит осмысленно). Введена отдельная
  принудительная группа `charts` (priority>vendor); итог: `charts` 372 кБ / `vendor` 396 кБ / `react-vendor`
  274 кБ — все < 500 кБ, предупреждение сборки ушло. Полностью ленивым чанк не сделать (pnpm-путь
  `.pnpm/<pkg>/node_modules` ломает negative-lookahead, именованная группа всегда прелоадится) — eager наравне
  с прочими vendor-зависимостями.
- **Тесты (по образцу `features/audit/__tests__/`).** `reportsUrlState.test.ts` (parse/build URL +
  date-only→ISO границы) и `useLicenseUsage.test.tsx` (сбор query сводки/drill-down, `enabled:false` без
  тенанта = нет запроса, success/error). FE 145 тестов зелёные; графики recharts в jsdom не рендерятся
  (нулевая ширина) — тесты на хуки/утилиты. `pnpm type-check`/`lint` чисто, полный CI (`build.ps1`) зелёный.
- **Проверка в preview.** На реальной dev-БД с накопленной телеметрией: пункт «Отчёты» в «Операциях», график
  рисуется (легенда max/avg/limit, оси), эффективный период из ответа, drill-down (выбор тенанта → второй
  график с его пиком), оговорка под сводкой; на диапазоне без данных (2025 г.) — empty-state «данные
  накапливаются». Консоль без ошибок recharts.
- **Канон present-tense.** `05_UI_REQUIREMENTS.md` §3.6 (новая страница Reports; «Администраторы» → §3.7);
  `06_UI_DESIGN.md` (Charts: recharts подключён на `/reports`, чанк `charts`; sidebar Operations +Reports;
  ссылка §3.6→§3.7); `ROADMAP.md` (recharts применён на «Отчётах», дашборд-графики — опция будущего; §3.6→§3.7).
  **ADR не трогали** — UI в рамках принятого ADR-25.
- **Трек «Отчёты» завершён 3/3** (`MLC-048` сбор → `MLC-049` API → `MLC-050` UI). Активных задач нет;
  следующую ставит куратор. ADR-25/20/16/3.3/single-node/RU-only не затронуты.

### MLC-051 — Экспорт отчётов: каркас + CSV + XLSX — 2026-06-07

- **Постановка (куратор, трек «Экспорт отчётов»).** Задача A из мини-трека (2 задачи: A — каркас+CSV+XLSX,
  B — HTML+PDF). Полная спека трека — `.claude/plans/adaptive-scribbling-quiche.md`; план реализации —
  `.claude/plans/mitlicense-center-velvety-hinton.md`. Цель: в `/reports` выгружать отчёт в файл; оба разреза
  (сводка по всем клиентам и детализация по выбранному) — **по отдельности**. Задача B (HTML/PDF,
  chart.js/jspdf) — отдельной сессией, не тронута.
- **Ключевое решение — экспорт целиком клиентский.** Данные уже посчитаны и лежат в браузере
  (`useReportsPage` → `summary.data`/`detail.data` типа `LicenseUsageSeriesResponse`). Новый бэкенд-эндпоинт
  не заводили (UI в рамках ADR-25, нового ADR нет) — серверный экспорт сэкономил бы только сериализацию.
- **Новый модуль `frontend/src/features/reports/export/`.**
  - `downloadBlob.ts` — `downloadBlob(filename, blob)`: object URL + временный `<a download>` + revoke
    (готовых утилит скачивания в проекте не было).
  - `exportFilename.ts` — `license-usage_<scope>_<from>_<to>.<ext>`; `scope` = `all` (сводка) или slug имени
    клиента (детализация, кириллица сохраняется, вырезаны fs-небезопасные символы, пустое имя → `client`);
    диапазон date-only из `fromUtc`/`toUtc`. Тип `ExportScope = "all" | { tenantName: string|null }`.
  - `toCsv.ts` — без зависимостей, RU-Excel-дружелюбно: **UTF-8 с BOM** (`String.fromCharCode(0xFEFF)` —
    литерал U+FEFF вырезается тулингом сборки), разделитель `;`, десятичная запятая в среднем (округление
    `Math.round(x*10)/10`, как `LicenseUsageChart`), дата `dd.MM.yyyy HH:mm` (date-fns/ru), `\r\n`. Чистая
    таблица «Начало бакета;Среднее;Пик;Лимит» (пик/среднее сводки не кладём).
  - `toXlsx.ts` — зависимость `xlsx` (SheetJS) через `dynamic import` по клику. Книга из двух листов:
    «Сводка» (разрез, эффективный диапазон, пик/лимит, момент пика, среднее) + «Данные» (таблица бакетов).
    Числа — **настоящими числами** (`aoa_to_sheet`, тип ячейки `n`), чтобы работали сводные/графики Excel.
  - `ExportMenu.tsx` — один компонент на оба разреза (shadcn `DropdownMenu`, «Скачать ▾», пункты «CSV»/«Excel»).
    Скрыт при пустом ряде (`!data || buckets.length === 0`). XLSX в `try/catch` с `toast.error` (dynamic import
    может упасть). Props: `data`, `scope`.
- **Встройка (оба разреза по отдельности, требование пользователя).** `LicenseUsageSummary.tsx` — `ExportMenu`
  в `CardHeader` (`scope="all"`); `ReportsDetail.tsx` — новый prop `selectedTenantName`, `ExportMenu`
  `scope={{ tenantName }}`; `ReportsPage.tsx` — прокинут `selectedTenantName` из `useReportsPage` (хук уже
  его отдавал, страница не использовала). i18n: блок `reports.export.*` (`menu`/`csv`/`xlsx`/`error`, ru).
- **Бандл (vite.config).** Новая rolldown-группа `export-libs` (priority 30, как `charts`) под `xlsx`. `xlsx`
  импортируется только динамически → чанк грузится по клику. Итог сборки: `export-libs` 425 кБ (gzip 142),
  все чанки < 500 кБ, предупреждений нет. `xlsx@0.18.5` добавлен в `dependencies`.
- **Тесты (Vitest).** `toCsv.test.ts` (BOM по сырым байтам EF BB BF — `blob.text()`/TextDecoder срезает
  ведущий BOM; `;`/заголовок/десятичная запятая/число строк/пустой ряд), `exportFilename.test.ts` (scope
  all/клиент, slug, sanitize, fallback), `toxlsx.test.ts` (читаем книгу обратно SheetJS — имена листов,
  шапка «Данные», числовые ячейки `t==="n"`, метка разреза), `ExportMenu.test.tsx` (рендер триггера, пункты
  CSV/Excel по `userEvent`-клику, скрытие при пустом/undefined ряде). Сериалайзер-тесты — в `node`-окружении
  (`// @vitest-environment node`), т.к. jsdom-`Blob` не имеет `.text()`/`.arrayBuffer()`. FE **162** зелёные.
  TZ-устойчивость фикстур: полдень UTC (date-only не плывёт), час в CSV проверяется регуляркой.
- **Проверка.** `pnpm test`/`type-check`/`lint` чисто; `pnpm build` — чанк `export-libs` отдельным, порог
  держится. Доказательство формата — реальные CSV/XLSX из тех же модулей (временный proof-скрипт, затем
  удалён): CSV-байты `EF BB BF`, десятичная запятая `3,5`/`5,7`, дата `dd.MM.yyyy HH:mm`; XLSX — два листа
  «Сводка»/«Данные», числовая ячейка `type=n value=3.5`. Live-скриншот меню требует накопленной телеметрии
  (меню гейтится непустым рядом) + логина — заменён детерминированным `ExportMenu`-тестом.
- **Терминология.** Спека куратора говорит «manualChunks-чанк», но реальный `vite.config` — rolldown
  `codeSplitting.groups`; добавлена группа в фактическую структуру (деталь реализации, не doc-divergence).
- **Канон present-tense.** `05_UI_REQUIREMENTS.md` §3.6 — буллет про выгрузку (меню «Скачать» в обоих разрезах,
  CSV/XLSX, клиентский экспорт без нового API). **ADR не трогали** — UI в рамках ADR-25.
- **Зависимости.** Разблокирует/предшествует `MLC-052` (HTML+PDF, ставит куратор). ADR-25/20/16/3.3/single-node/RU-only не затронуты.

### MLC-052 — Экспорт отчётов: HTML (интерактивный) + PDF — 2026-06-07

- **Постановка (куратор, трек «Экспорт отчётов»).** Задача B (финальная) мини-трека. Спека —
  `.claude/plans/adaptive-scribbling-quiche.md` (раздел «Задача B»); план реализации —
  `.claude/plans/mitlicense-center-starry-meerkat.md`. Цель: добавить к CSV/XLSX (MLC-051) два «живых»
  формата — HTML с **тем же интерактивным графиком**, что в панели (офлайн, одним файлом), и печатный PDF.
  Оба разреза (сводка/детализация) — по отдельности, как в A. Экспорт целиком клиентский (UI в рамках
  ADR-25, нового ADR/эндпоинта нет).
- **Согласование с пользователем.** В plan-режиме поднят конфликт: спека просила положить chart.js/jspdf в
  один чанк `export-libs` при пороге <500 кБ, но суммарно ≈1.4 МБ. Пользователь делегировал выбор
  («надёжно/правильно/best practices»). Решение: реальный скачиваемый `.pdf` через jsPDF со встроенным
  кириллическим шрифтом (а не print-fallback), чанки бьём по форматам.
- **`chartConfig.ts`** — единый источник конфигурации Chart.js (данные + опции), JSON-сериализуемый.
  Воспроизводит `LicenseUsageChart.tsx`: area `consumedMax` (sky `#0ea5e9`) + линия `consumedAvg`
  (emerald `#059669`) + линия `limit` (rose `#f43f5e`, `borderDash`), метки `dd.MM HH:mm` (date-fns/ru),
  `consumedAvg` округлён до десятых, `animation:false`/`responsive:false`. Переиспользуется HTML и PDF.
- **`toHtml.ts`** — самодостаточный офлайн-HTML (Blob `text/html`): инлайн `<style>`, сводка (пик/доля/момент/
  среднее, оговорка про обзорность суммы — только для сводки), `<canvas>`, таблица бакетов, инлайн-исходник
  Chart.js (UMD авто-регистрирует контроллеры → достаточно `new Chart`), `<script>` с `const DATA` (JSON,
  `<` экранирован). Чистое построение строки — canvas не нужен (график оживает при открытии). Исходник
  Chart.js отдаёт **виртуальный модуль `virtual:chartjs-umd-src`** (плагин `chartjsUmdSource` в vite.config:
  `load()` читает `chart.umd.min.js` установленного пакета). Почему не `node_modules/...?raw`: пакет не
  экспортирует `./dist` через `exports`, а alias-обход ломался в dev — esbuild-предбандл исполнял UMD как CJS
  вместо выдачи текста; виртуальный модуль не оптимизируется и работает одинаково в dev и сборке.
- **`toPdf.ts`** — `jspdf` + `jspdf-autotable` (`dynamic import`). Документ: заголовок (разрез+период) → сводка →
  картинка графика → таблица (`autoTable`). Картинка — **offscreen Chart.js** (`import("chart.js")` +
  `Chart.register(...registerables)`, canvas → `toDataURL("image/png")`), под гардом отсутствия 2D-контекста
  (node/jsdom → картинка пропускается, PDF валиден — так проходит smoke-тест). **Кириллица:** `addFileToVFS`+
  `addFont`+`setFont` встроенного сабсета; AutoTable — `font:"Roboto", fontStyle:"normal"` и для шапки (иначе
  helvetica-bold без кириллицы). `compress:true` (zlib через fflate) — иначе несжатый битмап графика раздувал
  PDF до **5.9 МБ → 145 кБ**.
- **Шрифт `fonts/robotoCyrillic.ts`** — base64 TTF-сабсета **Roboto Regular v2.137, Apache License 2.0**
  (источник: `@expo-google-fonts/roboto` через jsdelivr; сабсет Basic Latin + Latin-1 + весь блок Cyrillic
  U+0400–04FF + типографика, инструмент `subset-font`/harfbuzz, **без Python**; 102 кБ → ~137 кБ base64).
  Лицензия/происхождение зафиксированы в шапке файла. Едет в app-чанке `toPdf` (не в vendor).
- **Бандл (vite.config).** `export-libs` → три per-format группы `export-xlsx`/`export-chart`/`export-pdf`
  (один чанк был бы ~1.4 МБ). Тонкости, найденные при сборке:
  - неиспользуемые **опциональные** зависимости jsPDF (`html2canvas`/`canvg`/`dompurify` — только `.html()`/SVG,
    мы их не зовём) тянулись динамическим `import()` jsPDF и **утекали в eager-`vendor`** (≈250 кБ+, html2canvas
    один ~200 кБ). Застаблены alias-ом на пустой модуль (`jspdfOptionalStub.ts`) — vendor вернулся к 396 кБ.
    Статические codec-зависимости (`fflate`/`fast-png`) — нужны (сжатие/PNG), оставлены в общем vendor.
  - общий рантайм-хелпер `__vitePreload` (виртуальный, не в node_modules) ролдаун ко-локовал в `export-pdf`,
    из-за чего entry статически тянул тот чанк в `modulepreload` (jspdf грузился/инициализировался на старте).
    Изолирован в собственный чанк `vite-preload-helper` (priority 100, ~1.2 кБ) — `export-pdf`/`export-chart`/
    `export-xlsx` стали по-настоящему ленивыми (нет в preload).
  - Итог: все чанки <500 кБ (`export-chart` 203, `toHtml` 212, `export-xlsx` 425, `export-pdf` 430 кБ),
    предупреждений нет. `chart.js`/`jspdf`/`jspdf-autotable` в `dependencies`.
- **pnpm.** `jspdf` тянет опциональный `core-js`, чей build-скрипт pnpm 11 гейтит (ERR_PNPM_IGNORED_BUILDS),
  что валило `verify-deps-before-run` перед скриптами. Решение зафиксировано в `pnpm-workspace.yaml`
  (`allowBuilds: core-js: false` — не собираем).
- **Тесты (Vitest, node-окружение).** `toHtml.test.ts` (тип blob, инлайн-баннер `Chart.js v`, `<canvas>`,
  init-скрипт, JSON `DATA`, метки рядов, строки таблицы по бакетам, оговорка только для сводки, пустой ряд),
  `toPdf.test.ts` (Blob `application/pdf` ненулевой, сигнатура `%PDF`, пустой ряд). `ExportMenu.test.tsx`
  расширен на пункты HTML/PDF. FE **170** зелёные; type-check/lint/build чисто.
- **Проверка в браузере (dev-preview, реальные модули).** HTML: в офлайн-файле Chart.js инлайнится (209 кБ,
  баннер на месте), график **рисуется** (непустой canvas по пиксель-инспекции), 24 строки таблицы, кириллица
  в сводке; визуальный скрин — area+линия+пунктир-лимит, легенда «Пик потребления/Среднее потребление/Лимит».
  PDF (canvas доступен → реальная картинка): 145 кБ, `pdftotext` извлёк читаемую кириллицу
  («Использование лицензий — ООО «Ромашка»», «Пик за период: 10 из 10 (100%)», шапка «Начало бакета/Среднее/
  Пик/Лимит»); в PDF присутствуют `FontFile2` (встроенный TTF) и `/Image` (картинка графика).
- **Канон present-tense.** `05_UI_REQUIREMENTS.md` §3.6 — перечень форматов дополнен HTML/PDF (инлайн-движок,
  встроенный Roboto Apache-2.0, per-format lazy-чанки). **ADR не трогали** — UI в рамках ADR-25.
- **Трек «Экспорт отчётов» завершён 2/2** (`MLC-051` CSV/XLSX → `MLC-052` HTML/PDF). Активных задач нет;
  следующую задачу/трек ставит куратор. **Кандидаты на потом** (зафиксированы, не взяты): (1) live-скрин меню
  «Скачать» на самой `/reports` требует накопленной телеметрии — заменён детерминированной проверкой модулей;
  (2) две предсуществующие FE-формат-расхождения (`AuditFiltersBar.tsx`, `DeleteInfobaseDialog.tsx`) prettier
  флагует — не трогал (вне объёма, не staged). ADR-25/20/16/3.3/single-node/RU-only не затронуты.

### MLC-053 — dev/ops-утилита сброса пароля администратора (`reset-admin`) — 2026-06-07

- **Category:** Maintainability / Backend (dev/ops tooling)
- **Priority:** P2 · **Severity:** Medium
- **Module:** Identity / dev-tooling (`MitLicenseCenter.Tools.PerfHarness`)
- **File(s):** `backend/tools/MitLicenseCenter.Tools.PerfHarness/Program.cs` (диспетч + `RunResetAdminAsync`);
  `backend/tools/MitLicenseCenter.Tools.PerfHarness/AdminReset.cs` (**новый**);
  `backend/src/MitLicenseCenter.Infrastructure/Identity/IdentitySeeder.cs` (`GenerateInitialPassword` → `internal`);
  `backend/src/MitLicenseCenter.Infrastructure/MitLicenseCenter.Infrastructure.csproj` (`InternalsVisibleTo`);
  `scripts/reset-admin.ps1` (**новый**, UTF-8 BOM);
  `backend/tests/MitLicenseCenter.Tests.Unit/Identity/IdentitySeederTests.cs` (**новый**);
  `docs/OPERATIONS.md` (раздел «Recovering admin access»).
- **Постановка (куратор).** Пароль первого `admin` генерируется случайно при первом старте и печатается в
  лог **один раз** (`IdentitySeeder.EnsureSeededAsync`→`LogSeededAdmin`). При его потере единственный путь —
  `scripts/db-reset.ps1`, который дропает и пересоздаёт БД (стирает все данные). Нужна поддерживаемая команда,
  сбрасывающая пароль администратора **без потери данных**, через штатный `UserManager` (корректный
  Identity-хеш, та же парольная политика).
- **Контракт.** `PerfHarness reset-admin [--user admin] [--password <value>] [--unlock] [--connection <cs>]`.
  Находит пользователя (`--user`, дефолт `admin` = `IdentitySeeder.DefaultAdminUserName`); нет такого →
  ошибка + exit `2`. Пароль: `--password`, иначе криптослучайный по политике. Сброс через
  `GeneratePasswordResetTokenAsync`→`ResetPasswordAsync` (не голый SQL). `--unlock` снимает lockout
  (`SetLockoutEndDateAsync(null)`+`ResetAccessFailedCountAsync`; политика лока `MaxFailedAccessAttempts=5`).
  Логин+итоговый пароль печатаются в **stdout** (как сидер), exit `0`; слабый `--password` → ошибки политики +
  exit `3`.
- **Wiring (`AdminReset.cs`).** Поднимается `Host.CreateApplicationBuilder` + `services.AddInfrastructure(config, env)`;
  `UserManager<AppUser>` резолвится из scope **без запуска хоста** (`Run/StartAsync` не зовутся → хостед-сервисы
  RAS-пробер/hot-tier и Windows-only IIS-адаптеры конструируются лениво, не стартуют). Так парольная политика и
  token-провайдеры — 1:1 с приложением, единственный источник в `AddInfrastructure`, без дублирования. Строка
  подключения цепочкой `--connection` → env `ConnectionStrings__Default` → дефолт, инъекция через
  `Configuration.AddInMemoryCollection(["ConnectionStrings:Default"])` (последний источник перекрывает прочие;
  `AddInfrastructure` читает её через `GetConnectionString("Default")`).
- **Гоча валидации контейнера.** В Development `Host.CreateApplicationBuilder` включает `ValidateOnBuild` —
  на `Build()` он эджерно валидирует **все** дескрипторы, включая `SignInManager<AppUser>` (ему нужен
  `IAuthenticationSchemeProvider`, регистрируемый только Web-слоем через `AddAuthentication`), и падал
  `Unable to resolve service for type 'IAuthenticationSchemeProvider'`. Нам нужен только `UserManager`, поэтому
  валидация отключена явно: `builder.ConfigureContainer(new DefaultServiceProviderFactory(new
  ServiceProviderOptions { ValidateScopes = false, ValidateOnBuild = false }))`. Scope создаётся вручную.
- **Генерация пароля (парити).** `IdentitySeeder.GenerateInitialPassword()` сделан `private`→`internal` +
  `InternalsVisibleTo("MitLicenseCenter.Tools.PerfHarness")` в csproj Infrastructure — единый генератор,
  не плодим второй (политика-парити). `PickChar` остался private.
- **Аудит.** CLI-сброс без HTTP-initiator; сидер создание admin не аудитит → `AuditLog` **не пишем**
  (консистентно). Писать означало бы новое `AuditActionType` = новое замороженное int-значение — без надобности.
- **Безопасность вывода.** Пароль печатается только в stdout (как сидер), не в файловые лог-приёмники.
- **PS-обёртка `scripts/reset-admin.ps1`** (UTF-8 BOM, по образцу `perf-seed.ps1`): параметры
  `-User`/`-Password`/`-Unlock`/`-ConnectionString`/`-Configuration`, ставит `DOTNET_ENVIRONMENT=Development`
  (dev-key ring в LocalAppData — гарантированно writable без elevation; на корректность не влияет — токен
  сброса одноразовый и потребляется в том же процессе), снимает `$ErrorActionPreference=Stop` вокруг
  `dotnet run` и проверяет `$LASTEXITCODE` (паттерн build/perf-seed).
- **Тесты.** `IdentitySeederTests` (2 факта): генератор удовлетворяет политике (длина ≥12, есть
  upper/lower/digit/non-alphanumeric — прогон ×200 против флака перемешивания) и два вызова различны. BE-сборка
  и тесты зелёные.
- **Ручной прогон на dev-БД.** `reset-admin --user __nosuch__` → «не найден» + exit `2`; `--user admin` →
  печатает сгенерированный пароль + exit `0`; `--user admin --password '…' --unlock` → задаёт указанный + снимает
  lockout + exit `0`; `--password '123'` → ошибки политики (`PasswordTooShort`/`RequiresNonAlphanumeric`/
  `RequiresLower`/`RequiresUpper`) + exit `3`. Данные (клиенты/инфобазы) на месте — переписан только хеш пароля.
- **Канон present-tense.** `docs/OPERATIONS.md` — раздел «Recovering admin access — `reset-admin` instead of a
  destructive reset» (когда применять вместо `db-reset.ps1`, команда/флаги/exit-коды, сохранность данных, prod vs
  dev key ring). **ADR не требуется** — offline-утилита, контракты/эндпоинты/ADR не трогаются (ADR-20 допускает
  прямой `AppDbContext`/Identity в tools-проекте). tools-проект остаётся вне прод-publish (`IsPublishable=false`,
  Web на него не ссылается).
- **Status:** **Done** (2026-06-07).

### MLC-054 — Отчёты: полировка (плашка обрезки + сводка HTML/PDF + помесячный выбор) — 2026-06-07

- **Category:** UX / Frontend (+ малая правка контракта Reports API)
- **Priority:** P2 · **Severity:** Low
- **Module:** `features/reports/` (FE) + `Web/Endpoints/Reports/` (BE-флаг)
- **Постановка (куратор).** Три UX-шероховатости `/reports` одной задачей. Номер: куратор пометил `MLC-053`,
  но он занят (`reset-admin`, Done 2026-06-07) — согласован следующий свободный `MLC-054`. Спека —
  `.claude/plans/adaptive-scribbling-quiche.md`; план исполнения — `.claude/plans/mitlicense-center-eager-ullman.md`.
  ADR не трогали (UI/чтение в рамках ADR-25), эндпоинтов не добавляли, глубину/ретеншен не трогали.
- **Часть 1 — плашка обрезки.** Сервер молча резал запрос > 31 дня — сделали обрезку видимой. Контракт
  `LicenseUsageSeriesResponse` +`Clamped`/`MaxSpanDays` (`ReportsContracts.cs`); `ResolveRange` →
  `(From, To, Clamped)`, `Clamped=true` **ровно** в ветке `effectiveTo - effectiveFrom > MaxSpan` (дефолтное
  окно 7 дней её не задевает); `BuildResponse` прокидывает флаги (пустой ряд тоже несёт). Оба эндпоинта
  (`SummaryAsync`/`DrilldownAsync`) отдают флаг. FE: `types.ts` зеркалит поля; `ReportsPage.tsx` под фильтром
  рендерит уведомление при `summary.data?.clamped` (нейтральный info-блок `border`+`bg-muted/30`); i18n
  `reports.filters.clampNotice` (интерполяция `{{days}}`).
- **Часть 2 — HTML/PDF без сырой таблицы.** Презентационные выгрузки несли побакетную таблицу (на длинных
  периодах — тысячи строк). `toHtml.ts` — убраны вычисление `rows`, блок `<table>`/`.table-wrap` и CSS таблицы;
  заголовок/период/stats/caveat/график остались (`escapeHtml`/`round1` ещё нужны для заголовка/stats).
  `toPdf.ts` — убраны `import jspdf-autotable` и вызов `autoTable(...)`; заголовок→период→пик/среднее→картинка
  графика остались. Зависимость `jspdf-autotable` удалена из `package.json` + lock; regex чанка `export-pdf` в
  `vite.config.ts` → `(jspdf)`. Сырая таблица осталась только в CSV/XLSX.
- **Часть 3 — помесячный выбор «Месяц ‹ ›».** Чисто FE, заполняет те же `from`/`to` (новых URL-параметров нет).
  `reportsUrlState.ts` — чистые хелперы `monthToRange(ym)` (границы месяца через `endOfMonth`/`parseISO`) и
  `shiftMonth(ym, delta)` (`addMonths`, формат `yyyy-MM`). `ReportsFiltersBar.tsx` — группа «Месяц»: ‹ +
  `<input type="month">` + › (lucide `ChevronLeft`/`ChevronRight`, aria-label из i18n); значение — `from.slice(0,7)`
  или текущий месяц от `new Date()` при пустом периоде. **Свойство:** целый месяц всегда < 31 дня → кламп не
  триггерит (проверено).
- **Тесты.** BE: `Range_wider_than_max_span_clamps_from_forward` +`Clamped==true`/`MaxSpanDays==31`; новые
  `Default_range_is_not_clamped`, `Range_within_max_span_is_not_clamped`. FE: `reportsUrlState.test.ts` —
  `monthToRange` (28/29/30/31-дн.), `shiftMonth` (через границу года); `toHtml.test.ts` — нет таблицы / есть
  сводка+график; export-фикстуры обновлены новыми полями. BE 397 / FE 177 зелёные; type-check/lint/build чистые.
- **Вес чанков.** `export-pdf` 399.6 кБ (ушёл `jspdf-autotable`), все чанки < 500 кБ.
- **Проверено в браузере (`/reports`, реалистичная dev-БД).** Период > 31 дня → плашка «Запрошенный период больше
  31 дней — показаны последние 31», эффективный период 31 день; клик › помесячного контрола → `from`/`to` =
  границы месяца (`2026-04-01`/`2026-04-30`), плашки нет. Выгрузки сгенерированы на живых данных (2880 бакетов):
  HTML — нет `<table>`/«Начало бакета», есть период/пик/среднее + canvas + инлайн Chart.js (293 кБ);
  PDF — `%PDF-`, встроенный Roboto (`FontFile2`) + картинка графика (`/XObject`), 286 кБ.
- **Канон present-tense.** `03_DOMAIN_MODEL.md` (поля `clamped`/`maxSpanDays` + семантика флага),
  `05_UI_REQUIREMENTS.md` §3.6 (плашка обрезки, помесячный выбор, HTML/PDF = сводка+график без сырой таблицы).
- **Status:** **Done** (2026-06-07).

### MLC-055 — Переработка страницы `/settings` (секции, retention, порт RAS, единый пикер платформы) — 2026-06-07

- **Category:** UX / Frontend (+ канон-доки)
- **Priority:** P2 · **Severity:** Low
- **Module:** `frontend/src/features/settings/` + `i18n/ru.json` + docs (ADR-3.3 / 04 / 05)
- **Постановка (трек «Полировка /settings», 1/2).** Раздел «Параметры» накопил поля, дублирующие
  автодискавери или требующие ручного ввода там, где discovery уже есть, плюс скрытый ключ
  `LicenseUsage.RetentionDays` не выведен в UI. Четыре сведённые правки одной когерентной
  переработки страницы (дробить = тройной churn одного файла). Только frontend+доки, бэкенд не тронут.
  Сохранение остаётся пер-контрольным (общую «Применить» НЕ вводили). Спека —
  `.claude/plans/1-2-rippling-zephyr.md` (MLC-055); план исполнения — `.claude/plans/plan-elegant-hoare.md`.
- **(a) Перегруппировка секций.** `SECTIONS` в `SettingsPage.tsx`: смешанная «cluster» разнесена на
  «Подключение к 1С / RAS» (`OneC.Cluster.AdminUser/AdminPassword` + порт + платформа) и отдельную
  «Учёт лицензий» (`OneC.LicenseConsumingAppIds`). «audit» → объединённая «Хранение данных»
  (`Audit.RetentionDays` + `LicenseUsage.RetentionDays`). Из «Значений по умолчанию для новых баз»
  убрана версия платформы (ушла в пикер) → остались `Defaults.DatabaseServer` + `IIS.DefaultSiteName`.
  Ключи в БД не менялись — только распределение по секциям; устаревший комментарий «14 ключей» переписан.
- **(b) Выведена `LicenseUsage.RetentionDays`.** Ключ был в каталоге (`SettingDefinitions.cs`, 30–3650,
  дефолт 365) и в доке (`04_INFRASTRUCTURE.md` таблица), но не в UI — UI отстал. Добавлен в секцию
  хранения, `FIELD_META {type:number, min:30, max:3650}`, label/hint в `ru.json` («история для /reports»).
- **(c) RAS endpoint → поле «Порт».** Новый `RasPortField.tsx` (по образцу плоской ветки `SettingField`
  + ресинк draft): редактирует только порт (1024–65535, дефолт 1545), на сохранении пишет wire-формат
  `localhost:<порт>` (`buildRasEndpoint`), при загрузке вырезает порт (`parseRasPort`). Хост фиксирован
  `localhost` (single-node топология). Бэкенд (`WebinstArgs`, `RacExecutableRasClusterClient`, kind
  `HostPort`) не тронут — wire-формат `host:port` сохранён, не-localhost host остаётся настраиваемым через API/БД.
- **(d) Единый пикер «Платформа 1С».** Новый `PlatformPicker.tsx` заменил `RacPathDetect.tsx` (удалён).
  Основной контрол — `Select` по `useRacPaths(true)` (save-on-pick): пункт = установленная платформа
  с rac.exe, label = распарсенная версия, выбор пишет **оба** раздельных ключа одним действием
  (`OneC.RAS.ExePath` = путь, `OneC.DefaultPlatformVersion` = версия) двумя мутациями `useUpdateSetting`
  + тост. Версия парсится чистым хелпером `parsePlatformVersionFromRacPath` (regex
  `[\\/]1cv8[\\/]([^\\/]+)[\\/]bin[\\/]rac\.exe$` + проверка «4 числовых сегмента», длины НЕ фиксируем —
  1С 8.5 одноцифровой build `8.5.1.1302`). Свёрнутый escape-hatch (`ChevronDown`-disclosure) разводит
  ключи врозь: путь — `SettingField` (ручной + Save), версия — `DiscoveryField`/`usePlatformVersions`
  (полный список, в т.ч. версии без rac.exe) с мостом draft+Save (немедленный onChange `DiscoveryField`
  не годится для пер-контрольного сохранения). Внешняя ресинхронизация переиспользует паттерн `SettingField`.
- **Реализационная гоча (зафиксирована).** `DiscoveryField.onChange` срабатывает на каждый keystroke
  ручного ввода (в форме инфобазы дёшево — кладёт в RHF-state); на странице настроек onChange = сетевой
  save, поэтому основной пикер — `Select` (save-on-pick), а ручной ввод — escape-hatch с явным Save.
- **Переиспользовано (не писали заново):** `features/discovery/{DiscoveryField,useDiscovery}`,
  `features/settings/{SettingField,useSettings}`. Чистые хелперы вынесены в `parsing.ts`.
- **Тесты.** `parsing.test.ts` (версия из rac-пути вкл. 8.5 одноцифровой build / прямые слэши / null;
  порт parse/build/дефолты); новый render-тест в `SettingsPage.test.tsx` (мок api по URL: порт-поле
  показывает `1600`, пикер показывает текущий путь+версию). FE 192 зелёные; type-check/lint чистые.
- **Verification.** Сначала покрыто jsdom-тестами (порт парсится `localhost:1600`→`1600`; пикер
  показывает путь+версию) + unit-тесты чистых парсеров. **Затем live-preview против запущенного стека
  (2026-06-07, пользователь поднял backend+frontend):** реальный `GET /discovery/rac-paths` нашёл
  `C:\Program Files\1cv8\8.5.1.1302\bin\rac.exe`, версия распарсилась `8.5.1.1302` (одноцифровой build
  8.5 — каверзный кейс на живых данных), строка состояния показала оба ключа; поле «Порт» = `1545` из
  хранимого `localhost:1545`; escape-hatch развернулся — поле «Путь к rac.exe» + версия через
  `DiscoveryField`/`usePlatformVersions` (`8.5.1.1302 — x64`); `LicenseUsage.RetentionDays` в «Хранение
  данных» рядом с audit; шесть секций в правильном порядке; консоль без warn/error; `auth/me`/`settings`/
  `discovery/rac-paths` → `200`. Живые настройки не менялись (RAS health не трогали).
- **Канон present-tense.** ADR-3.3 (UI-подача rac.exe-пути через пикер платформы + endpoint как порт;
  wire-формат `host:port`/первый позиционный аргумент НЕ менялся), `04_INFRASTRUCTURE.md` (UI-подача
  tool path/endpoint; каталог-таблица 17 ключей не тронута — `LicenseUsage.RetentionDays` уже был),
  `05_UI_REQUIREMENTS.md` §3.3 (зеркало секций /settings). ADR-20/16/single-node/RU-only не затронуты.
- **Следом по треку:** `MLC-056` (SQL-instance discovery localhost + пикер сервера БД, net-new backend).
- **Status:** **Done** (2026-06-07).

---

## Чистка реестра 2026-06-10 — секции завершённых треков (перенесено из PROJECT_BACKLOG.md)

> Перенесено дословно при возврате реестра к «тонкому» формату (п. 4 «Как пользоваться»).
> Контекст треков: методика, согласованные развилки, порядок исполнения, Done-сводки.

### Рефакторинг-трек — порядок исполнения (Phase 1–2, активны)

Тонкие записи; полные спеки (Цель/Причина/Польза/Риск/Сложность/Модули/Зависимости) —
в план-файле `distributed-orbiting-snail.md`. Берём строго в этом порядке, по одной за сессию.

1. ~~`MLC-030` (REF-02)~~ — **Done** (2026-06-03): архитектурные guard-тесты границ слоёв на NetArchTest (см. архив).
2. ~~`MLC-029` (REF-01)~~ — **Done** (2026-06-03): дедуп маппинга `Publication` request→entity в `InfobasesEndpoints` (хелпер `ApplyPublicationFields`), поведение 1:1 (см. архив).
3. ~~`MLC-031` (REF-03)~~ — **Done** (2026-06-03): фабрика CRUD-mutation хуков `useInvalidatingMutation` (FE), 10 хуков переведены, поведение 1:1 (см. архив).
4. ~~`MLC-034` (REF-06)~~ — **Done** (2026-06-04): тонкий Web-аудит-фасад `HttpContext.AuditAsync` (делегат-описание), 9 каноничных сайтов свёрнуты, состав журнала 1:1, ADR-20 не затронут (см. архив).
5. ~~`MLC-033` (REF-05)~~ — **Done** (2026-06-04): обобщённый conflict→descriptor маппер `matchConflictCode` + общий хвост submit форм `toastFormSubmitError` (`lib/apiErrors`), 5 сайтов переведены, поведение 1:1 (см. архив).
6. ~~`MLC-032` (REF-04)~~ — **Done** (2026-06-04): декомпозиция крупных FE-страниц (Audit/Publications/Sessions) на контейнер + оркестрационный хук + презентационные части по образцу MLC-023, поведение 1:1 (см. архив).
7. ~~`MLC-035` (REF-07)~~ — **Done** (2026-06-04): группировка плоского `Web/Endpoints` по подпапкам-фичам (+ `Shared/`); плоский namespace сохранён (IDE0130 не энфорсится), `git mv` без правок контента, поведение/контракты/регистрация 1:1 (см. архив).

**Phase 1–2 рефакторинг-трека закрыт полностью** (MLC-029..035). Phase 3–4
(`MLC-025/026/027/011(a)/028` + `MLC-036` RAS Strategy B) — gated на триггеры, см. ниже.

### Трек «Отчёты — использование лицензий» (открыт 2026-06-06)

Постановка пользователя; нарезан куратором. Новый раздел «Отчёты»; первый отчёт —
использование лицензий (concurrent-сеансов) во времени: график + текстовые пояснения,
сводка по всем клиентам + drill-down. **Истории потребления нет** (снимок только в памяти,
`ActiveSessionSnapshotStore`) → time-series вводится с нуля, на UI empty-state «данные
накапливаются». Полная спека (Цель/Объём/Файлы/Под-решения/Проверка) — в план-файле
`C:\Users\andre\.claude\plans\concurrent-purring-kahn.md`. Берём строго по порядку,
по одной за сессию:

1. ~~`MLC-048`~~ — **Done** (2026-06-06): Backend, сбор time-series (фундамент): ADR-25 +
   таблица `LicenseUsageSnapshots` (телеметрия в Infrastructure) + миграция +
   singleton-аккумулятор (бакеты 15 мин, min/max/avg) с врезкой в cold `ReconciliationJob`
   + настройка `LicenseUsageRetentionDays` + ретеншен-джоба (см. архив).
2. ~~`MLC-049`~~ — **Done** (2026-06-06): Backend, Reports API: `GET /reports/license-usage`
   (сводка по всем, осиротевшие записи включены) + `/{tenantId}` (drill-down), единый контракт
   `LicenseUsageSeriesResponse`, дефолт/кламп диапазона (7д/31д), тесты (см. архив).
3. ~~`MLC-050`~~ — **Done** (2026-06-06): Frontend, раздел `/reports`: пункт меню «Операции»,
   фича `features/reports/` (контейнер + хук + презентационные части), первый график на recharts
   (`ComposedChart`), фильтр периода, drill-down, empty-state (см. архив). **Трек завершён 3/3.**

### Трек «Экспорт отчётов» (открыт 2026-06-07)

Постановка пользователя; нарезан куратором. Выгрузка отчёта `/reports` в файл — оба разреза
(сводка и детализация) по отдельности. Экспорт целиком клиентский (данные уже в браузере,
`LicenseUsageSeriesResponse`), нового бэкенд-эндпоинта нет (UI в рамках ADR-25). Мини-трек из
2 задач, полная спека — в план-файле `C:\Users\andre\.claude\plans\adaptive-scribbling-quiche.md`:

1. ~~`MLC-051`~~ — **Done** (2026-06-07): Каркас экспорта + табличные форматы CSV и XLSX
   (модуль `features/reports/export/`, меню «Скачать» в обоих разрезах, см. архив).
2. ~~`MLC-052`~~ — **Done** (2026-06-07): HTML (самодостаточный офлайн-файл с интерактивным
   Chart.js-графиком) + PDF (jspdf + встроенный кириллический Roboto, картинка графика + таблица),
   per-format lazy-чанки. **Трек «Экспорт отчётов» завершён 2/2** (см. архив).

### Трек «Полировка страницы /settings» (открыт 2026-06-07)

Постановка пользователя; нарезан куратором. Раздел «Параметры» накопил поля, дублирующие
автодискавери (rac.exe ↔ версия платформы) или требующие ручного ввода там, где discovery уже
есть; плюс скрытый ключ `LicenseUsage.RetentionDays` не выведен в UI. Цель — переиспользовать
существующую discovery-инфраструктуру (`/discovery/*` + `DiscoveryField`), ничего не писать с нуля
кроме SQL-instance discovery. Полная спека (Контекст/развилки/файлы/проверка) — в план-файле
`C:\Users\andre\.claude\plans\1-2-rippling-zephyr.md`. Согласованные развилки: сохранение остаётся
пер-контрольным (без общей «Применить»); поле `status` формы и скрытость `OneC.Cluster.Server`
сохраняются; SQL-discovery только localhost. Берём по одной за сессию:

1. `MLC-055` — **Done (2026-06-07)** — Переработка `/settings` одним заходом (frontend+доки):
   секции разнесены, `LicenseUsage.RetentionDays` выведена, RAS endpoint→поле «Порт», единый пикер
   «Платформа 1С». Отчёт — в индексе «Закрыто» ниже и в `PROJECT_BACKLOG_ARCHIVE.md`.
2. `MLC-056` — **Done (2026-06-08)** — SQL-instance discovery (localhost) + пикер сервера БД: backend
   `ISqlInstanceDiscovery`/`SqlInstanceDiscovery` (реестр `Instance Names\SQL`, оба view, platform-guard,
   DI Singleton) + `GET /discovery/sql-instances`; frontend `useSqlInstances` + `DatabaseServerField` на
   `/settings` и `databaseServer`→`DiscoveryField` в форме инфобазы. Отчёт — в индексе «Закрыто» ниже.
   **Трек «Полировка /settings» завершён 2/2.**

### Трек «Полировка панели v1.1» (открыт 2026-06-08)

Постановка пользователя; нарезан куратором из анализа тех-долга (2026-06-08). Структурного долга нет, код
чист; согласованы два пункта (B4 «графики дашборда» отклонён; **webinst connstr-auth рассмотрен и отклонён** —
в официальной доке 1С механизма пароля для webinst нет, не реализуем и не вводим; корзина C trigger-gated не
берётся — триггеры не сработали: 17<25–30 ключей настроек, 728<1000 строк i18n, спавны срезаны MLC-041).
Полная спека (Контекст/объём/файлы/переиспользование/проверка) — в план-файле
`C:\Users\andre\.claude\plans\eventual-hopping-pebble.md`. Изначально два пункта (тема + Администраторы); по
ходу MLC-058 из §3.7 выделена третья задача `MLC-059` (форс-смена пароля + время последнего входа — требуют
миграцию, поэтому отдельно). Берём по одной за сессию:

1. `MLC-057` — **Done (2026-06-08)** — Переключатель темы (light/dark/system): `ThemeProvider` (`next-themes`)
   в `App.tsx` + тумблер `ThemeToggle` в `Topbar` + i18n `theme.*`. Отчёт — в индексе «Закрыто» ниже.
2. `MLC-058` — **Done (2026-06-08)** — Раздел «Администраторы» (Backend API + Frontend UI в одном заходе):
   эндпоинты `/admins` (список Admin+Viewer / создать с выбором роли / сброс пароля / disable+enable) на
   готовой Identity, +4 слота аудита (103–106), оба guard'а (сам себя / последний активный Admin), миграции
   нет; фронт `features/admins/`, admin-only роут + пункт сайдбара, показ временного пароля один раз. Как
   построено: без «последнего входа» и форс-смены пароля (требуют колонок на `AppUser` = миграция) — вынесены
   в `MLC-059`. Отчёт — в индексе «Закрыто» ниже.
3. `MLC-059` — **Done (2026-06-08)** — Форс-смена пароля при первом входе + время последнего входа (с
   миграцией): две колонки на `AppUser` (`MustChangePassword`, `LastLoginAt`) одной миграцией; login пишет
   время+флаг и возвращает флаг в `/me`+`/login`, create/reset ставят флаг, change-password снимает; фронт —
   блокирующий экран форс-смены (роут-гейт по `useMe`) + колонка «Последний вход». Отчёт — в индексе «Закрыто»
   ниже.

**Трек «Полировка панели v1.1» завершён 3/3 (MLC-057/058/059).**

### Мини-трек «Раздел Пользователи» (открыт 2026-06-08)

Постановка пользователя; нарезан куратором. Раздел `/admins` управляет и Admin-, и Viewer-учётками →
название «Администраторы» неточно. Две правки одного экрана, **один чат, два коммита**: полное
переименование (решение пользователя — до кода) + смена роли существующей учётки. Полная спека
(файлы/guard'ы/аудит/проверка) — в план-файле `C:\Users\andre\.claude\plans\users-section-rename-roles.md`.
Берём по одной за сессию (в одном чате последовательно):

1. `MLC-060` — **Done (2026-06-08)** — Полное переименование «Администраторы»→«Пользователи»
   (рефакторинг, поведение 1:1): `/admins`→`/users` (роут + API `MapGroup` + папки `Web/Endpoints/Users`,
   `features/users`), контракты/`Problems`/коды ошибок (parity BE↔FE), имена слотов аудита `Admin*`→`User*`
   (int 103–106 заморожены), i18n, канон §3.7. Отчёт — в индексе «Закрыто» ниже.
2. `MLC-061` — **Done (2026-06-08)** — Смена роли существующей учётки (Admin↔Viewer):
   `POST /api/v1/users/{id}/role` (Admin), guard'ы «сам себе нельзя» + «нельзя разжаловать последнего
   активного Admin», +1 слот аудита `UserRoleChanged=107`; фронт — действие «Сменить роль» (radio),
   подсказка «вступит в силу при следующем входе». Отчёт — в индексе «Закрыто» ниже.

**Мини-трек «Раздел Пользователи» завершён 2/2 (MLC-060/061).**

### Трек «Анализ быстродействия 1С» (открыт 2026-06-08)

Постановка пользователя; нарезан куратором. Новый раздел панели «Быстродействие» — ответ на
«почему 1С тормозит»: атрибуция (1) 1С vs не-1С, (2) если 1С — какой сеанс/пользователь/вызов.
**Методика** — «светофор ресурсов» (сатурация: CPU-очередь, disk-латентность, paging — не голый %)
+ атрибуция потребителя по семьям процессов (1С/MSSQL/ОС-обновления/антивирус/прочее) со спуском
внутрь виновной семьи. **Модель Live + Recording** (согласована, отменяет always-on историю):
live-панель собирает pull-по-требованию и **ничего не персистит** (закрыл вкладку — сбор стоп);
запись (recording) включается **вручную** для расследования, пишет в БД-таблицу с авто-стопом.
Отклонены в v1 (gated): always-on «чёрный ящик», сшивка SQL→сеанс→юзер. Co-located/single-node,
RU-only. Полная спека (методика/метрики/раскладка/нарезка/проверка) — в план-файле
`C:\Users\andre\.claude\plans\spicy-discovering-torvalds.md`. Трек = `MLC-063..071`, 5 фаз;
**Фазы 2–3 (1С-сеансы/SQL) — контур**; **разведка `MLC-063` инвертировала риск №1** — `rac session list`
отдаёт полный набор perf-полей (`cpu-time-*`/`duration-current`/`-dbms`/`memory-*`/`blocked-by-*`/`calls-*`)
уже в покое, `process list` (`available-perfomance`/`pid`) и DMV-доступ + атрибуция SQL **по базе**
подтверждены, **SQL→сеанс→юзер невозможна** (отклонение обосновано). Состав `MLC-066`/`068` доуточнён в
плане (секция «Результаты разведки»). Порядок:
063 → 064 → 065 → 066→067 → 068→069 → 070→071. Берём по одной за сессию:

1. `MLC-063` — **Done (2026-06-08)** — Разведка источников perf-метрик (research): на нагруженном стенде
   8.5.1.1302 снят реальный вывод `rac session list` (perf-поля ЕСТЬ — исходная гипотеза опровергнута) +
   `rac process list` + проверен доступ к MSSQL DMV (`VIEW SERVER STATE`, атрибуция по базе
   `1CV83 Server`→БД→клиент). Отчёт — в индексе «Закрыто» ниже и в секции «Результаты разведки» плана.
2. `MLC-064` — **Done (2026-06-08)** — Backend: адаптер host-метрик (`IHostMetricsProbe` + нейтральные
   DTO; `OneCHostMetricsProbe` на **WMI**, не `PerformanceCounter` — имена счётчиков локализованы на RU
   Windows; ADR-20 + `#pragma CA1416` + `StubHostMetricsProbe`) + `GET /performance/host` (live, Viewer).
   Заведён **ADR-26**, настройка `Performance.ProcessFamilyMap`, дельта-CPU% через singleton. Отчёт — в
   индексе «Закрыто» ниже и в `PROJECT_BACKLOG_ARCHIVE.md`.
3. `MLC-065` — **Done (2026-06-08)** — Frontend: каркас раздела `/performance` + стартовый live-экран.
   Фича `features/performance/` по образцу `features/sessions` (polling 5с, `placeholderData: prev`) +
   `features/reports` (recharts): роут lazy `/performance` (`Viewer`), пункт сайдбара «Быстродействие»
   (`Activity`, группа «Операции»), i18n `performance.*`, Zod-схема ответа (критичная граница MLC-016).
   Live-экран: баннер-вердикт + светофор гейджей CPU/RAM/Disk на `Progress` (пороги по сатурации:
   очередь/латентность/paging, не голый %) + атрибуция по семьям (stacked-bar recharts + плотная таблица,
   ключи `OneC`/`Mssql`/`OsUpdate`/`Antivirus`/`Other` → i18n) + честный `Measuring` («измеряю…», не нули).
   Канон §3.8 + 06_UI_DESIGN §3/§5. Контракт сверен end-to-end с живой WMI-пробой. **Фаза 1 завершена.**
   Отчёт — в индексе «Закрыто» ниже и в `PROJECT_BACKLOG_ARCHIVE.md`.
4. `MLC-064a` — **Done (2026-06-08)** (доработка Фазы 1, найдена на `MLC-065`) — честный сигнал
   недоступных процессов в host-пробе. `OneCHostMetricsProbe.ReadProcesses()` теперь **считает**
   `Win32Exception` (нехватка прав) в `HostMetricsSnapshot.ProcessesInaccessible` + производный
   `AttributionIncomplete` (вместо тихого глотания); псевдопроцессы Idle/System (PID 0/4) исключены
   (нечитаемы при любых правах → сигнал не кричит вхолостую под админом), `InvalidOperationException`
   (гонка выхода) не считается. Frontend — `AttributionWarningBanner` на `/performance` (амбер, образец
   IIS-permissions) по `attributionIncomplete`, Zod-граница расширена. Канон: ADR-26 + 04 §6 + 05 §3.8 +
   `OPERATIONS.md` «Быстродействие — required permissions». Чистые `ProcessFamilyMap`/`ProcessFamilyGrouping`
   не тронуты. Тесты: подсчёт/производный флаг на стабе (BE) + баннер (FE). Отчёт — в индексе «Закрыто» ниже.
5. `MLC-066` — **Done (2026-06-08)** — Backend: 1С-сеансы/процессы «кто грузит». `IClusterClient`
   расширен `ListSessionLoadsAsync` (perf-поля сеанса) + новой командой `ListProcessesAsync`
   (`rac process list`); нейтральные DTO `OneCSessionLoad`/`OneCProcessLoad` + `OneCLoadSnapshot`;
   endpoint `GET /performance/onec-sessions` (Viewer); гочи 063 (отрицательная память, инвариантный
   парс дробных/научной нотации) реализованы; +1 спавн/poll учтён в ADR-3.3. Отчёт — в индексе
   «Закрыто» ниже.
6. `MLC-067` — **Done (2026-06-08)** — Frontend: экран «кто грузит внутри 1С» (`features/performance/`,
   секция ниже host-снимка на `/performance`). Таблицы сеансов (топ по `cpu-time-current`/`duration-current`,
   бейдж состояния blocked/long/active/silent/idle, колонка DBMS-доли) и рабочих процессов
   (`available-perfomance` с тинтом, `avg-call-time`, память, pid); Zod-граница `oneCLoadSnapshotSchema`,
   polling 5с (`placeholderData: prev`), nullable perf → «—». **Фаза 2 завершена.** Отчёт — в индексе ниже.
7. `MLC-068` — **Done (2026-06-09)** — Backend: SQL DMV realtime-проба. `ISqlPerformanceProbe` +
   Infrastructure-адаптер `SqlPerformanceProbe` (чистый ADO.NET, **не** Windows-only — как
   `SqlDatabaseDiscovery`): активные запросы + цепочки блокировок (`sys.dm_exec_requests`/`sql_text`),
   IO-stall по базам (`sys.dm_io_virtual_file_stats`), дельта wait-stats (`sys.dm_os_wait_stats`) через
   singleton-стейт (первый poll → `Measuring`); атрибуция database→Infobase→tenant по `DB_NAME`
   (признак 1С — `program_name='1CV83 Server'`, SQL→сеанс→юзер не закладывалась); честный degraded
   при отсутствии `VIEW SERVER STATE` (`Status=PermissionDenied`)/недоступности (`Unavailable`). Endpoint
   `GET /performance/sql` (Viewer, vertical slice — атрибуция из своего `AppDbContext`). Канон: ADR-26 +
   04 §6.2 + OPERATIONS «Быстродействие — required permissions». Отчёт — в индексе «Закрыто» ниже.
8. `MLC-069` — **Done (2026-06-09) — ЗАВЕРШАЕТ ФАЗУ 3** — Frontend: вкладка SQL-активности. Секция
   `SqlLoadSection` ниже `OneCLoadSection` на `/performance` (третий live-источник — свой
   `useSqlPerformance`, своя загрузка/ошибка). Zod-граница `sqlPerformanceViewSchema` (`snapshot`+`databases`,
   enum `status` строкой); **урок MLC-067 применён превентивно** — nullable через `omittable()` (не
   `.nullable()`) + схема-тест на СЫРОМ omit-null ответе. Чистые хелперы `sqlLoad.ts` (атрибуция база→клиент
   регистронезависимо, агрегация по базе/клиенту, сортировка по ЦП, `collectBlockerIds` для цепочек,
   `formatInt`). Компоненты: `SqlActiveRequestsTable` (топ по ЦП, бейджи `1С`/`ждёт сеанс N`/`блокирует`,
   текст запроса+tooltip, nullable→«—»), `SqlDatabaseLoadTable` (по базе/клиенту), `SqlContentionTables`
   (дельта wait + IO-stall; `measuring`→«измеряю…»), `SqlStatusBanner` (degraded `PermissionDenied`/
   `Unavailable`, образец MLC-064a). i18n `performance.sql.*`. Канон §3.8 present-tense. Тесты +20 (283),
   type-check/lint/build зелёные; **live-verify на стенде под нагрузкой** (Zod на реальном omit-null,
   активные 1С-запросы с атрибуцией, дельта wait/IO, polling). Отчёт — в индексе «Закрыто» ниже.
9. `MLC-070` — **Done (2026-06-09)** — Backend: Recording (запись по требованию). Сущности-телеметрия
   `PerfRecording`/`PerfRecordingSample` в `Infrastructure.Reporting` (рядом с `LicenseUsageSnapshot`, НЕ в
   адаптерном `Infrastructure.Performance` — guard `LayerBoundaryTests`); host-метрики плоскими колонками +
   JSON-колонки (семьи / топ-виновники 1С/SQL через общий `PerfSampleJson`); FK-cascade; миграция
   `MLC070PerfRecordings` (BOM/LF нормализованы). Singleton `IPerfRecordingService`/`PerfRecordingService`
   (одна активная запись + `SemaphoreSlim`, БД/`IClusterClient` через `IServiceScopeFactory` — паттерн
   `HotTierPollingService`) + фоновый `PerfRecordingSamplingService`; тик пишет host (`IHostMetricsProbe`) +
   топ-N сеансов/процессов 1С + SQL-снимок; **авто-стоп** по `Performance.RecordingMaxDurationMinutes`/
   `…MaxSamples` (что раньше), интервал `…RecordingSampleIntervalSeconds` (деф. 15с); время через
   `TimeProvider`; рестарт → осиротевшая `Active`→`Interrupted`. API `/performance/recordings`: старт/стоп/
   удаление = **Admin** (409 `RECORDING_ACTIVE` на повторный старт / удаление идущей), список/просмотр =
   `Viewer`. Аудит старт/стоп НЕ заведён (ADR-26). Тесты +29 (512): сервис старт/стоп/авто-стоп (TimeProvider)/
   recover, persistence+FK-cascade (SQLite), API + роль-гейт по метаданным. Канон ADR-26 (present-tense) +
   ADR-3.3 спавн-каденция + 04 §6.3. Отчёт — в индексе «Закрыто» ниже.
10. `MLC-071` — **Done (2026-06-09)** — Frontend: Recording UI (трек «Анализ быстродействия 1С», Фаза 4 —
    **завершает трек**). Секция `RecordingSection` на `/performance` ниже трёх live-источников — единственный
    персистируемый источник. Управление (старт/стоп/удаление) = `Admin` (гейт по `useMe`), список/просмотр =
    `Viewer`. Кнопка старт/стоп + пульсирующий индикатор «идёт запись» (виден всем); 409 `RECORDING_ACTIVE` на
    повторный старт/удаление идущей → тост через `matchConflictCode`; стоп/удаление подтверждаются `AlertDialog`
    (06_UI_DESIGN). Список расследований (`RecordingsTable`: старт/окончание/статус+stop-reason/кто/число
    сэмплов, ряд → просмотр). Просмотр (`RecordingDetailDialog`): график host во времени (`RecordingHostChart`,
    recharts `ComposedChart` — ЦП%/память% на оси 0–100, латентность диска мс на правой) + топ-виновники 1С/SQL
    за период (реюз таблиц 067/069 на пиковых-за-период срезах, чистый `recordingAggregation.ts`) + экспорт
    (`RecordingExportMenu`: CSV синхронно + Excel lazy SheetJS, образец `features/reports/export`). **Zod-граница
    через `omittable()`** (`stoppedAtUtc`/`stopReason`/`oneC`/`sql` опускаются бэкендом) + схема-тест на сыром
    omit-null ответе (урок `api-omits-null-fields`). Хуки `useRecordings` (polling 5с) + мутации через
    `useInvalidatingMutation`. i18n `performance.recording.*`. +21 тест (304 FE): схема omit-null, агрегация,
    хуки, CSV, регресс на коллизию queryKey. type-check/lint/build зелёные. Канон present-tense
    `05_UI_REQUIREMENTS.md` §3.8 (запись как построенная). **Live e2e пройден на нагруженном стенде** (admin,
    перепроведение документов): старт→34 сэмпла по таймеру→стоп→просмотр (график host + пиковые виновники:
    BackgroundJob ЦП 2.4с, SQL `INSERT #tt20` по `mitpro`)→CSV-экспорт; **поймал и пофикшен баг** — `useRecordingDetail(null)`
    использовал `recordingsQueryKey` как fallback и подхватывал массив-список → `data.recording.status` падал у
    закрытого диалога (JSX-проп вычисляется до размонтирования Radix); фикс — обособленный ключ детали +
    регресс-тест. ADR не затронуты. Отчёт — в индексе «Закрыто» ниже.

**Трек «Анализ быстродействия 1С» (`MLC-063..071`) завершён 9/9.** Раздел «Быстродействие» отвечает на
«почему 1С тормозит» тремя live-источниками (host-снимок → 1С-сеансы → SQL) и записью по требованию для
расследования во времени. Фаза 5 (UI-холистик, `MLC-062` движок таблиц) — отдельный трек, вне этого, gated.

### Трек «Резервное копирование баз SQL» (открыт 2026-06-09)

Постановка пользователя (админ инфраструктуры); нарезан куратором. Кнопка «сделать full-бэкап» базы клиента
на карточке инфобазы для Admin и **Viewer** (операторов). Все базы в SIMPLE; на части серверов крутится
внешний дифференциальный шедулер. **Ключевое решение:** бэкап всегда `COPY_ONLY` (восстановим, но не сбрасывает
differential base → не ломает внешнюю дифф-цепочку), поэтому признак «diff/full» сервера в коде не нужен.
Деплой single-node (`ConnectionStrings:Default`, локальный диск SQL-сервера, учётке панели выдаётся
**sysadmin** — нужно для `xp_*`). Хранение «1 свежий бэкап на базу (новый-перед-удалением) + TTL 24ч»,
очередь с потолком параллельных и замком на базу, проверка свободного места по оценке размера базы. Restore —
**вне объёма** (оператор через SSMS).

**Упирается в Locked-ADR:** ADR-15 держал бэкап «permanently out of scope» (revoke требует явной правки) →
`MLC-075` **изменяет ADR-15** (расщепление: 2FA и *плановая* оркестрация остаются out-of-scope; добавлено
узкое on-demand-исключение) + заводит **ADR-27**. Полная спека (контекст/развилки/файлы/SQL-механика/тесты/
риски/проверка) — в план-файле `C:\Users\andre\.claude\plans\lazy-cuddling-finch.md`. Берём по одной за сессию:

1. `MLC-075` · Architecture · P2 · **Done (2026-06-09)** — Канон-фундамент (decide-before-code, doc-only):
   изменить **ADR-15** (снять backup-«locked», расщепив 2FA / плановую оркестрацию / новое on-demand-исключение)
   + завести **ADR-27** (on-demand `COPY_ONLY` бэкап: адаптер ADR-20, оркестратор-насос + очередь-таблица,
   keep-latest+TTL, server-side `xp_*`, disk-guard по оценке, роли Viewer/Admin, слоты аудита 510–514, sysadmin).
   Present-tense описания фич в `04`/`05`/`OPERATIONS` — в код-задачах как built.
2. `MLC-076` · Backend · P2 · **Done (2026-06-09)** — Фундамент: settings-ключи (`Backup.FolderPath`/`TtlHours`/
   `MaxParallel`/`DiskSafetyMarginMb`) + definitions; сущность `DatabaseBackup` (`Infrastructure.Reporting`,
   без FK) + миграция `MLC076DatabaseBackups`; порт `ISqlBackupService` + адаптер `SqlBackupAdapter`
   (вся SQL-механика: sysadmin→оценка→место→mkdir→BACKUP COPY_ONLY+VERIFY→delete-older, never-throws);
   слоты аудита 510–514; `FakeSqlBackupService` (+`BackupGate` для конкурентных тестов 077); layer-guard
   пополнен; канон 04 §4/§7/§8 + OPERATIONS «Бэкап — required permissions». Отчёт — в индексе «Закрыто».
3. `MLC-077` · Backend · P2 · **Done (2026-06-10)** — Оркестрация + API: `IBackupOrchestrator`→`BackupOrchestrator`
   (singleton; очередь = таблица, FIFO + замок-на-базу + пер-тик потолок `Backup.MaxParallel`; BACKUP вне gate) +
   `BackupPumpService` (wake-или-таймаут; recovery осиротевших `Running`→`Failed/Interrupted` на старте) +
   `BackupRetentionJob` (recurring 03:15 UTC: server-side файлы по DISTINCT-парам + reap строк батчами под
   execution strategy, паттерн MLC-074) + эндпоинты `/api/v1/backups` (list/detail/start=Viewer, delete=Admin;
   409 `BACKUP_ACTIVE`/`BACKUP_FOLDER_NOT_CONFIGURED`/`BACKUP_DELETE_FAILED`) + DI; 583 теста; канон 04
   §8.1–8.3. Отчёт — в индексе «Закрыто».
4. `MLC-078` · Frontend · P2 · **Done (2026-06-10)** — Frontend + live e2e: фича `features/backups/` (диалог
   бэкапов инфобазы с поллингом 5с и статус-бейджами; Zod-граница через `omittable()` — 5 nullable-полей
   `BackupSummary` опускаются на проводе, незнакомый enum-статус деградирует, не роняя список), иконка-кнопка
   «Бэкапы базы» на строке инфобазы (видна Viewer и Admin — НЕ в admin-дропдауне), Admin-удаление через
   `AlertDialog`, секция `Backup.*` на `/settings`, i18n `backups.*` + 5 слотов аудита 510–514; 409-тосты
   через `matchConflictCode`; +24 теста (328 FE). **Live e2e на стенде пройден полностью** (реальный SQL;
   потолок MaxParallel=2 пойман: 2 Running + 1 Queued; keep-latest файлово; Viewer-прогон; консоль чистая).
   Канон 05 §3.9. Отчёт — в индексе «Закрыто».

**Трек «Резервное копирование баз SQL» ЗАВЕРШЁН 4/4 (MLC-075..078).**

---


---

## Чистка реестра 2026-06-10 — полные записи бывшего индекса «Закрыто» (MLC-029..078)

> Для `MLC-029..055` развёрнутые пер-задачные секции есть выше в этом архиве — записи ниже их дублируют
> в сжатой форме (сохранены при чистке без потерь). Для `MLC-056..078` записи ниже — **единственные**
> полные отчёты (дисциплина переноса в архив была нарушена после MLC-055, восстановлена этой чисткой).

- `MLC-029` (REF-01) — Дедуп маппинга `Publication` request→entity в `InfobasesEndpoints` (приватный хелпер `ApplyPublicationFields` + две перегрузки-адаптера), поведение 1:1, ADR-20 не затронут — Done (2026-06-03)
- `MLC-030` (REF-02) — Архитектурные guard-тесты границ слоёв на NetArchTest (`Architecture/LayerBoundaryTests.cs`): Domain без зависимостей, Application без Infra/Web, Web без прямых адаптеров 1С/IIS (ADR-5/16/20) — Done (2026-06-03)
- `MLC-031` (REF-03) — Фабрика CRUD-mutation хуков `useInvalidatingMutation` (`frontend/src/lib`): generic по переменным мутации, один ключ / массив ключей / функция-резолвер, опц. доп-`onSuccess`; 10 хуков 5 фич переведены, поведение/политика инвалидации 1:1 — Done (2026-06-03)
- `MLC-032` (REF-04) — Декомпозиция крупных FE-страниц Audit/Publications/Sessions на контейнер + оркестрационный хук (`use<Page>`) + презентационные части (`<Feature>Table`/`<Feature>FiltersBar`, Audit ещё `auditUrlState.ts`/`AuditPagination.tsx`) по образцу MLC-023; JSX/i18n/колонки/фильтры/пагинация/polling/диалоги 1:1, query-ключи и контракты не тронуты, code-splitting сохранён — Done (2026-06-04)
- `MLC-033` (REF-05) — Обобщённый conflict→descriptor маппер `matchConflictCode<T>` + общий хвост submit форм `toastFormSubmitError` (`frontend/src/lib/apiErrors.ts`, надстройка над `lib/api`, контракт `ConflictBody`/`readConflictBody` не тронут); `mapConflictToField` + 4 диалога переведены, диалоги-подтверждения иной природы (Kill/Reconcile/DeleteInfobase) не тронуты, поведение/сообщения 1:1 — Done (2026-06-04)
- `MLC-034` (REF-06) — Тонкий Web-аудит-фасад `HttpContext.AuditAsync` (`EndpointHelpers.cs`, форма «делегат-описание»): инкапсулирует `ResolveInitiator()` + плумбинг `initiator`/`ct`, `AuditActionType`/`AuditDescriptions.*` остаются явными в строке вызова; 9 каноничных сайтов (Infobases/Tenants/Publications) свёрнуты, парные записи раздельны, состав/порядок/условность журнала 1:1; Sessions/Settings/Auth вне объёма; ADR-20 не затронут — Done (2026-06-04)
- `MLC-035` (REF-07) — Группировка плоского `Web/Endpoints` по подпапкам-фичам (Infobases/Tenants/Publications/Sessions/Settings/Auth/Audit/Discovery/Dashboard/Health) + общий `Shared/` (EndpointHelpers/Problems/DbUniqueViolation/AuditDescriptions/InfobaseValidationRules); IDE0130 не энфорсится → выбран плоский namespace `MitLicenseCenter.Web.Endpoints` (минимальный churn: `git mv` без правок контента, using/`Program.cs`/тесты не тронуты); поведение/маршруты/контракты/аудит/регистрация 1:1. **Завершает Phase 1–2 рефакторинг-трека.** — Done (2026-06-04)
- `MLC-037` (PERF-01) — Инструментирование горячего пути: лёгкий слой `Meter` (`System.Diagnostics.Metrics`) в единственной точке спавна `SystemProcessRacRunner.RunAsync` (Counter `rac.exe.spawns` + Histogram длительности, тег `command`/`outcome`) и на cold/hot-цикле (`reconciliation.cold/hot.duration`, `kills`, ObservableGauge `hot_tenants`) поверх существующих `Stopwatch`; near-zero overhead под гардом `Enabled`, поведение 1:1. Baseline снят `dotnet-counters` (спавны видны, кросс-чек с логом; счётчик = полный спавн-бюджет, надмножество логов), процедура — в `docs/OPERATIONS.md` «Наблюдаемость перфа». Фундамент перф-трека (разблокирует измеримость; триггер-замер для `MLC-036`). ADR-16/3.3/6/15/20 не затронуты. — Done (2026-06-04)
- `MLC-039` (PERF-03) — Нагрузочный seed-харнесс (dev/test-only проект `backend/tools/MitLicenseCenter.Tools.PerfHarness`, `/tools/` в slnx, не в прод-publish): seed-режим засевает N клиентов / M инфобаз+публикаций (1:1) / K строк аудита через реальный `AppDbContext` (FK + уникальные индексы соблюдены, миграции не тронуты), пишет `scenario.json`; rac-stub-режим (за существующим `SystemProcessRacRunner`, новый адаптер не введён — ADR-16) отдаёт S синтетических сессий с over-limit тенантами, прогоняя cold/hot/kill-путь под ростом. Замер «до→после» снят `dotnet-counters`: `hot_tenants` 6→60 (ровно ×10), cold/hot.duration растут с числом сессий, спавн-бюджет каденционно-ограничен (в рамках ADR-3.3). 11 unit-тестов; процедура + ростовые точки — в `docs/OPERATIONS.md`. ADR-6/16/3.3/20/single-node/RU-only не затронуты. — Done (2026-06-04)
- `MLC-038` (PERF-02) — Профиль наблюдаемости EF + захват baseline запросов: опт-ин гейт `Diagnostics:EfQueryProfiling` (+ отдельный `EfSensitiveDataLogging`, gated) навешивает `LogTo`(CommandExecuted) на `AddDbContext` (`Infrastructure/Diagnostics/EfQueryProfiling.cs` + 7 unit-тестов); по умолчанию выключен, прод-логи/поведение 1:1 (`Database.Command=Warning` не тронут), подтверждено рантаймом (флаг off → 0 EF-command-строк, файл-приёмник не создаётся). Снят baseline+×10 (харнесс MLC-039, Perf-БД): сгенерированный SQL + warm-тайминги четырёх эндпоинтов + Actual Execution Plan аудита. Вывод: дорогой растущий паттерн — фильтр-список аудита по `TenantId` + `ORDER BY Timestamp DESC,Id DESC` (Sort+key lookup, logical reads 2241→7480 на ×10, SQL сам просит индекс impact 69%) — прямой вход в **PERF-06**; `/sessions/snapshot` = 0 EF; коррелированный COUNT `/tenants` подтверждён как не-N+1. Запросы не менялись (наблюдение). Результаты — в `docs/OPERATIONS.md`. ADR-20/16/3.3/single-node/RU-only не затронуты. — Done (2026-06-05)
- `MLC-040` (PERF-04) — Readiness-проба зависимостей: новый анонимный `GET /api/v1/health/ready` (liveness `/health` оставлен дёшевым 1:1) с тремя read-only пробами под таймаутом 2с — БД `CanConnectAsync` (единственная гейтит `not_ready`/`503`), RAS через готовый снапшот `IRasHealthReader` (ok/degraded/unknown, **без нового спавна `rac.exe`** — ADR-16/3.3), Hangfire-сторадж `GetStatistics()`; RAS-Сбой и Hangfire-down → `degraded`/`200` (single-node). Тело санитизировано (ADR-4.1/MLC-009): только грубые суб-статусы, сырые ошибки — в журнал сервера. Чистый агрегатор `ReadinessEvaluator` + 13 тестов. Замер «до→после» (`dotnet-counters`): суб-статусы различаются (db/ras/hangfire), 650+ health-запросов дали **0** записей `rac.exe.spawns` (контроль `System.Runtime` активен → сбор живой), DB-down→503 покрыт юнит-тестом (fail-fast ADR-18 не даёт стартовать против опущенной БД). ADR-18/22/20/single-node/RU-only не затронуты. — Done (2026-06-05)
- `MLC-041` (PERF-05) — Кросс-вызовный кэш резолва UUID кластера: новый singleton `IClusterUuidCache`/`ClusterUuidCache` (ключ `(ExePath, Endpoint)`, один слот — single-node даёт один кластер) под `lock`, инжектится в scoped `RacExecutableRasClusterClient`. `ResolveClusterUuidAsync` сначала бьёт по кэшу → тёплый кэш убирает лишний спавн `cluster list` перед `session list`/`session terminate`/`infobase summary list`; кэшируется только успешный резолв. Инвалидация: смена endpoint/ExePath (пересбор ключа из TTL-снапшота → промах), и safety-net на non-zero exit cluster-scoped команды (stale-UUID → перерезолв на следующем вызове); идемпотентный `AlreadyGone` («Сеанс … не найден») кэш НЕ инвалидирует. Замер «до→после» (детерминированный харнесс сценария MLC-039, реальный адаптер + классификатор `RacCommandTag`, no-op-кэш = доформенное поведение 1:1): на hot=15/cold-lists=2/kills=5 `cluster.list` 22→**1**, TOTAL спавнов 44→**23**, kill-путь **2→1**/kill, hot-тик **2→1**. `session list`/`terminate` 1:1; идемпотентный kill (re-fetch+сверка дескриптора в `KillEnforcer`) не тронут — кэш влияет только на аргумент `--cluster=`. Новый адаптер/транспорт не введён (ADR-16/3.3; не RAS Strategy B = MLC-036). 7 новых unit-тестов (4 кэш/инвалидация + 3 замер-гарда), BE 315 / FE 118 зелёные. ADR-3.3/04_INFRASTRUCTURE спавн-каденция обновлены present-tense. ADR-16/21/20/single-node/RU-only не затронуты. — Done (2026-06-05)
- `MLC-042` (PERF-06) — Составной индекс `AuditLogs` под фильтр+сортировку: новый `IX_AuditLogs_TenantId_Timestamp_Id` `(TenantId, Timestamp DESC, Id DESC)` под дорогой растущий запрос `/audit` (фильтр по `TenantId` + `ORDER BY Timestamp DESC, Id DESC`); ключ убирает Sort и ограничивает key lookup размером страницы. Замер «до→после» (Perf-БД, тот же seed MLC-039): TenantId-only list logical reads 100k 2239→**166**, 1M 8244→**165** (план Sort+key-lookup → Index Seek; не растёт с таблицей); список без фильтра 1:1 (по-прежнему `IX_AuditLogs_Timestamp`, ~165). Одноколоночный `IX_AuditLogs_TenantId` удалён той же миграцией (составной с лидирующим `TenantId` его покрывает: TenantId-COUNT 16→27/21→19 reads — seek по композиту; число индексов на запись 4→4, INSERT-нейтрально). `ActionType`-композит и INCLUDE НЕ вводились — план показал `ActionType`-фильтр едет по упорядоченному `IX_AuditLogs_Timestamp`+`Top` без Sort (1099→1108 = без эффекта), а covering раздул бы индекс из-за `Description nvarchar(max)`. Запросы эндпоинта, retention `DELETE`, enum int-стабильность 1:1. Миграция нормализована (UTF-8 без BOM, LF). BE 316 / FE 118 зелёные. `03_DOMAIN_MODEL.md` §4 (индексы AuditLog) present-tense. ADR-20/16/3.3/single-node/RU-only не затронуты. — Done (2026-06-05)
- `MLC-043` (PERF-07) — Батч-загрузка публикаций в `DriftCheckJob` (N+1 → 1): `RunAllAsync` раньше грузил все `Publication.Id`, затем `foreach (id) → FirstOrDefault` на каждую (N+1 round-trips/проход). Теперь — один проекционный `AsNoTracking`-запрос лёгкого `record DriftSnapshot` строго нужных проверке полей (без тяжёлого `VrdCustomXml`/tracking), обход материализованного списка. `CheckOneAsync` (on-demand reconcile/check-drift) переведён на тот же снимок для одной публикации — единая `ProcessOneAsync`, без дублирования. Запись результата — targeted-UPDATE по `Id` (`Local`-аware: mutate уже-трекаемой иначе `Attach` транзиента + `Modified` только 3 drift-колонки; `ExecuteUpdate` отпал — InMemory его не транслирует), эффект `SaveChanges` 1:1. `ct`/устойчивость к ошибке отдельной публикации (внешний `try/catch` вокруг `foreach`) — 1:1. Замер «до→после» реальными SQL round-trip'ами (`DbCommandInterceptor`, SQLite = та же EF-трансляция): на N=25 загрузочных SELECT/проход **26 → 1**. 20 drift+reconcile тестов 1:1 (ADR-4.1: 210 только на переходе в не-InSync), +1 регресс-гард `DriftCheckBatchQueryTests`. BE 316 / FE 118 зелёные. `OPERATIONS.md` PERF-07 present-tense. ADR-4.1/20/16/3.3/single-node/RU-only не затронуты, smoke не сломан. — Done (2026-06-05)
- `MLC-044` — Hot-тир теперь enforce'ит (near-realtime kill ≤5с) + быстрый экран: до этого kill шёл только на cold-цикле (≈25с), hot-цикл лишь обновлял снимок — расхождение с ADR-6 («окно ≤5с»). Теперь `HotTierPollingService.RunCycleOnceAsync` после fetch/overlay вызывает `IKillEnforcer.EnforceAsync` строго по hot-тенантам, **переиспользуя единственный список тика** как fresh-проверку (без второго спавна `rac.exe`; `EnforceAsync` принял `freshSessions?`). Защита от over-kill (MLC-001): оба пути (cold Hangfire-джоб + hot BackgroundService, на который `[DisableConcurrentExecution]` не действует) берут общий in-process замок `IEnforcementGate` (singleton `SemaphoreSlim(1,1)`); hot берёт его **до** fetch'а, поэтому cold не вклинится между fetch и kill. Идемпотентный протокол (re-fetch + сверка дескриптора, аудит на `Killed||AlreadyGone`, newest-first, cap 20) — 1:1. FE-опрос `/sessions/snapshot` и `/dashboard/summary` 15с→5с. Замер «до→после» (детерминированный харнесс, реальный путь `RunCycleOnceAsync`+`EnforceAsync`, прокси спавнов = вызовы `IClusterClient`): kill latency **≈25с→≤4с**; `session.list`/hot-тик **1** (переиспользован, не 2) → ~15/мин ≤26/мин (ADR-3.3); `session.terminate` 0 на `Consumed==Limit`. Тест отсутствия over-kill: конкурентные cold+hot убивают ровно `Consumed-Limit` (не `2×`), без двойного аудита (`HotColdEnforcementOverKillTests`) + `EnforcementGateTests` (взаимоисключение) + `HotEnforcementMeasurementTests`. BE 322 / FE 119 зелёные. Канон present-tense: ADR-6/6.1 (расхождение закрыто), 02 Concurrency Control, 04 §5. Новый адаптер не введён (ADR-16/3.3; не RAS Strategy B = MLC-036). ADR-20/single-node/RU-only не затронуты. — Done (2026-06-05)
- `MLC-046` — Публикации: **массовые операции** (bulk publish через webinst + bulk change-platform через web.config) поверх одиночных MLC-045. Постановка пользователя. Фронт оркеструет существующие одиночные эндпоинты пулом (параллелизм 3) — пачка = N идемпотентных вызовов (надёжность = переиспользование протестированного пути; без bulk-эндпоинта/контрактов/миграций/enum). Backend: единственное — замок `IWebinstConcurrencyGate` (singleton `SemaphoreSlim(3)`) вокруг спавна webinst (кэп независимо от клиента, семья ADR-3.3). Frontend: shadcn `checkbox` + мультиселект (header=выбрать отфильтрованные), bulk-бар, единое подтверждение перезатирания со списком gated (Source≠Webinst&&Published), bulk-смена платформы (одна версия из установленных), generic пул-хук `useBulkOperation` + прогресс-диалог (частичный успех, отмена, снятие успешных для повтора). Аудит 212/213 — по-публикационно (без изменений). Tab-bound прогон; tab-independent (Hangfire) — отложенная опция ADR-4. BE 329 / FE 132 (+`dotnet format`/lint/type-check/build) зелёные. Канон present-tense: ADR-4, 04/05/OPERATIONS. — Done (2026-06-05)
- `MLC-047` — Управление жизненным циклом IIS из веб-панели: новый Application-порт `IIisLifecycleService`/`OneCIisLifecycleService` (recycle/start/stop пула, start/stop/restart сайта, server-wide `iisreset`) + блок «Управление IIS» над списком публикаций; discovery из live IIS (`ServerManager`/`ServiceController` W3SVC), мутации сериализованы `IIisResetConcurrencyGate`, аудит `IisApplicationPoolRecycled=220..IisStarted=228`, подтверждение деструктивных операций. ADR-24 (+ уточнение ADR-4 read-only); ADR-20 граница без изменений. — Done (2026-06-06)
- `MLC-048` — Сбор time-series использования лицензий (фундамент трека «Отчёты»): ADR-25 + таблица-телеметрия `dbo.LicenseUsageSnapshots` (`Infrastructure/Reporting`, FK SetNull, индекс `(TenantId, BucketStartUtc)`) + миграция `MLC048LicenseUsageSnapshots` (нормализована); singleton `ILicenseUsageAccumulator`/`LicenseUsageAccumulator` (бакеты 15 мин, running min/max/avg, флаш на границе) с врезкой в cold `ReconciliationJob.RunColdAsync` (семпл по активным тенантам, идл=0, вне enforcement-замка, **без нового спавна `rac.exe`**); настройка `LicenseUsage.RetentionDays` + `LicenseUsageRetentionJob` (cron `30 3`, batched portable `ExecuteDelete`, без аудита). Слой-граница: Application видит нейтральные `LicenseUsageSample`/`LicenseUsageBucket`, не entity. 15 новых тестов; BE 377 зелёные. Канон present-tense: ADR-25, 04/OPERATIONS/ROADMAP/00_INDEX. — Done (2026-06-06)
- `MLC-049` — Reports API поверх собранной `MLC-048` таблицы: папка-фича `Web/Endpoints/Reports/` (`ReportsEndpoints`+`ReportsContracts`, namespace `MitLicenseCenter.Web.Endpoints`), `MapGroup("/api/v1/reports")`, `RequireAuthorization(Roles.Viewer)`, регистрация в `Program.cs` рядом с Dashboard. Прямая инъекция `AppDbContext` (ADR-20) + `TimeProvider` (дефолт `to=now`), `AsNoTracking`, фильтр `BucketStartUtc ∈ [from,to]`, сортировка по `BucketStartUtc` ASC. `GET /reports/license-usage` (сводка: DB-side `GroupBy(BucketStartUtc)`, на бакет `ConsumedMax`/`ConsumedAvg`/`Limit` = Σ по тенантам; **осиротевшие `TenantId=null` включены** — история платформы не усыхает при удалении клиента, прецедент AuditLog) + `GET /reports/license-usage/{tenantId}` (drill-down: хранимые значения как есть, null-тенант не достаётся, несущ. tenant → пустой ряд, не 404). Единый контракт `LicenseUsageSeriesResponse { Buckets:[{BucketStartUtc, ConsumedAvg, ConsumedMax, Limit}], FromUtc, ToUtc, PeakConsumed, PeakLimit, PeakAtUtc, AverageConsumed }`. Диапазон: дефолт последние 7 дней, кламп ширины 31 день (двигает `from` вперёд, эффективный диапазон в ответе), `to<from` → `ValidationProblem`, пустой ряд = `200` (не ошибка, под empty-state FE). 9 новых тестов (образец `DashboardSummaryTests`); полный CI зелёный. Read-API в рамках ADR-25 (нового ADR нет). Канон present-tense: `03_DOMAIN_MODEL.md` §«Persistence & API Contracts», `OPERATIONS.md`. ADR-20/single-node/RU-only не затронуты. — Done (2026-06-06)
- `MLC-050` — Frontend, раздел «Отчёты» (`/reports`): фича `features/reports/` по образцу `features/audit/` (контейнер `ReportsPage` + оркестрационный `useReportsPage` + презентация), хуки TanStack Query `useLicenseUsage`/`useLicenseUsageByTenant` (query через `URLSearchParams`, границы только если заданы — дефолт/кламп на сервере), URL-state `from`/`to`/`tenant`. **Первый график на recharts** — `LicenseUsageChart` (`ComposedChart` в `ResponsiveContainer`: area `consumedMax` + линии `consumedAvg`/`limit`-пунктир, ось времени date-fns/ru), один компонент на сводку и drill-down. Сводка (`LicenseUsageSummary`: эффективный период из `fromUtc`/`toUtc`, стат-тексты пик/среднее, **заметная оговорка** про обзорность суммы по бакету) + отдельный блок детализации (`ReportsDetail`: Select клиента → ряд тенанта) + обязательный empty-state «данные накапливаются» (пустой ряд = 200). Роут lazy `/reports` под `ProtectedRoute` (не admin), пункт `LineChartIcon` в «Операциях», i18n `nav.reports`+`reports.*` (ru). Бандл: recharts+d3/redux вынесены в отдельный eager-чанк `charts` (vite.config) — все чанки < 500 кБ. 2 новых теста (url-state + хуки), FE 145 зелёные; type-check/lint/preview/полный CI зелёные. Канон present-tense: `05_UI_REQUIREMENTS.md` §3.6 (+«Администраторы» →§3.7), `06_UI_DESIGN.md` (Charts/sidebar), `ROADMAP.md`. **Трек «Отчёты» завершён 3/3.** ADR не трогали (UI в рамках ADR-25). — Done (2026-06-06)
- `MLC-045` — Публикации 1С через `webinst` + смена платформы правкой `wsisapi.dll` в `web.config` (read-only статус вместо drift, ADR-4; ADR-4.1 revoked). Постановка пользователя: новые базы быстро публикуются через `webinst`, версия платформы меняется правкой только `wsisapi.dll` в `web.config` (default.vrd не трогается), enforcement-reconcile не нужен. Бэкенд: новый адаптер `IWebinstPublisher`/`OneCWebinstPublisher` (ADR-20, путь к exe из версии через `WebinstExeResolver`/`OneCInstallRoots`, UTF-16LE-декод вывода, connstr из `OneC.Cluster.Server`+имя ИБ); `IIisPublishingService.ChangePlatformAsync` (хелпер `WsisapiVersionRewriter` — переиспользованный regex из удалённого `VrdPatcher`); `PublicationStatusEvaluator` + `PublicationStatusRefreshJob` (read-only, без аудита) вместо `PublicationDriftDetector`/`DriftCheckJob`; эндпоинты `POST /publications/{id}/check|publish|change-platform` (publish гейтит перезатирание не-`Webinst` публикаций по `Confirm`); аудит `PublicationPublished=212`/`PublicationPlatformChanged=213` (210/211 — historical, не пишутся). Домен: убраны `EnableOData`/`EnableHttpServices`/`VrdCustomXml`, добавлен `Source` (Unknown/Webinst/Configurator), drift-поля → `LastCheckStatus`/`LastCheckAt`/`LastCheckDetails`; миграция `MLC045WebinstPublishing` (drop + rename + сброс статуса в Unknown). Настройка `OneC.Cluster.Server`. Фронт: колонки «Источник»/«Статус», действия «Проверить сейчас»/«Опубликовать»/«Сменить платформу» (+ `PublishPublicationDialog`/`ChangePlatformDialog`), убраны OData/HTTP-поля формы и parity-правила. BE 326 / FE 119 (+type-check/lint) зелёные. Канон present-tense: DECISIONS ADR-4 (+ADR-4.1 revoked), 01/03/04/05/ROADMAP/00_INDEX/OPERATIONS. connstr-auth для webinst **рассмотрен и отклонён** (2026-06-08): в офиц. доке 1С механизма передачи пароля в webinst нет → не реализуем, webinst публикует без auth; `OneC.Cluster.Admin*` остаются только за rac.exe. ADR-20/16/3.3/single-node/RU-only не затронуты. — Done (2026-06-05)
- `MLC-051` — Экспорт отчётов `/reports`, каркас + CSV + XLSX (клиентский, без нового API): модуль `features/reports/export/` (`downloadBlob` + `exportFilename` + `toCsv` + `toXlsx` + `ExportMenu`), меню «Скачать» в обоих разрезах (сводка `scope="all"` / детализация `scope={{tenantName}}`) по отдельности, скрыто при пустом ряде. CSV — RU-Excel-дружелюбно (UTF-8 BOM, `;`, десятичная запятая, дата `dd.MM.yyyy HH:mm`); XLSX (SheetJS, dynamic import) — два листа «Сводка»/«Данные», числа числовыми. Новый rolldown-чанк `export-libs` под `xlsx` (vite.config, грузится только по клику, <500 кБ). 9 новых тестов (toCsv/exportFilename/toXlsx/ExportMenu), FE 162 зелёные; type-check/lint/build чистые. Канон present-tense: `05_UI_REQUIREMENTS.md` §3.6. ADR не трогали (UI в рамках ADR-25). — Done (2026-06-07)
- `MLC-052` — Экспорт отчётов `/reports`, форматы HTML + PDF (клиентские, без нового API): `toHtml` — самодостаточный офлайн-файл с интерактивным графиком на vanilla Chart.js (исходник UMD инлайнится в документ через виртуальный модуль `virtual:chartjs-umd-src` — плагин читает файл установленного пакета, без вендоринга и без esbuild-предбандла dev-сервера, что ломал бы `?raw`), воспроизводит график панели (area `consumedMax` + линии `consumedAvg`/`limit`-пунктир) + сводка + таблица. `toPdf` — jspdf + jspdf-autotable: заголовок/сводка/картинка графика (offscreen Chart.js → PNG, гард при отсутствии canvas) / таблица; встроен кириллический сабсет **Roboto Regular (Apache-2.0)** через `addFileToVFS`/`addFont` (станд. шрифты jsPDF без кириллицы); `compress:true` (fflate) — PDF 5.9 МБ→145 кБ. `chartConfig.ts` — общий источник данных/опций. Чанки: `export-libs`→ per-format `export-xlsx`/`export-chart`/`export-pdf` (один чанк был бы ~1.4 МБ); неиспользуемые опц-зависимости jsPDF (html2canvas/canvg/dompurify, только `.html()`/SVG) застаблены алиасом, иначе текли в eager-vendor; общий `__vitePreload`-хелпер изолирован в свой чанк, иначе тянул `export-pdf` в preload. `core-js` (опц. деп jspdf) помечен не-собираемым (`pnpm-workspace.yaml allowBuilds`). Все чанки <500 кБ. 8 новых тестов (toHtml/toPdf), FE 170 зелёные; type-check/lint/build чистые. Проверено в браузере: HTML рисует график офлайн (скрин), PDF — кириллица читается (`pdftotext`: «Использование лицензий — ООО «Ромашка»», шапка таблицы), `FontFile2`+`/Image` встроены. Канон present-tense: `05_UI_REQUIREMENTS.md` §3.6. **Трек «Экспорт отчётов» завершён 2/2.** ADR не трогали (UI в рамках ADR-25). — Done (2026-06-07)
- `MLC-053` — dev/ops-утилита сброса пароля администратора без потери данных: новый verb `reset-admin` в dev/test-only бинаре `MitLicenseCenter.Tools.PerfHarness` (`IsPublishable=false`, вне прод-publish) рядом с `seed`/rac-stub. Поднимает `Host.CreateApplicationBuilder` + `AddInfrastructure(...)` (строка подключения через `AddInMemoryCollection` → `ConnectionStrings:Default`), резолвит `UserManager<AppUser>` из scope **без запуска хоста** (хостед-сервисы/IIS-адаптеры конструируются лениво, не стартуют; eager-валидация контейнера отключена `ServiceProviderOptions{ValidateOnBuild=false}` — иначе падала бы на `SignInManager` без `IAuthenticationSchemeProvider` Web-слоя). Сброс через штатный `GeneratePasswordResetTokenAsync`→`ResetPasswordAsync` (корректный Identity-хеш + та же парольная политика из `AddInfrastructure`, не голый SQL); `--unlock` снимает lockout (`SetLockoutEndDateAsync(null)`+`ResetAccessFailedCountAsync`). Флаги `--user` (дефолт `admin`)/`--password` (иначе криптослучайный, генератор `IdentitySeeder.GenerateInitialPassword` стал `internal` + `InternalsVisibleTo`, единый источник парити)/`--connection`; пароль печатается в stdout (как сидер), exit `0`/`2` (нет пользователя)/`3` (политика). PS-обёртка `scripts/reset-admin.ps1` (UTF-8 BOM, по образцу `perf-seed.ps1`, ставит `DOTNET_ENVIRONMENT=Development` для LocalAppData-key ring). 2 unit-теста генератора (политика + различимость); BE зелёные. Ручной прогон на dev-БД: генерация/явный/слабый(→exit 3)/несущ.(→exit 2)/`--unlock` — данные сохранены. Канон present-tense: `OPERATIONS.md` «Recovering admin access». Offline-утилита: контракты/эндпоинты/ADR не тронуты (ADR-20 допускает прямой Identity в tools); аудит не пишется (консистентно с сидером). — Done (2026-06-07)
- `MLC-054` — Полировка `/reports` (UX, постановка куратора; номер сдвинут с занятого `MLC-053`). Три части: **(1)** видимая обрезка периода — контракт `LicenseUsageSeriesResponse` +`Clamped`/`MaxSpanDays` (`ResolveRange`→`(From,To,Clamped)`, `Clamped` ровно в ветке `>MaxSpan`; дефолтное окно 7д не триггерит; пустой ряд тоже несёт флаги), FE-плашка под фильтром при `summary.data?.clamped` + i18n `clampNotice`; **(2)** HTML/PDF без сырой побакетной таблицы (остаются сводка+график) — `toHtml.ts`/`toPdf.ts`, зависимость `jspdf-autotable` удалена (package.json + regex чанка `export-pdf` в vite.config), сырая таблица только в CSV/XLSX; **(3)** помесячный выбор «Месяц ‹ ›» — чистые хелперы `monthToRange`/`shiftMonth` (`reportsUrlState.ts`) + контрол в `ReportsFiltersBar.tsx`, заполняет те же `from`/`to` (целый месяц <31д → кламп не триггерит). BE 397 / FE 177 зелёные; type-check/lint/build чистые, `export-pdf` 399.6 кБ (<500). Проверено в браузере: плашка на >31д, помесячный клик без плашки, HTML/PDF без таблицы (живые данные 2880 бакетов). Канон: `03_DOMAIN_MODEL.md`, `05_UI_REQUIREMENTS.md` §3.6. ADR не трогали (ADR-25). — Done (2026-06-07)
- `MLC-055` — Переработка `/settings` (трек «Полировка /settings», 1/2; frontend+доки, без бэкенда). Четыре сведённые правки одной страницы: **(a)** секции разнесены — смешанная «cluster» → «Подключение к 1С / RAS» (креды + порт + платформа) и отдельная «Учёт лицензий» (`OneC.LicenseConsumingAppIds`); `audit`→объединённая «Хранение данных»; из «Значений по умолчанию» убрана версия платформы (ушла в пикер); **(b)** выведена скрытая `LicenseUsage.RetentionDays` (была в каталоге+доке, не в UI) рядом с `Audit.RetentionDays`; **(c)** RAS endpoint→поле «Порт» (`RasPortField`, 1024–65535, дефолт 1545; хранение wire-формата `localhost:<порт>`, бэкенд не тронут); **(d)** единый пикер «Платформа 1С» (`PlatformPicker` вместо `RacPathDetect`) — выбор установленной платформы из `useRacPaths` пишет **оба** раздельных ключа одним действием (`OneC.RAS.ExePath` + `OneC.DefaultPlatformVersion`, версия парсится из пути `…\1cv8\<version>\bin\rac.exe` чистым хелпером `parsePlatformVersionFromRacPath`, regex 4 сегмента без фиксации длин — 8.5 одноцифровой build), свёрнутый escape-hatch (`SettingField` для пути + `DiscoveryField`/`usePlatformVersions` для версии) разводит ключи врозь. Переиспользованы `DiscoveryField`/`useDiscovery`/`SettingField`/`useSettings`; пер-контрольное сохранение сохранено (без общей «Применить»). Чистые хелперы `parsing.ts` (версия + порт parse/build) + unit-тесты; новый render-тест на пикер/порт. FE 192 зелёные; type-check/lint чистые. **Live-preview прогнан против запущенного стека (2026-06-07):** реальный discovery нашёл `…\1cv8\8.5.1.1302\bin\rac.exe`, версия распарсилась `8.5.1.1302` (одноцифровой build), оба ключа в строке состояния; порт `1545` из `localhost:1545`; escape-hatch показал путь + версию `8.5.1.1302 — x64` (`usePlatformVersions`); `LicenseUsage.RetentionDays` виден в «Хранение данных»; секции разнесены; консоль чистая, `settings`/`discovery/rac-paths` `200`. Канон present-tense: ADR-3.3 (UI-подача rac.exe/порта, wire-формат не тронут), `04_INFRASTRUCTURE.md`, `05_UI_REQUIREMENTS.md`. ADR-20/16/single-node/RU-only не затронуты. — Done (2026-06-07)
- `MLC-056` — SQL-instance discovery (localhost) + пикер сервера БД (трек «Полировка /settings», 2/2;
  backend+frontend+доки). **Backend:** новый Application-порт `ISqlInstanceDiscovery` (синхронный, без
  аргументов) + адаптер `Infrastructure/Discovery/SqlInstanceDiscovery.cs` — читает оба registry-view
  (`Registry64`+`Registry32`=WOW6432Node) ключа `HKLM\…\Microsoft SQL Server\Instance Names\SQL`,
  value-names = имена инстансов; чистый статический `Map` (`MSSQLSERVER`→`localhost`, именованный→
  `localhost\<name>`, дедуп `SortedSet`/OrdinalIgnoreCase + сортировка) отделён от I/O реестра, как
  `RacPathDiscovery.Scan`. Без SQL Browser/UDP — только localhost. DI **Singleton** рядом с
  `RacPathDiscovery`/`PlatformVersionDiscovery` (машинно-локальный снимок, не per-request как
  `SqlDatabaseDiscovery`). Эндпоинт `GET /discovery/sql-instances` (без query, как `GetIisSitesAsync`):
  try/catch + санитайз (MLC-009) — реестр недоступен/нет прав → `Available:false` + русский текст,
  сырое исключение в лог (`LogSqlInstancesDiscoveryFailed`). **Платформенная совместимость:** атрибут
  `[SupportedOSPlatform("windows")]` — на приватном `ReadInstanceNames()`, обёртка `FindLocalInstances()`
  под guard'ом `OperatingSystem.IsWindows()`, чистый `Map` без атрибута → unit-тест Map'а собирается
  кросс-платформенно под `TreatWarningsAsErrors` (класс-атрибут дал бы CA1416 на тесте). Пакет
  `Microsoft.Win32.Registry` отдельно **не понадобился** (типы доступны транзитивно через
  `Microsoft.Data.SqlClient`; сборка чистая, без CA1416). **Frontend:** `useSqlInstances(enabled)` по
  образцу `useRacPaths`; новый `DatabaseServerField.tsx` (`/settings`, `Defaults.DatabaseServer`) —
  зеркало `VersionEscapeField` (локальный draft + явный Save + ресинк при внешней мутации; НЕ
  save-on-keystroke — гоча 055, страница пер-контрольная), спец-рендер в `SettingsPage` рядом с
  `RasPortField`/`PlatformPicker`; в форме инфобазы плоский `Input` поля `databaseServer` заменён на
  `DiscoveryField` напрямую (form-state, `field.onChange`), `sqlInstance*` прокинуты через
  `useInfobaseForm`→`InfobaseFormDialog`→`PublicationFieldset` — выбор инстанса кормит существующий
  пикер баз (`useDatabases(watchedDatabaseServer)`). Новых i18n-ключей не нужно (переиспользованы
  `discovery.*`, `settings.labels/hints["Defaults.DatabaseServer"]`, `settings.actions/toasts.*`).
  Тесты: `SqlInstanceDiscoveryTests` (7× чистый Map) + 2 endpoint-теста (throw→`Available:false`
  санитизировано / happy-path). BE 406 / FE 192 зелёные; type-check/lint чистые; NetArchTest-границы
  целы (порт в Application, адаптер в Infrastructure, Web→только интерфейс). **Live-preview прогнан
  против запущенного стека (2026-06-07, дефолтный инстанс `MSSQLSERVER`):** `GET /discovery/sql-instances`
  → `{items:["localhost"],available:true}` (`MSSQLSERVER`→`localhost`); на `/settings` поле сервера БД —
  пикер с опцией `localhost` + «Обновить»/«Сохранить»/«Ввести вручную»; в форме инфобазы «СУБД»→«Сервер
  СУБД» теперь пикер (`localhost`), цепочка `databaseServer`→`useDatabases` подгрузила реальные базы
  (`bd1`/`MitLicenseCenter`/`mitpro`/`test`); консоль чистая, все discovery-вызовы `200`. Канон present-tense:
  `04_INFRASTRUCTURE.md` (discovery error-contract +`/sql-instances`), `05_UI_REQUIREMENTS.md` §3.x
  (пикер сервера БД). ADR не вводился (localhost-реестр за интерфейсом в Infrastructure = штатная
  adapter-граница ADR-20). ADR-16/3.3/single-node/RU-only не затронуты. **Трек «Полировка /settings»
  завершён 2/2.** — Done (2026-06-07)
- `MLC-057` — Переключатель темы (light/dark/system) (трек «Полировка панели v1.1», 1/2; frontend+доки).
  Приложение обёрнуто в `ThemeProvider` из `next-themes` (`App.tsx`: `attribute="class"`,
  `defaultTheme="system"`, `enableSystem`, `storageKey="mlc-theme"`); новый `components/layout/ThemeToggle.tsx`
  (dropdown light/dark/system на shadcn `DropdownMenuRadioGroup`, иконки `SunIcon`/`MoonIcon`/`MonitorIcon`,
  `useTheme().setTheme`) в `Topbar` слева от меню пользователя; i18n `theme.*` (label/light/dark/system, ru).
  Заодно `App` переключён на тематизированный `@/components/ui/sonner` (раньше импортировал сырой `sonner`,
  и `useTheme` в нём не работал без провайдера) — тосты теперь следуют выбранной теме. Переиспользовано:
  `.dark`-переменные + `@custom-variant dark` (`index.css`, готовы), `next-themes` (уже в `package.json`),
  shadcn `dropdown-menu`/`button`. 3 новых теста (`ThemeToggle.test.tsx`: рендер + смена темы вешает/снимает
  класс `.dark` на `documentElement` + пишет `localStorage`). FE 195 зелёные; type-check/lint чистые.
  **Live-preview прогнан против запущенного стека (2026-06-08):** `system`→`dark` по `prefers-color-scheme`
  (OS dark, `mlc-theme` пуст), затем выбор «Светлая» → класс `light` + `mlc-theme=light` + **переживает
  перезагрузку**, выбор «Тёмная» → класс `dark`; светлый/тёмный скрин сняты, консоль чистая. Канон
  present-tense: `06_UI_DESIGN.md` §3 (тумблер/ThemeProvider описаны как построенные), `ROADMAP.md` (пункт
  снят из «заспечено, но не построено»). ADR не трогали (UI-фича в рамках существующего дизайн-канона);
  single-node/RU-only не затронуты. — Done (2026-06-08)
- `MLC-058` — Раздел «Администраторы» (Backend API + Frontend UI; трек «Полировка панели v1.1», 2/2 —
  **трек завершён**). Управление учётками панели из UI вместо консольной `reset-admin`. **Backend:** новый
  `Web/Endpoints/Admins/{AdminsEndpoints,AdminsContracts}.cs` (`MapGroup("/api/v1/admins")`, регистрация в
  `Program.cs`): `GET /admins` (`Viewer`; логин/роли/`isActive`=не-залочен), `POST /admins` (`Admin`; логин +
  роль `Admin`/`Viewer` радио, валидируется против `Roles.All`), `POST /{id}/reset-password|disable|enable`
  (`Admin`). Всё через `UserManager<AppUser>` (не голый DbContext). «Отключение» = Identity-lockout
  (`LockoutEnd=MaxValue`, реально режет вход в `PasswordSignInAsync`), «включение» = снятие + сброс счётчика.
  Два guard'а (до мутации, 409): «сам себя» (`ADMIN_CANNOT_DISABLE_SELF`) и «последний активный **Admin**»
  (`ADMIN_LAST_ACTIVE`, считаются только учётки роли `Admin` — лишний `Viewer` не спасает); дубликат логина →
  `409 ADMIN_USERNAME_DUPLICATE`, не найден → `404 ADMIN_NOT_FOUND`. +4 слота аудита **103–106**
  (`AdminCreated`/`AdminDisabled`/`AdminPasswordReset`/`AdminEnabled`, enum заморожен, `tenantId: null`) +
  фабрики `AuditDescriptions`. **Развилка генерации пароля решена вариантом «а» (ADR-20-чисто):** новый
  Application-порт `IInitialPasswordGenerator` + Infrastructure-реализация поверх `IdentitySeeder.
  GenerateInitialPassword()` (единый источник, парити с парольной политикой); Web инжектит интерфейс.
  Пароль возвращается в ответе и показывается в UI один раз — **в аудит/логи не пишется** (улучшение против
  формулировки §3.7). Схема Identity не менялась → **миграции нет**. **Frontend:** фича `features/admins/`
  (`types`/`useAdmins`/`AdminsPage`/`AdminFormDialog` с радио ролей/`GeneratedPasswordDialog`+copy/`Reset`/
  `Disable`/`EnableAdminDialog`) по образцу `features/tenants/`; admin-only роут `/admins` (`ProtectedRoute
  requireAdmin`), пункт сайдбара «Администраторы» (`ShieldIcon`, группа «Система»); мутации на
  `useInvalidatingMutation` (`["admins"]`); guard-отказы 409 → понятный тост (`matchConflictCode`); i18n
  `nav.admins` + `admins.*` + `common.copy/copied` + 4 ключа `audit.actions.*`. **Сознательные as-built-обрезки
  (каждая = колонка `AppUser` = миграция, которой задача избегает):** нет «последнего входа» и форс-смены
  пароля; смена роли существующей учётки тоже вне объёма (роль — только при создании). Тесты: backend
  `AdminsEndpointsTests` (10 кейсов над реальным `UserManager`/EF-InMemory: создание/дубликат/сброс/disable+
  enable/оба guard'а/404/список + аудит-без-пароля) — BE **416** зелёные, NetArchTest-границы целы; frontend
  `AdminFormDialog`/`DisableAdminDialog` тесты — FE **201** зелёные, type-check/lint чистые. **Live-preview
  против запущенного стека (2026-06-08):** создан админ → показан временный пароль (copy работает) →
  логин новым админом проходит; сброс → старый пароль `401`, новый `200`; отключение → вход `401`; включение
  → вход `200`; guard «сам себя» и «последний активный админ» (через валидную cookie уже-отключённого
  админа) → `409` с понятным текстом; аудит содержит 4 действия **без пароля**; UI-скрины (список, форма с
  радио, диалог пароля) сняты, консоль чистая. Канон present-tense: `05_UI_REQUIREMENTS.md` §3.7 (снято «не в
  v1», описан построенный раздел + as-built-границы), `03_DOMAIN_MODEL.md` (слоты 103–106), `06_UI_DESIGN.md`
  (sidebar), `ROADMAP.md` (пункт снят). ADR-20 не затронут (Identity напрямую в Web допустим; генератор — через
  Application-порт). — Done (2026-06-08)
- `MLC-059` — Форс-смена пароля при первом входе + время последнего входа (трек «Полировка панели v1.1», 3/3 —
  **трек завершён**). Доводит две фичи дизайна §3.7, обрезанные в MLC-058 (обе требуют колонку на `AppUser`).
  **Домен+миграция:** `AppUser` (был пустой `IdentityUser<Guid>`) +2 свойства — `MustChangePassword` (bool,
  default false) и `LastLoginAt` (`DateTime?`, UTC); одна миграция `MLC059ForcePasswordChangeAndLastLogin`
  (2 колонки в `auth.Users`), сгенерированные файлы нормализованы (UTF-8 без BOM + LF — гоча CLAUDE.md).
  **Backend (`Web/Endpoints/Auth/`):** `LoginAsync` пишет `LastLoginAt = clock.GetUtcNow()` (`TimeProvider`)
  и возвращает `mustChangePassword` в `CurrentUserResponse`; тот же флаг в `MeAsync`; `ChangePasswordAsync`
  снимает флаг по успеху (рядом с `RefreshSignInAsync`) — форс-смена идёт через этот же эндпоинт, новых
  enum-слотов не нужно (аудит `AdminPasswordChanged=102` 1:1). `AdminsEndpoints` create/reset ставят
  `MustChangePassword=true`; `AdminResponse` +`LastLoginAt` (опускается при null — `WhenWritingNull`). Гейт —
  только фронтовый (single-node admin-only). **Frontend:** `currentUserSchema` +`mustChangePassword`;
  `ProtectedRoute` при флаге рендерит новый блокирующий экран `ForcePasswordChange` (замещает весь `AppShell`,
  разрешён только выход) — после успешной смены инвалидирует `/me`, гейт снимается; форма смены вынесена из
  `ProfilePage` в переиспользуемый `features/profile/ChangePasswordForm.tsx` (профиль 1:1); колонка «Последний
  вход» в `AdminsPage` (`date-fns`/`ru`, `—` при null, схема через `omittable`); i18n `auth.forceChange.*` +
  `admins.fields.lastLogin`. Тесты: backend новый `AuthEndpointsTests` (login пишет `LastLoginAt`+флаг, me
  отдаёт флаг, change-password снимает — реальные `UserManager`+`SignInManager`+cookie-схема над EF-InMemory) +
  `AdminsEndpointsTests` (create/reset ставят флаг, list отдаёт `LastLoginAt`) — BE **420** зелёные,
  NetArchTest-границы целы; frontend — гейт-кейс в `ProtectedRoute.test`, фикстуры обновлены — FE **202**
  зелёные, type-check/lint чистые, `build.ps1` зелёный. **Live-preview против запущенного стека (2026-06-08):**
  создан `operator1` → вход им → блокирующий экран форс-смены (сайдбар скрыт), переход на `/tenants` всё равно
  держит на экране → смена пароля → пускает в приложение; сброс пароля → повторный вход снова даёт форс-смену;
  «Последний вход» заполняется после входа (`08.06.2026 …`), у не входивших `—`; кнопка «Выйти» с экрана
  возвращает на `/login`; скрины (экран форс-смены, список с колонкой) сняты, консоль чистая. Канон
  present-tense: `05_UI_REQUIREMENTS.md` §3.7 (last-login + форс-смена описаны как построенные, снято «out of
  scope»), `03_DOMAIN_MODEL.md` §6 (поля `MustChangePassword`/`LastLoginAt`). ADR-20 не затронут. — Done
  (2026-06-08)
- `MLC-060` — Полное переименование раздела «Администраторы»→«Пользователи» (мини-трек «Раздел Пользователи»,
  1/2; чистый рефакторинг, поведение строго 1:1 с MLC-058/059). Раздел управляет и Admin-, и Viewer-учётками,
  поэтому имя «Администраторы» неточно. Где «admins/Admin(s)» означало **раздел** → «users/User(s)»; роли
  `Roles.Admin/Viewer` (доступ), ops-утилита `reset-admin` и логин-слоты аудита `AdminLoggedIn=100`/
  `AdminLoggedOut=101`/`AdminPasswordChanged=102` (про вход, не про раздел) **не тронуты**. **Backend:**
  `git mv` `Web/Endpoints/Admins/`→`Users/`, `AdminsEndpoints`→`UsersEndpoints` (`MapAdminsEndpoints`→
  `MapUsersEndpoints`, `MapGroup("/api/v1/admins")`→`/users`, `WithTags`), контракты `Admin*Response/
  CreateAdminRequest`→`User*`; `Problems`/`ProblemCodes` `Admin*`→`User*` **со строками кодов**
  `ADMIN_*`→`USER_*` (`USER_USERNAME_DUPLICATE`/`USER_NOT_FOUND`/`USER_CANNOT_DISABLE_SELF`/`USER_LAST_ACTIVE` —
  контракт API, parity BE↔FE через `matchConflictCode`); имена слотов аудита `AuditActionType.AdminCreated/
  Disabled/PasswordReset/Enabled`→`User*` (**int 103–106 заморожены — переименовано только имя C#**),
  `AuditDescriptions.*` методы (русский текст «Учётная запись …» role-neutral сохранён; «администратором
  {initiator}» = роль исполнителя, не раздел). `Program.cs`-регистрация. **Frontend:** `git mv`
  `features/admins/`→`features/users/`, `AdminsPage`→`UsersPage`, `useAdmins`→`useUsers` (`adminsQueryKey
  ["admins"]`→`usersQueryKey ["users"]`, все хуки `*Admin`→`*User`), `AdminFormDialog`→`UserFormDialog`,
  `Disable/EnableAdminDialog`→`*UserDialog` (проп `admin`→`user`), `types` (`ADMIN_ROLES`/`Admin*`→`USER_ROLES`/
  `User*`); роут `/admins`→`/users` (`requireAdmin` сохранён); сайдбар «Пользователи» + иконка `ShieldIcon`→
  `UsersRound`, `to="/users"`; i18n `nav.admins`→`nav.users`, блок `admins.*`→`users.*` (подписи раздела
  «Администраторы»→«Пользователи»; ярлыки ролей «Администратор»/«Наблюдатель» и guard-текст про «последнего
  активного администратора» = роль, сохранены); `matchConflictCode`-коды под backend. Тесты переименованы
  (`UsersEndpointsTests`, `UserFormDialog`/`DisableUserDialog`), ссылка в `AuthEndpointsTests`. Миграции нет
  (rename только символов/строк, БД-контракт enum по числам цел). BE **420** / FE **202** зелёные,
  NetArchTest-границы целы, type-check/lint чистые, `build.ps1` зелёный. **Live-preview против запущенного
  стека (2026-06-08):** раздел открывается на `/users` (сайдбар «Пользователи», иконка людей), создание/сброс/
  disable/enable работают 1:1, аудит пишет «Учётная запись … администратором …», коды конфликтов `USER_*`
  матчатся фронтом. Канон present-tense: `05_UI_REQUIREMENTS.md` §3.7 (Administrators→Users/«Пользователи»),
  `06_UI_DESIGN.md` (sidebar), `03_DOMAIN_MODEL.md` (имена enum 103–106). ADR не затронуты. — Done (2026-06-08)
- `MLC-061` — Смена роли существующей учётки Admin↔Viewer (мини-трек «Раздел Пользователи», 2/2; фича поверх
  переименованного `/users`). **Завершает мини-трек 2/2.** **Backend:** новый эндпоинт `POST
  /api/v1/users/{id}/role` (`RequireAuthorization(Roles.Admin)`, тело `ChangeUserRoleRequest { Role }`),
  валидация `role ∈ Roles.All` → `ValidationProblem`; применение через `RemoveFromRolesAsync(текущие)` +
  `AddToRoleAsync(новая)`; учётка уже ровно в целевой роли → идемпотентно `200` без аудита. Два guard'а (409,
  до мутации): «сам себе» (`USER_CANNOT_CHANGE_OWN_ROLE` — само-разжалование = потеря доступа) и «последний
  активный Admin» при разжаловании Admin→не-Admin (переиспользует общий `USER_LAST_ACTIVE` + извлечённый хелпер
  `HasOtherActiveAdminAsync`, единый с `DisableAsync`). +1 слот аудита `AuditActionType.UserRoleChanged = 107`
  (enum заморожен, новое число — 103–106 заняты MLC-058); `AuditDescriptions.UserRoleChanged(user, oldRole,
  newRole, initiator)`, `tenantId: null`. Сообщение `Problems.UserLastActiveAdmin()` обобщено под отключение и
  разжалование (фронт подбирает точную формулировку по коду). Миграции нет. **Frontend:** хук `useChangeUserRole`
  (`useInvalidatingMutation`, инвалидация `["users"]`); новый `ChangeRoleDialog` (radio Admin/Viewer с дефолтом
  текущей роли, подсказка «вступит в силу при следующем входе», guard-отказы → тост через `matchConflictCode`);
  пункт «Сменить роль» (`UserCogIcon`) в дропдауне `UsersPage`; i18n `users.actions.changeRole` +
  `users.changeRole.*` + `users.toasts.roleChanged` + `users.errors.cannotChangeOwnRole`/`lastActiveAdminDemote`.
  Тесты: backend +6 (happy промоут, идемпотентность без аудита, self-guard, demote-last-admin guard, invalid
  role, unknown id) → BE **426** зелёные; frontend +3 (`ChangeRoleDialog`: промоут+инвалидация, оба guard-тоста)
  → FE **205** зелёные; NetArchTest целы, type-check/lint/`build.ps1` зелёные. **Live-preview против
  запущенного стека (2026-06-08):** дропдаун показывает «Сменить роль», диалог рендерит radio+подсказку; через
  реальные роуты — промоут `mitpro` Viewer→Admin (`200`, список обновился), разжалование Admin→Viewer (`200`),
  повторная установка той же роли (`200` без второй записи аудита), смена роли себе (`409
  USER_CANNOT_CHANGE_OWN_ROLE`); аудит содержит `UserRoleChanged` «Роль учётной записи «mitpro» изменена с
  Viewer на Admin / с Admin на Viewer администратором admin». Канон present-tense: `05_UI_REQUIREMENTS.md` §3.7
  (смена роли как построенная), `03_DOMAIN_MODEL.md` (слот 107). ADR не затронуты. — Done (2026-06-08)
- `MLC-063` — Разведка источников perf-метрик 1С/хоста/SQL (трек «Анализ быстродействия 1С», Фаза 0, research) — Done (2026-06-08). На живом **нагруженном** стенде 8.5.1.1302 снят реальный вывод `rac session list` / `rac process list` + проверен доступ к MSSQL DMV. **Риск №1 ИНВЕРТИРОВАН:** `session list` отдаёт полный набор perf-полей (`cpu-time-*`/`memory-*`/`duration-current`/`-dbms`/`blocked-by-*`/`calls-*`/`last-active-at`) уже в покое (пустая фикстура PR 3.8 — артефакт idle-сеанса, не ограничение версии); под нагрузкой `*-current` оживают, сеанс привязывается к rphost. `process list` — `available-perfomance`/`avg-call-time`/`memory-size`/`pid` на месте. DMV-доступ есть (панель=`Trusted_Connection`; prod нужен `GRANT VIEW SERVER STATE TO [login]`, не на группу — UAC-фильтрация), атрибуция SQL **по базе** работает (`program_name='1CV83 Server'`→`DB_NAME`→`Infobase`→клиент), **SQL→сеанс→юзер невозможна** (подтверждено — отклонение опции обосновано). Гочи для `MLC-066`: `memory-current` бывает отрицательным; `avg-*` дробные (инвариантный парс); `process list` = +1 спавн `rac.exe`/poll (ADR-3.3). Урок: тяжёлый отчёт был CPU-bound в rphost (не DBMS-bound) → UI атрибутирует и app-сервер (rac), и SQL (DMV) равно уверенно. Прод-код не менялся (research). Полный отчёт — секция «Результаты разведки (MLC-063)» в `.claude/plans/spicy-discovering-torvalds.md`.
- `MLC-064` — Backend: адаптер host-метрик (трек «Анализ быстродействия 1С», Фаза 1) — Done (2026-06-08). Application-порт `IHostMetricsProbe` + нейтральные DTO (`HostMetricsSnapshot`/`CpuMetrics`/`MemoryMetrics`/`DiskMetrics`/`ProcessGroupUsage`) + чистые `ProcessFamilyMap`/`ProcessFamilyGrouping` (атрибуция по семьям, тестируются без WMI). Infrastructure-адаптер `OneCHostMetricsProbe` — **на WMI, не `PerformanceCounter`** (имена perf-категорий/счётчиков локализованы на RU Windows → English-lookup падает; WQL-свойства инвариантны — подтверждено CIM-проверкой на стенде): CPU%/queue/RAM/pages из `Win32_PerfFormattedData_*`, латентность диска из `Win32_PerfRawData_PerfDisk_PhysicalDisk` ручным cook `PERF_AVERAGE_TIMER` (формат-класс режет дробь до целых секунд), total RAM из `Win32_ComputerSystem`, CPU%/RAM процессов из `System.Diagnostics.Process`. Паттерн `OneCIisPublishingService`: `[SupportedOSPlatform("windows")]` + `#pragma CA1416` в DI + `StubHostMetricsProbe`. Singleton держит предыдущий снимок (CPU-времена процессов + сырые perf-счётчики диска) для **дельты CPU%** между poll; первый poll → `Measuring=true`. Маппинг процесс→семья настраиваемый: `Settings.Performance.ProcessFamilyMap` (образец `OneC.LicenseConsumingAppIds`). Endpoint `GET /api/v1/performance/host` (Viewer, vertical slice ADR-20). Заведён **ADR-26** (Live pull/без персиста + Recording on-demand/БД/авто-стоп; адаптеры host/SQL по ADR-20; perf-события — своя таблица, не AuditLog; «чёрный ящик» и SQL→сеанс→юзер отклонены/gated). Канон present-tense: `DECISIONS.md` ADR-26 + `04_INFRASTRUCTURE.md` §6 + строка каталога настроек. Тесты: чистая группировка (без WMI) + стаб + endpoint; guard-тест границ усилён (`Infrastructure.Performance` в запрете для Web). `dotnet build`/`format`/443 теста зелёные. **Live-проверено на стенде:** два poll'а `GET /performance/host` под Viewer — poll 1 `Measuring=true` (CPU/RAM из WMI валидны, дельта-метрики 0), poll 2 `Measuring=false` с живыми CPU%-дельтами по семьям и cooked-латентностью диска (read≈0.4мс/write≈0.08мс); 1С-процессы (rphost/ragent/rmngr/ras) опознаны в семью OneC, sqlservr → Mssql.
- `MLC-065` — Frontend: каркас раздела «Быстродействие» + стартовый live-экран (трек «Анализ быстродействия 1С», Фаза 1 — **завершает её**) — Done (2026-06-08). Новая фича `features/performance/` по образцу `features/sessions` (realtime polling) и `features/reports` (recharts). **Граница API:** `types.ts` — Zod-схема `hostMetricsSnapshotSchema` (критичная граница ADR-10.1/MLC-016 — live-экран питает вердикт; `family` валидируется как свободная строка, незнакомый ключ настраиваемого маппинга не роняет снимок). **Хук:** `useHostMetrics` (`refetchInterval: 5_000`, `placeholderData: prev` — паттерн `useSessionsSnapshot`, схема в `api()`); оркестрация `usePerformancePage` (сатурации/доли/вердикт через `useMemo`). **Чистые хелперы (тестируемы без React):** `thresholds.ts` — сатурация OK/Warn/Crit по сигналам насыщения, «хуже из двух» (CPU = очередь+%, RAM = paging+занятость, Disk = латентность read/write+очередь, пороги 75/90%, queue 2/4, 10/20мс), не голый %; `attribution.ts` — `toFamilyShares` (доли + сорт по порядку семей, незнакомые в конец, без div/0), `dominantFamily`, `computeVerdict` (узкое место + главный потребитель; `measuring`→вердикт не выносится). **Компоненты:** `MetricGauge` (гейдж на `Progress`, радиального нет; тинт индикатора по сатурации через arbitrary-variant; `measuring`→«измеряю…», не ноль) — переиспользуемый «по месту»; `SaturationGauges` (CPU/Disk зависят от дельты→measuring, RAM мгновенный→всегда); `ProcessFamilyAttribution` (горизонтальный stacked-bar recharts expand + плотная таблица CPU%/RAM ГБ/процессы; `familyColors.ts` — цвет+i18n-подпись по ключу); `VerdictBanner` (цвет/иконка по уровню, связывает ресурс+семью); `PerformancePage` (контейнер: header+свежесть `RelativeTime` + error-баннер + секции, паттерн `SessionsPage`). Роут lazy `/performance` (`ProtectedRoute`, **не** admin — Viewer) в `router.tsx`; пункт сайдбара «Быстродействие» (`ActivityIcon`, группа «Операции») в `Sidebar.tsx`; i18n `nav.performance` + секция `performance.*` (RU, семьи `OneC`/`Mssql`/`OsUpdate`/`Antivirus`/`Other`→подписи). Гейдж обобщается в Фазе 5 (не блокирует). Канон present-tense: `05_UI_REQUIREMENTS.md` §3.8 + `06_UI_DESIGN.md` §3 (правило гейджей сатурации) / §5 (sidebar). Тесты по образцу `features/sessions`/`features/reports`: `thresholds.test` + `attribution.test` (чистые), `useHostMetrics.test` (endpoint+схема), `MetricGauge.test` (measuring). `pnpm type-check`/`lint`/`test` (232 теста) зелёные. **Live-проверено end-to-end** на стенде: логин Viewer/Admin → 2 poll'а `/performance/host` (JSON совпал с Zod-схемой точно, включая `9.99E-05` и `measuring`-переход) → preview `/performance`: пункт меню, вердикт, светофор гейджей (emerald-OK), stacked-bar+таблица, polling живой, консоль чистая. **Атрибуция по семьям подтверждена корректной** (frontend и backend-классификация): дефолт `Performance.ProcessFamilyMap` (`OneC=rphost,ragent,rmngr,ras;Mssql=sqlservr;…`) применяется и при `<NULL>` в БД (`Parse(null)`→`Default`); точная реплика пробы в не-elevated контексте на этой машине даёт `OneC:4 / Mssql:1 / Antivirus:2 / Other:353` (0 пропущено) — совпадает с live-тестом MLC-064 под `dev.ps1`. **Гоча верификации (зафиксировано):** backend, поднятый через preview-инструмент, работает в более изолированном контексте и видит меньше процессов (200 vs 360), молча пропуская сервисные процессы 1С/SQL (службы) → в preview атрибуция выглядела как один `Other`; под штатным backend (`dev.ps1` elevated / prod service-account в Administrators) семьи `OneC`/`Mssql` заполняются. **Out-of-scope куратору (MLC-064, не взято):** проба молча пропускает нечитаемые процессы (`ReadProcesses` глотает Win32Exception) → при недостатке привилегий раздел показывает правдоподобно-неверную картину (всё `Other`) без сигнала; кандидат на честный индикатор «N процессов недоступно / нужны права» по образцу IIS-permissions.
- `MLC-064a` — Доработка host-пробы: честный сигнал недоступных процессов (трек «Анализ быстродействия 1С», доработка Фазы 1, найдена на `MLC-065`) — Done (2026-06-08). Закрывает дефект честности из отчёта `MLC-065`: `OneCHostMetricsProbe.ReadProcesses()` молча глотал `Win32Exception` → под недостаточно привилегированным backend'ом раздел показывал правдоподобное «всё Прочее» без сигнала. **Backend:** `ReadProcesses` теперь различает три случая — успешное чтение; `InvalidOperationException` (процесс вышел между перечислением и чтением — гонка, не права, не считается); `Win32Exception` (access denied — **считается** в новый счётчик). Псевдопроцессы Idle (PID 0) / System (PID 4) исключены из счётчика (нечитаемы при любых правах, ни одной семье не принадлежат → сигнал не кричит вхолостую под админом). Новое поле `HostMetricsSnapshot.ProcessesInaccessible` + производный `AttributionIncomplete` (`> 0`); `StubHostMetricsProbe` и endpoint-тесты обновлены. **Frontend:** `AttributionWarningBanner` (амбер, `ShieldAlertIcon`, образец IIS-permissions) на `/performance` под `VerdictBanner`, рисуется по `attributionIncomplete`; Zod-схема `hostMetricsSnapshotSchema` расширена двумя полями; i18n `performance.attributionWarning` (`{{count}}`, безопасная для грамматики форма «Недоступно процессов: N»). **Чистые `ProcessFamilyMap`/`ProcessFamilyGrouping` не тронуты** (подтверждены корректными на MLC-065). Канон present-tense: `DECISIONS.md` ADR-26 (клауза честного сигнала) + `04_INFRASTRUCTURE.md` §6 (правило подсчёта, исключение Idle/System, гонка-vs-права) + `05_UI_REQUIREMENTS.md` §3.8 (баннер) + `OPERATIONS.md` новая секция «Быстродействие — required permissions» (симптом, dev `-NoElevate`, prod = те же права что IIS-метабаза, Idle/System=0 под админом). Тесты: BE подсчёт/производный флаг на стабе + endpoint pass-through (445 тестов); FE баннер (233 теста). `dotnet test`/`pnpm test`/`type-check`/`lint` зелёные. Дефект проявлялся только под не-elevated backend; в штатном проде (под админом для IIS) баннер скрыт.
- `MLC-066` — Backend: 1С-сеансы/процессы «кто грузит» (трек «Анализ быстродействия 1С», Фаза 2, ядро трека) — Done (2026-06-08). Состав подтверждён разведкой `MLC-063` (perf-поля реальны на 8.5.1.1302). **Application:** нейтральные DTO `OneCSessionLoad` (perf-срез сеанса, отдельно от kill-path `ClusterSession` — тот остаётся тонким) + `OneCProcessLoad` (рабочий процесс) в `Clusters/ClusterModels.cs`; response `OneCLoadSnapshot` в `Performance/`. Интерфейс `IClusterClient` расширен `ListSessionLoadsAsync` (тот же спавн `session list`, богаче маппинг) + **новая команда** `ListProcessesAsync` (`rac process list`). **Infrastructure:** маппинг `RacExecutableRasClusterClient.ParseSessionLoads`/`ParseProcessLoads` (internal static, как `ParseInfobases`) — все perf-поля **nullable**, парсер «never throws» сохранён (отсутствующие поля → null, не drop). Поля сеанса: `cpu-time-current`, `duration-current`/`-dbms`, `blocked-by-dbms`/`-ls`, `memory-current`, `process`/`connection` (нулевой UUID `0000…`→null), `last-active-at`, `session-id`. Процесса: `pid`, `available-perfomance` (опечатка rac сохранена), `avg-call-time`, `memory-size`. **Гочи разведки реализованы:** `memory-current` знаковый `long` (бывает отрицательным при GC); числа парсятся `CultureInfo.InvariantCulture` (`avg-call-time` в научной нотации `9.99E-05` → `NumberStyles.Float`). UUID-кэш `IClusterUuidCache` переиспользован (без лишнего `cluster list`); ошибка команды → пустой список + инвалидация (паттерн `ListActiveSessionsAsync`). **Web:** endpoint `GET /api/v1/performance/onec-sessions` (Viewer, vertical slice ADR-20 — Web зовёт только `IClusterClient`, к rac.exe не ходит), компонует снимок из двух команд + стампит `CapturedAtUtc`. **Спавн-бюджет:** +1 спавн/poll (process list сверх session list) = 2/poll на тёплом кэше, **только pull-по-требованию** (вкладка открыта), не фон — отмечено в ADR-3.3. Тесты: фикстуры из сырых срезов разведки (нагруженный `session list` + `process list`) — отрицательная память / дробные / научная нотация / отсутствующие/нулевые поля; адаптер-тесты (спавн команды + переиспользование кэша + инвалидация на ошибке); endpoint через `Substitute.For<IClusterClient>()`; две рукописные фейк-реализации `IClusterClient` дополнены. `dotnet build`/`format`/462 теста зелёные. Канон present-tense: `DECISIONS.md` ADR-3.3 (новая команда + perf-маппинг + спавн-каденция) + ADR-26 (endpoint Фазы 2) + `04_INFRASTRUCTURE.md` §6.1 + `05_UI_REQUIREMENTS.md` §3.8. UI-поверхность (вкладка «кто грузит») — следующая задача `MLC-067`. ADR-16/20/single-node/RU-only не затронуты.
- `MLC-067` — Frontend: экран «кто грузит внутри 1С» (трек «Анализ быстродействия 1С», Фаза 2 — **завершает её**) — Done (2026-06-08). Drill-down-секция на готовом `GET /api/v1/performance/onec-sessions` (MLC-066), рендерится **ниже** host-снимка на том же `/performance` (отдельный live-источник от host-пробы: собственный запрос, своя загрузка/ошибка). **Граница API:** `types.ts` дополнен Zod-схемами `oneCSessionLoadSchema`/`oneCProcessLoadSchema`/`oneCLoadSnapshotSchema` (критичная граница ADR-10.1/MLC-016 — perf-поля питают подсветку; зеркалят nullable-DTO бэкенда, все perf-поля `.nullable()`). **Хук:** `useOneCLoad` (`refetchInterval: 5_000`, `placeholderData: prev`, схема в `api()` — паттерн `useHostMetrics`/`useSessionsSnapshot`). **Чистые хелперы (тестируемы без React) `onecLoad.ts`:** `classifySession` (приоритет blocked > long > active > silent > idle: блокировка `blocked-by-dbms`/`-ls`≠0; долгий вызов `duration-current` ≥ 5с; молчит — нет вызова и `last-active-at` старше 5 мин относительно `capturedAtUtc`); `sortSessionsByLoad` (по `cpu-time-current`, тай-брейк `duration-current`, null тонут вниз, без мутации); `availablePerformanceBand` (тинт процесса, пороги 800/500 при capacity 1000); форматтеры `formatMs`/`formatSignedMb` (знак GC сохраняется)/`formatBytes`/`formatAvgCallMs` (секунды rac→мс)/`shortUuid` — **null → «—», не 0**. **Компоненты:** `OneCSessionsTable` (плотная таблица: №/пользователь/хост/приложение/ЦП/длит./СУБД-доля/память/активность `RelativeTime`/бейдж состояния `StatusBadge`, тултип с рабочим процессом; собственное пустое состояние; **отдельно** от `/sessions` — без kill-действия), `OneCProcessesTable` (процесс/pid/доступная производительность с тинтом/ср.вызов/память), `OneCLoadSection` (контейнер с двумя секциями, skeleton/ошибка; **общий empty-state «нет активной нагрузки»** когда оба списка пусты — бэкенд без Available-флага, решение 066: отсутствие сигнала ≠ ошибка, обычно rac не настроен/нет активных сеансов; хинт на RAS-health дашборда). Подключено в `PerformancePage` ниже host-блоков, рендерится всегда (свой источник). i18n `performance.onec.*` (RU; состояния, заголовки таблиц, пустые состояния). Семьи/measuring-честность раздела — как на MLC-065 (host-часть не тронута). Канон present-tense: `05_UI_REQUIREMENTS.md` §3.8 (drill-down как построенный + empty-state + nullable→«—»). Тесты по образцу `features/sessions`/`features/performance`: `onecLoad.test` (классификация/сортировка/форматтеры, 26 кейсов), `useOneCLoad.test` (endpoint+схема+ошибка), `OneCSessionsTable.test` (null→«—», подсветка blocked/long, сортировка, пустое имя/список). `pnpm type-check`/`lint`/`test` (261 тест) зелёные. **Браузерная верификация пройдена под живым backend (admin) — и окупилась: поймала критичный баг границы уже смерженного экрана.** Backend глобально опускает null-поля (`JsonIgnoreCondition.WhenWritingNull`, `Program.cs`) → у idle-сеанса отсутствуют `process`/`connection`, а Zod `.nullable()` не пропускает **отсутствующий** ключ → `ApiSchemaError` ронял весь снимок при любом idle-сеансе (idle — норма, секция была сломана почти всегда). **Фикс (PR #44):** nullable perf-поля → `.nullish()` + нормализация в `null` (паттерн `omittable()`, `lib/apiSchema.ts`, ADR-10.1/MLC-016) + регресс-тест на **сыром** ответе backend с опущенными ключами. **Урок (память `api-omits-null-fields`):** Zod-границы тестировать на сыром ответе backend (опущенные null-ключи), а не на полном объекте — unit-фикстуры со всеми ключами дефект не ловят. ADR не затронуты.
- `MLC-068` — Backend: SQL DMV realtime-проба «1С грузит SQL?» (трек «Анализ быстродействия 1С», Фаза 3 backend) — Done (2026-06-09). Объём/права подтверждены разведкой `MLC-063`. **Application:** порт `ISqlPerformanceProbe` + нейтральные DTO `SqlPerformanceSnapshot` (статус/`Measuring`/активные запросы/IO-stall/wait-дельта) с `SqlActiveRequest`/`SqlDatabaseIo`/`SqlWaitDelta` и enum `SqlProbeStatus` (`Ok`/`PermissionDenied`/`Unavailable`, на проводе строкой); response `SqlPerformanceView` (snapshot + `SqlDatabaseAttribution[]`) + **чистый `SqlAttributionResolver`** (сшивка `DB_NAME`→`Infobase.DatabaseName`→tenant, регистронезависимо, незарегистрированная база → null-клиент). **Infrastructure:** адаптер `SqlPerformanceProbe` — **чистый ADO.NET** (`Microsoft.Data.SqlClient`, как `SqlDatabaseDiscovery`), **НЕ Windows-only** (без `[SupportedOSPlatform]`/`#pragma CA1416`). Источники: `sys.dm_exec_requests`⨝`dm_exec_sessions` `OUTER APPLY dm_exec_sql_text` (топ-50 по cpu_time, только user-сессии, кроме своего `@@SPID`; цепочки блокировок через `blocking_session_id`, 0→null), `dm_io_virtual_file_stats` (IO-stall по базам), `dm_os_wait_stats` (wait). IO-stall и wait **кумулятивны с старта SQL → дельта** между poll через singleton-стейт (паттерн host-CPU% MLC-064); первый poll `Measuring=true` (активные запросы мгновенны, доступны сразу); доброкачественные idle-ожидания отфильтрованы, отрицательная дельта (рестарт счётчиков) отброшена. **Подключение** наследует параметры из `ConnectionStrings:Default` + `InitialCatalog=master` (DMV серверного охвата; single-node co-located → строка панели = сервер 1С-инфобаз, новой настройки нет). **Честный degraded:** `HAS_PERMS_BY_NAME(...,'VIEW SERVER STATE')` проверяется первым → нет права → `Status=PermissionDenied` (не пустой «всё спокойно», паттерн MLC-064a); SQL недоступен/строка не задана → `Unavailable`. **Гранулярность — база** (SQL→сеанс→юзер не закладывалась, ADR-26; признак 1С — `program_name='1CV83 Server'`, `IsOneC`). **Web:** endpoint `GET /api/v1/performance/sql` (Viewer, vertical slice — к DMV только через порт, атрибуцию джойнит из своего `AppDbContext`: `Infobase`⨝`Tenant`). Типы smallint в DMV (`session_id`/`blocking_session_id`/`database_id`) читаются `GetInt16`. **Тесты (+21, всего 483):** чистый `SqlAttributionResolver` (регистр/дедуп/null-клиент/детерминизм при дубле), чистые дельты `ComputeWaitDeltas`/`ComputeIoDeltas` (прирост/сорт/idle-фильтр/новые-сброс-нулевые/cap-топ) + `IsOneCProgram`, endpoint через `StubSqlPerformanceProbe` + in-memory БД (атрибуция Ok + pass-through `PermissionDenied`). `dotnet build`/`format`/483 теста зелёные. Канон present-tense: `DECISIONS.md` ADR-26 (Фаза 3 endpoint + адаптер/права/атрибуция) + `04_INFRASTRUCTURE.md` §6.2 + `OPERATIONS.md` «Быстродействие — required permissions» (`VIEW SERVER STATE`, grant на login не на группу). UI-вкладка SQL — следующая задача `MLC-069`. ADR-16/20/single-node/RU-only не затронуты. **Замечание куратору (out-of-scope):** проба полагается на co-located топологию (`DataSource` строки панели = инстанс 1С-инфобаз); при разнесении SQL потребуется явный источник инстанса (опора на `ISqlInstanceDiscovery`/MLC-056) — отложено как gated на смену топологии (single-node — locked constraint).
- `MLC-069` — Frontend: вкладка SQL-активности (трек «Анализ быстродействия 1С», Фаза 3 — **завершает её 2/2**) — Done (2026-06-09). Секция `SqlLoadSection` «1С грузит SQL?» — третий live-источник на `/performance` (ниже host/1С), поверх `GET /performance/sql` (MLC-068). Таблица активных запросов (`SqlActiveRequestsTable`: топ по CPU, текст запроса, бейдж 1С по `isOneC`, блокировка «ждёт сеанс N»/«блокирует» по `blockingSessionId`), цепочки блокировок (`SqlContentionTables`), дельта wait-stats + IO-stall по базам, нагрузка по базам/клиентам (`SqlDatabaseLoadTable`, джойн строк DMV с `databases`-атрибуцией ответа). Чистые `sqlLoad.ts`; `useSqlPerformance` (polling 5с, `placeholderData: prev`); честный degraded `SqlStatusBanner` по `status` (`PermissionDenied`/`Unavailable`, образец MLC-064a); `Measuring` (wait/IO дельта-0 на первом poll); nullable→«—». **Урок [[api-omits-null-fields]] применён превентивно:** Zod-граница через `omittable()` + схема-тест на сыром omit-null ответе (тот же риск, что live-verify поймала на MLC-067). i18n `performance.sql.*`; +20 тестов (283 FE), type-check/lint/build зелёные. **Live e2e на стенде** (backend elevated + MSSQL + 1С под отчётом): Zod принял реальный omit-null ответ, реальные wait-дельты/IO-stall, активные 1С-запросы с бейджем, атрибуция база→клиент (незарегистр. база→«—»); подтвердил двустороннюю атрибуцию (отчёт CPU-bound в rphost, не SQL). Канон present-tense `05_UI_REQUIREMENTS.md` §3.8. ADR не затронуты.
- `MLC-070` — Backend: Recording (запись по требованию) (трек «Анализ быстродействия 1С», Фаза 4 backend) — Done (2026-06-09). **Сущности-телеметрия** `PerfRecording` (Id/StartedAtUtc/StoppedAtUtc?/Status `Active`/`Stopped`/`Interrupted`/StartedBy/StopReason? `Manual`/`TimeLimit`/`SampleLimit`) + `PerfRecordingSample` (host-метрики уровня 1 плоскими колонками + `ProcessesInaccessible` + JSON-колонки `ProcessGroupsJson`/`OneCLoadJson`/`SqlLoadJson`, FK **cascade**) в `Infrastructure.Reporting` — рядом с `LicenseUsageSnapshot`, **намеренно не** в адаптерном `Infrastructure.Performance` (guard `LayerBoundaryTests` запрещает Web ссылаться на него; Web читает телеметрию vertical slice ADR-20). Enum'ы — int (`HasConversion`), на проводе строкой. JSON переиспользует Application-записи live-снимков (`ProcessGroupUsage`/`OneCLoadSnapshot`/`SqlPerformanceSnapshot`) через общий `PerfSampleJson`. Миграция `MLC070PerfRecordings` (BOM/LF нормализованы — гоча CLAUDE.md). **Application:** порт `IPerfRecordingService` (Start/Stop/SampleOnce/RecoverInterrupted/HasActiveRecording) + исходы `PerfRecordingStartResult`/`*StopOutcome`. **Infrastructure:** singleton `PerfRecordingService` — одна активная запись (id+старт+счётчик сэмплов) под `SemaphoreSlim`, БД и scoped `IClusterClient` через `IServiceScopeFactory` (паттерн `HotTierPollingService`); фоновый `PerfRecordingSamplingService` (`BackgroundService`) тикает по `Performance.RecordingSampleIntervalSeconds` (деф. 15с) и зовёт `SampleOnceAsync` (no-op без активной записи). Тик: host (`IHostMetricsProbe`) + топ-N сеансов/процессов 1С (`IClusterClient`) + SQL (`ISqlPerformanceProbe`) → сэмпл (best-effort: недоступный источник → JSON null, не ложный нуль). **Авто-стоп** — два независимых лимита (что раньше): `Performance.RecordingMaxDurationMinutes` (деф. 60) / `…MaxSamples` (деф. 1000); триггерный сэмпл пишется до стопа. Время через `TimeProvider` (детерминированные тесты). Рестарт процесса → осиротевшая `Active`→`Interrupted` на старте (best-effort, как partial-бакет ADR-25). **Web:** `/api/v1/performance/recordings` — `POST` (старт, 409 `RECORDING_ACTIVE` если уже идёт) + `POST …/{id}/stop` + `DELETE …/{id}` (409 если ещё `Active` — сначала стоп) = **Admin**; `GET` (список свежие-сверху) + `GET …/{id}` (ряд сэмплов, JSON-колонки десериализованы) = `Viewer`. Аудит старт/стоп **не** заведён (ADR-26; новый `AuditActionType` = замороженный номер, серия 600+ при необходимости). **Спавн-бюджет:** сэмплинг спавнит rac.exe+DMV по таймеру пока идёт запись — единственный perf-путь без открытой вкладки, ограничен авто-стопом на интервале сэмплинга (≫ 5с live-poll); отмечено в ADR-3.3. **Тесты (+29, всего 512):** сервис старт/повторный-старт/стоп/авто-стоп по времени и числу сэмплов (через `TimeProvider`)/recover-Interrupted/no-op-без-записи; persistence+FK-cascade на SQLite; API (Created/Conflict/Ok/NotFound/NoContent) + роль-гейт по метаданным маршрутов. `dotnet build`/`format`/512 тестов зелёные. Канон present-tense: `DECISIONS.md` ADR-26 (Recording реализован) + ADR-3.3 (спавн-каденция записи) + `04_INFRASTRUCTURE.md` §6.3. UI записи — следующая задача `MLC-071`. ADR-16/20/single-node/RU-only не затронуты.
- `MLC-071` — Frontend: Recording UI (трек «Анализ быстродействия 1С», Фаза 4 — **завершает трек 9/9**) — Done (2026-06-09). Секция `RecordingSection` на `/performance` **ниже трёх live-источников** — единственный персистируемый источник раздела (поверх `/api/v1/performance/recordings`, MLC-070). **Роль-гейт:** управление (старт/стоп/удаление) = `Admin` (гейт по `useMe`, контролы скрыты у Viewer — совпадает с `Viewer`-читаемостью самого роута); список/просмотр = `Viewer`. **Контролы+индикатор:** Admin-кнопка старт (или стоп при активной) + пульсирующий «Идёт запись» (виден всем); старт показывает 409 `RECORDING_ACTIVE` тостом (`matchConflictCode`); стоп/удаление подтверждаются `AlertDialog` (06_UI_DESIGN — деструктив/необратимое). **Список** (`RecordingsTable`): старт/окончание/статус (`Идёт`/`Остановлена`/`Прервана` + stop-reason)/кто/число сэмплов; ряд → просмотр, Admin — per-row удаление (disabled у `Active`, бэкенд вернёт 409). **Просмотр** (`RecordingDetailDialog`, диалог): метаданные + график host во времени (`RecordingHostChart`, recharts `ComposedChart` — ЦП% и память-занято% на левой оси 0–100, латентность диска мс на правой) + топ-виновники 1С/SQL за период (реюз таблиц `OneCSessionsTable`/`OneCProcessesTable`/`SqlActiveRequestsTable`/`SqlDatabaseLoadTable` MLC-067/069 на **пиковых-за-период срезах** — момент наибольшей нагрузки каждого сеанса/процесса/запроса; чистый `recordingAggregation.ts`) + экспорт. Сэмплы несут только snapshot DMV-пробы (без атрибуции база→клиент — она лишь в live-эндпоинте) → SQL-таблицы с пустой картой, клиент «—». **Экспорт** (`RecordingExportMenu`, образец `features/reports/export`): «Скачать» CSV (синхронно, BOM/«;»/десятичная запятая для RU-Excel) + Excel (lazy SheetJS-чанк, общий с reports) ряда host-сэмплов. **Граница API:** Zod-схемы `recordingSummary`/`recordingList`/`recordingDetail`/`recordingSample` (`types.ts`, критичная ADR-10.1/MLC-016); `stoppedAtUtc`/`stopReason` (нет у активной) и `oneC`/`sql` сэмпла (нет у ненастроенного источника) через **`omittable()`** — бэкенд опускает null-поля (`JsonIgnoreCondition.WhenWritingNull`), `.nullable()` упал бы. **Хуки** `useRecordings` (polling 5с — свежесть индикатора/счётчика; `useRecordingDetail` enabled по id) + старт/стоп/удаление через `useInvalidatingMutation`. i18n `performance.recording.*`. **Тесты (+21, 304 FE):** схема на сыром omit-null ответе (урок `api-omits-null-fields` превентивно), агрегация (пиковые срезы, память%/латентность, null-стойкость), хуки (схема+методы+пути), CSV (BOM/разделитель/мс/строки), **регресс на коллизию queryKey** (деталь vs список). `pnpm type-check`/`lint`/`test`/`build` зелёные (lazy xlsx — общий чанк). Канон present-tense `05_UI_REQUIREMENTS.md` §3.8 (запись как построена). **Live e2e пройден на нагруженном стенде** (admin под перепроведением документов): старт→34 сэмпла по таймеру→стоп (диалог)→просмотр (график host во времени + пиковые виновники: `BackgroundJob` ЦП 2.4с / rphost avail-perf 322, SQL `INSERT #tt20` по `mitpro`)→CSV-экспорт (34 строки, RU-формат). **Live-verify окупился — поймал баг:** `useRecordingDetail(null)` использовал `recordingsQueryKey` как fallback-ключ → у закрытого диалога `data` подхватывал массив-список, `data.recording.status` падал (JSX-проп вычисляется при создании элемента, до решения Radix не монтировать); **фикс — обособленный ключ детали (`recordingDetailQueryKey("__none__")`) + регресс-тест** на непопадание массива в disabled-деталь. ADR-16/20/26/single-node/RU-only не затронуты. **Трек «Анализ быстродействия 1С» (`MLC-063..071`) завершён 9/9 — активной задачи не остаётся; Фаза 5 (UI-холистик, `MLC-062`) — вне трека, gated.**
- `MLC-072` — Верификация корректности метрик «Быстродействие» (диагностика/research, прод-код не менялся) — Done (2026-06-09). Повод: оператор видел всплеск CPU `sqlservr`, которого нет в Диспетчере задач. На нагруженном стенде (16 лог. ядер, backend элевирован, `processesInaccessible=0`) одновременно сняты наш API (`/host`,`/onec-sessions`,`/sql`, 14 поллов×5с) и 3 эталона: `Get-Counter` (PDH, плотный ~1.8с), повторная WMI-перечитка, прямой rac/DMV. **Вердикт: метрики КОРРЕКТНЫ, баг величины НЕ подтверждён.** `sqlservr` CPU 1.53% = `Get-Counter '\Process(sqlservr)\% Processor Time' ÷ 16` = ДЗ → нормировка на `Environment.ProcessorCount` верна и эмпирически подтверждена; host CPU%/очередь/RAM/диск-латентность (ручной cook `PERF_AVERAGE_TIMER` точен), OneC/Mssql-семьи, rac-сеансы (`cpuCur`/`availPerf`/`mem`), DMV-снимок — все совпали с эталонами. «Всплеск, которого нет в ДЗ» — **методика отображения, не ошибка чисел**, 4 механизма: (1) 5с-усреднение секции vs ~1с мгновенный ДЗ (алиасинг — за секунду sqlservr гулял 1.05→2.29%); (2) FE `placeholderData` держит прошлый кадр ≥5с; (3) **рассинхрон базы времени — host-гейдж мгновенный (WMI), а раскладка по семьям дельта-средняя за 5с → визуально «не сходятся» (host 38% / семьи 16%)** — главный источник недоверия; (4) скос `now`↔чтение ~560мс (~10% систематики, всплесков не порождает; +observer-effect: сам опрос спавнит rac×2+WMI+DMV). UX-находки (выравнивание базы времени host↔атрибуция + `SOS_WORK_DISPATCHER` в benign-список + подпись усреднения) **отложены пользователем как опция `MLC-073`** (P3, отображение, не корректность). Ограничение прогона: реальный SQL-bound всплеск не пойман (нагрузка была CPU-bound в rphost, SQL тих — как MLC-063) → при необходимости прицельный повтор под тяжёлым SQL. Прод-код/канон не менялись (research). ADR не затронуты.
- `MLC-074` — Фикс: retention-джобы падали при retry-стратегии EF (P2-дефект, найден в логах backend на MLC-072; вне трека «Быстродействие») — Done (2026-06-09). **Симптом:** при `EnableRetryOnFailure` (`DependencyInjection.cs:50` → `SqlServerRetryingExecutionStrategy`) ручная `BeginTransactionAsync` вне `CreateExecutionStrategy().ExecuteAsync` несовместима со стратегией повторов → `The configured execution strategy ... does not support user-initiated transactions` → `dbo.LicenseUsageSnapshots`/`dbo.AuditLogs` не чистились на **реальном SQL Server**. **Фикс (обе джобы):** тело одной батч-итерации (begin→delete→commit) обёрнуто в `var strategy = _db.Database.CreateExecutionStrategy(); await strategy.ExecuteAsync(async () => { … })` — каждый батч = отдельная атомарная retriable-единица; внешний `do-while` (накопление `totalDeleted`, проверка `lastBatch == BatchSize`) остался снаружи. Поведение 1:1: `BatchSize=5000`, commit-per-batch (против lock escalation), `OperationCanceledException` пробрасывается, `LogPurgeFailed` при сбое, аудит-запись `AuditLogsPurged` только при `totalDeleted>0`. Смысл ADR-25 (retention) не изменён. **Почему не ловили (дыра класса MLC-008):** retry-стратегия активна только на SQL Server, а юнит-тесты идут на SQLite/InMemory (`NonRetryingExecutionStrategy`, гарда нет). **Тесты (+2, всего 514):** добавлен тест-дубль `TestHelpers.RetriesOnFailureExecutionStrategy` (`RetriesOnFailure=true`) + перегрузка `SqliteTestDb.Create(Action<SqliteDbContextOptionsBuilder>)` для навешивания стратегии. (1) **License** (`LicenseUsageRetentionJobTests`): под ретраящей стратегией джоба удаляет старое корректно — **до фикса падала точной prod-ошибкой** (репро провайдер-агностично: гард живёт в ядре EF, не в SQL-Server-провайдере; на багованном коде упал на `ToListAsync`→`OnFirstExecution`). (2) **Audit** (`AuditRetentionJobTests`, новый файл): raw `ExecuteSql` (`DELETE TOP`) в EF Core 10 **обходит** execution strategy (идёт прямо в `RelationalCommand`, минуя `OnFirstExecution`), поэтому strategy-гард на SQLite для него не воспроизводим; дискриминатор — счётчик запусков стратегии (`onFirstExecution`): до фикса джоба минует стратегию (counter=0, нет и retry-защиты), после — батч идёт через `CreateExecutionStrategy().ExecuteAsync` (counter>0). Оба теста проверены red→green. **Live e2e на реальном SQL Server** (`Server=.`, тот же `EnableRetryOnFailure`): временный харнесс вставил маркер-строки 2015г в обе таблицы, обе джобы (retention=3000д, cutoff≈2018) отработали **без strategy-ошибки** и реально удалили маркеры; реальные данные 2025г (3.5М снимков / 99.6k аудита) целы (totals неизменны), строк <2018 не осталось; харнесс удалён. `dotnet test` 514 зелёных, `dotnet format` чист. Канон не затрагивался (баг кода). ADR-25/20/3.3/single-node/RU-only не затронуты.
- `MLC-075` — Канон-фундамент трека «Резервное копирование баз SQL» (Architecture, doc-only, decide-before-code) — Done (2026-06-09). Фича упиралась в **Locked-ADR-15** («Backup orchestration … permanently out of scope … re-introducing … requires explicitly revoking ADR-15 first») — поэтому не «добавить ADR поверх», а **корректно изменить решение**. **ADR-15 изменён** (расщеплён: in-app 2FA + *плановая*/scheduled оркестрация + restore остаются out-of-scope/ответственностью оператора; добавлено **узкое on-demand-исключение** со ссылкой на ADR-27, подчёркнуто сосуществование с внешней дифф-стратегией). **Заведён ADR-27** «On-demand SQL database backup (operator-initiated, `COPY_ONLY`)»: всегда `COPY_ONLY` (не сбрасывает DCM → не ломает внешний diff; признак diff/full сервера не нужен); адаптер `ISqlBackupService`→`SqlBackupAdapter` (чистый ADO.NET, ADR-20, never-throws-degraded); оркестратор-насос `IBackupOrchestrator`+`BackupPumpService`, очередь=таблица `DatabaseBackups` (`Infrastructure.Reporting`), пер-тик потолок `Backup.MaxParallel`+замок-на-базу+FIFO; keep-latest-1 (новый→`RESTORE VERIFYONLY`→delete old) + TTL-джоба; server-side `xp_create_subdir`/`xp_fixeddrives`/`xp_delete_file`; disk-guard по оценке used-pages (нет оценки→не стартуем, без GB-floor); роли запуск=Viewer / удаление+настройки=Admin; слоты аудита 510–514 (500-серия); учётке панели нужен **sysadmin**. Кросс-реф у revoked **ADR-9** обновлён. **Present-tense описания фич в `04`/`05`/`OPERATIONS` сознательно отложены в код-задачи** (конвенция: канон описывает построенное — ADR-26/OPERATIONS-права писались в MLC-064/064a/068, не в doc-задаче). Трек нарезан `MLC-075..078`; спека — `.claude/plans/lazy-cuddling-finch.md`. Изменены только `docs/DECISIONS.md`+`docs/PROJECT_BACKLOG.md` (кода нет). ADR-16/20/25/26/single-node/RU-only не затронуты.
- `MLC-076` — Backend-фундамент трека «Резервное копирование баз SQL» (ADR-27) — Done (2026-06-09). **Settings:** 4 ключа `Backup.FolderPath` (Path, БЕЗ default — зависит от инсталляции, паттерн `OneC.RAS.ExePath`) / `Backup.TtlHours` (Number, «24», 1..8760) / `Backup.MaxParallel` (Number, «2», 1..8) / `Backup.DiskSafetyMarginMb` (Number, «2048», 0..1048576) в `SettingKey`+`SettingDefinitions` (сидер подхватывает сам). **Аудит:** слоты 500-серии `BackupRequested=510`/`BackupSucceeded=511`/`BackupFailed=512`/`BackupDeleted=513`/`BackupsPurged=514` (int заморожены; +5 InlineData во freeze-тест). **Application/Backups:** `BackupStatus {Queued=0,Running=1,Succeeded=2,Failed=3}` + `BackupFailureReason {None=0,InsufficientSpace=1,EstimateUnavailable=2,PermissionDenied=3,BackupFailed=4,Interrupted=5}` (int ЗАМОРОЖЕНЫ — дисциплина `PerfRecordingModels`; в БД `HasConversion<int>`, на проводе строкой) + `SqlBackupResult`/`SqlDeleteResult`; порт `ISqlBackupService` («never throws», образец `ISqlPerformanceProbe`): `BackupAsync(server,db,folderRoot,safetyMarginMb,ct)` = ВЕСЬ безопасный цикл одной операции, `DeleteBackupsOlderThanAsync(server,folderPath,cutoffUtc,ct)` — для ретенции-077 и Admin-удаления. **Сущность:** `DatabaseBackup` в `Infrastructure.Reporting` (НЕ в адаптерном неймспейсе — guard; `InfobaseId` Guid БЕЗ FK — запись переживает удаление инфобазы, образец `PerfRecording`/`LicenseUsageSnapshot`), конфиг inline в `AppDbContext` (enum→int; индексы `RequestedAtUtc` и `(DatabaseServer,DatabaseName,Status)` — дорога насоса), `DbSet<DatabaseBackup>`; миграция `MLC076DatabaseBackups` (BOM/LF нормализованы, `db-reset.ps1` прошёл). **Адаптер:** `SqlBackupAdapter` (`Infrastructure.Backups`, internal sealed, чистый ADO.NET — НЕ Windows-only, без CA1416, образец `SqlPerformanceProbe`; `IConfiguration`+`TimeProvider`+`ILogger`, строка = `ConnectionStrings:Default` с `DataSource=server`/`InitialCatalog=master`, LoggerMessage, catch `SqlException`/`InvalidOperationException`→типизированный degraded): (0) `IS_SRVROLEMEMBER('sysadmin')`≠1 → `PermissionDenied`; (1) базы нет в `sys.databases` → `BackupFailed` «не найдена»; (2) оценка used-ROWS-страниц `FILEPROPERTY('SpaceUsed')` через `sp_executesql` + `USE QUOTENAME(@db)` c OUTPUT-параметром, NULL → `EstimateUnavailable` (НЕ стартуем); (3) folderRoot строго `X:\…` (UNC/относительный → `BackupFailed` с понятным текстом), свободно из `xp_fixeddrives` (temp-таблица) по букве, требуется ≥ оценка/1024 + margin → иначе `InsufficientSpace` с числами; (4) `xp_create_subdir root\db` (идемпотентно); (5) `BACKUP DATABASE QUOTENAME(@db) TO DISK=@path WITH COPY_ONLY, COMPRESSION, CHECKSUM, FORMAT, INIT, STATS=10` через `sp_executesql` (`@path=…\db\db_yyyyMMdd_HHmmss.bak`, время из `TimeProvider`; имя — ТОЛЬКО QUOTENAME, путь — ТОЛЬКО параметр; CommandTimeout 4 часа, НЕ 5с пробы); (6) `RESTORE VERIFYONLY WITH CHECKSUM` ДО удаления старого; (7) только после verify `xp_delete_file 0,@subfolder,N'BAK',@cutoff` (cutoff=старт BACKUP — keep-latest-1, новый-перед-удалением); (8) размер из `msdb.dbo.backupset.compressed_backup_size` (join `backupmediafamily` по `physical_device_name`, TOP 1 по `backup_finish_date` DESC; не нашли → null, не fail). **Гоча времени:** `xp_delete_file` сравнивает файловые timestamp'ы в ЛОКАЛЬНОМ времени SQL-хоста → UTC-cutoff порта конвертируется через локальную TZ панели (легитимно: co-located single-node; задокументировано в 04 §8). **Фейк:** `FakeSqlBackupService` (`Infrastructure/Backups/Testing`, образец `StubSqlPerformanceProbe`) — настраиваемые результаты, потокобезопасная запись аргументов Backup/Delete, `BackupGate` (TCS) для «подвисших» бэкапов в конкурентных тестах оркестратора 077. **DI:** `AddSingleton<ISqlBackupService, SqlBackupAdapter>` (stateless). **Layer-guard:** `MitLicenseCenter.Infrastructure.Backups` добавлен в forbidden-list `Web_does_not_reference_OneC_IIS_infrastructure_adapters_directly`. **Тесты (+32, всего 546):** persistence-контракт SQLite (`dbo.DatabaseBackups`, оба индекса, отсутствие FK, enum round-trip), 4 settings-definitions (kind/default/диапазоны/дефолт-в-диапазоне), заморозка int (аудит 510–514 + оба enum бэкапа), контракт фейка (запись аргументов/результаты/ворота), чистый `TryGetDriveLetter` (8 кейсов: локальные диски vs UNC/относительный). Сам адаптер — integration-only (T-SQL юнитами не покрывается, как `SqlPerformanceProbe`/`SqlDatabaseDiscovery`); live e2e — в 078. **Канон:** 04 §4 каталог +4 ключа; §7 переформулирован под изменённый ADR-15 («strategy — оператор», кросс-реф ADR-27); новый §8 «On-demand SQL database backup — adapter» (порт/SQL-механика/degraded-статусы, по образцу §6.2); OPERATIONS — новая секция «Бэкап — required permissions» (sysadmin конкретному логину — `db_backupoperator` НЕ хватает для `xp_*`; симптом = `PermissionDenied`; локальный диск SQL-хоста; пишет файлы service account SQL; disk-guard; keep-latest; `COPY_ONLY`-сосуществование с внешним diff). UI/эндпоинтов нет — про них не написано (объём 077/078). Оркестратор/насос/джоба/эндпоинты НЕ делались (строго фундамент). Попутно найден `[Doc divergence]` (Recording-ключи MLC-070 нет в каталоге 04 §4) — заведён в «Кандидаты», молча не чинился; **закрыт куратором 2026-06-10 вариантом (а)** (таблица 04 §4 дополнена тремя строками `Performance.Recording*` — она остаётся полным каталогом всех ключей). `dotnet test` 546 зелёных, `dotnet format` чист. ADR-16/20/25/26/single-node/RU-only не затронуты.
- `MLC-077` — Backend: оркестрация + API трека «Резервное копирование баз SQL» (ADR-27) — Done (2026-06-10). **Application:** `BackupModels` дополнен `BackupRequestOutcome {Queued=0, AlreadyActive=1}` + `BackupRequestResult(Outcome, BackupId)` (у `AlreadyActive` — id СУЩЕСТВУЮЩЕЙ активной строки); новый порт `IBackupOrchestrator` (`RequestAsync`/`PumpOnceAsync` — тест-шов, образец `SampleOnceAsync`/`RecoverInterruptedAsync`/`WaitForWakeAsync` — точка ожидания насоса, wake-или-таймаут); `Application/Jobs/IBackupRetentionJob` (образец `IAuditRetentionJob`). **Оркестратор** `Infrastructure/Backups/BackupOrchestrator` (internal sealed singleton, образец `PerfRecordingService`: `SemaphoreSlim`-gate на учётные операции, `IServiceScopeFactory` для scoped `AppDbContext`/`IAuditLogger`, `TimeProvider`; in-memory `HashSet` выполняющихся пар server+db (OrdinalIgnoreCase) + wake-семафор ёмкости 1 с коалесценцией): `RequestAsync` под gate — есть Queued/Running той же пары → `AlreadyActive` с существующим id, иначе вставка `Queued` + wake; `PumpOnceAsync` — каждый тик ЗАНОВО читает `Backup.MaxParallel` (кламп 1..8 по definition; смена действует со следующего тика без рестарта, снижение не убивает идущие) и одним проходом FIFO-по-`RequestedAtUtc` + замок-на-базу + потолок, помечает `Running`+`StartedAtUtc` и пускает бэкап `Task.Run`'ом в свежем scope; **сам `BackupAsync` — ВНЕ gate** (бэкапы параллельны по замыслу — осознанное отличие от `PerfRecordingService`); папка читается к моменту старта (очистили пока ждал в очереди → строка `Failed/BackupFailed` с понятным текстом, без вызова адаптера); финиш под gate — успех → `Succeeded`+`CompletedAtUtc`+`FilePath`/`FileSizeBytes`+аудит 511, провал → `Failed`+`FailureReason`/`ErrorMessage`+аудит 512 (initiator = `RequestedBy` строки; tenantId не пишется — нет FK, связку несёт эндпоинт-аудит 510), снятие замка + wake (следующий из очереди стартует сразу); **оркестратор файлы НЕ удаляет** — keep-latest внутри `BackupAsync` (после VERIFYONLY, MLC-076); `RecoverInterruptedAsync` — осиротевшие `Running`→`Failed/Interrupted` («файл может быть неполным»), `Queued` не трогаются. **Насос** `BackupPumpService` (`BackgroundService`, образец `PerfRecordingSamplingService`): на старте recovery, цикл «wake ИЛИ таймаут 5с»→`PumpOnceAsync`, исключения тика логируются — сервис не падает; тонкая обвязка, юнитами не покрывается. **Retention** `Infrastructure/Jobs/BackupRetentionJob` (03:15 UTC `backup-retention`, смещено от 03:00/03:30): `Backup.FolderPath` не задан → логируемый no-op; (а) файлы — `DeleteBackupsOlderThanAsync(server, root\db, now−TTL)` по DISTINCT-парам строк (TTL = `Backup.TtlHours`, кламп 1..8760); (б) строки старше cutoff по `RequestedAtUtc`, НЕ трогая Queued/Running — provider-portable батчи (id-select→`ExecuteDelete`, образец `LicenseUsageRetentionJob`), каждый в `CreateExecutionStrategy().ExecuteAsync` (паттерн MLC-074); (в) аудит `BackupsPurged=514` (initiator System) только при >0 строк. **Web:** `Endpoints/Backups/` (vertical slice ADR-20; DTO `BackupSummary`+`StartBackupRequest` рядом) — `GET /api/v1/backups[?infobaseId=]`+`GET /{id}` (Viewer, свежие сверху), `POST` (Viewer: Infobase 404 → server+db; папка пуста → 409 `BACKUP_FOLDER_NOT_CONFIGURED`; `AlreadyActive` → 409 `BACKUP_ACTIVE`; 201 + аудит `BackupRequested=510` с TenantId инфобазы), `DELETE /{id}` (Admin: 404; Running → 409 `BACKUP_ACTIVE`; файл есть → server-side `DeleteBackupsOlderThanAsync(подпапка из FilePath, cutoff=now)` — keep-latest-1 означает «в подпапке ровно этот файл», провал → 409 `BACKUP_DELETE_FAILED` и строка ЦЕЛА (иначе .bak осиротеет невидимым), успех → строка удалена + аудит `BackupDeleted=513`). `Problems`/`ProblemCodes` +3 (`BACKUP_DELETE_FAILED` добавлен сверх спеки — у «вернуть ошибку» провала удаления не было кода; машинно-читаемый 409 в семействе); `AuditDescriptions` +2 билдера (510/513 — только Web-писанные; 511/512/514 пишут оркестратор/джоба в Infrastructure — Web-каталог им недоступен по направлению слоёв, формулировки инлайн рядом с кодом, образец `AuditRetentionJob`). **DI/Program:** singleton оркестратор + насос (`IHostedService`-forward), scoped джоба; `RecurringJob.AddOrUpdate("backup-retention", "15 3 * * *")`; `MapBackupsEndpoints`. `LayerBoundaryTests` менять не пришлось — `Infrastructure.Backups` уже в forbidden-list с MLC-076 (Web ходит только в `Application.Backups`-порты, guard зелёный). **Тесты (+37, всего 583):** `BackupOrchestratorTests` на `FakeSqlBackupService`+`BackupGate`+EF InMemory+`MutableClock` (гоча: имя InMemory-БД — СНАРУЖИ лямбды `AddDbContext`, иначе каждый scope получает свою пустую БД) — Queued-строка; дубль → `AlreadyActive` с тем же id; per-db эксклюзия (две Queued одной базы засеяны в обход `RequestAsync` — defence-in-depth); потолок 2→ровно 2 Running, ворота→третья следующим тиком; FIFO; динамический MaxParallel между тиками; кламп 0→1 и 99→8; успех (поля+аудит 511+`DeleteCalls` ПУСТ — keep-latest не дублируется) и провал по каждому Reason (аудит 512); recovery (Running→Interrupted, Queued цел); папка пуста → Failed без вызова адаптера; терминальные состояния дожидаются поллингом (финиш пишет фоновая задача), `DrainAsync` перед Dispose. `BackupRetentionJobTests` (SQLite): DISTINCT-вызовы с верным cutoff; кламп TTL; reap щадит Queued/Running/свежие; аудит только при >0; no-op без папки; зелёный под ретраящей стратегией (`RetriesOnFailureExecutionStrategy`, регресс класса MLC-074). Эндпоинт-тесты: list-фильтр/сортировка, 404'ы, 409 folder/active (без аудита), 201+аудит 510 с TenantId, DELETE: Running→409, успех→файл-вызов+строки нет+аудит 513, провал файла→строка цела, без файла→без вызова; роль-гейт по метаданным маршрутов (POST=Viewer, DELETE=Admin). `dotnet test` 583 зелёных, `dotnet format` чист. **Канон:** 04 §8 переименован в «On-demand SQL database backup (MLC-076/077)», добавлены §8.1 (оркестрация: очередь-таблица/насос/потолок/замок/FIFO/recovery), §8.2 (retention 03:15 UTC), §8.3 (таблица API+ролей). 05/UI не тронуты (нет UI — объём 078). Live e2e — в `MLC-078` (последняя задача трека). ADR-15/16/20/25/26/27/single-node/RU-only не затронуты.
- `MLC-078` — Frontend + live e2e трека «Резервное копирование баз SQL» (ADR-27) — Done (2026-06-10). **Завершает трек 4/4 (`MLC-075..078`).** **Фича `features/backups/`:** `types.ts` — Zod-схемы `backupSummarySchema`/`backupListSchema` (критичная граница ADR-10.1/MLC-016); **5 nullable-полей** (`startedAtUtc`/`completedAtUtc`/`filePath`/`fileSizeBytes`/`errorMessage`) опускаются на проводе (`WhenWritingNull`) → объявлены через `omittable()` (урок [[api-omits-null-fields]], превентивно как 069/071) + обязательный схема-тест на сыром omit-null ответе; `status`/`failureReason` — `z.enum(...).or(z.string().transform(...))`: незнакомое строковое значение будущего бэкенда деградирует к нейтральному бейджу с сырым именем (lookup `?? neutral` + i18n `defaultValue`), НЕ роняя весь список (образец «family» в performance/types), не-строка по-прежнему отвергается. `useBackups.ts` — `useBackups(infobaseId)` (поллинг 5с + `placeholderData: prev`, пока диалог открыт; ключ `["backups", id]` обособлен per-infobase, для null — `"__none__"` — урок коллизии ключей MLC-071) + `useStartBackup`/`useDeleteBackup` через `useInvalidatingMutation`. `BackupsDialog.tsx` — диалог одной инфобазы: «Сделать бэкап» (Viewer+Admin; **не блокируется при активном** — серверный замок-на-базу единственный источник правды, дубль → честный 409-тост), таблица со статус-бейджами (Queued «В очереди» neutral / Running info с пульсирующей точкой `animate-pulse` / Succeeded «Готов» success / Failed «Ошибка» danger + `failureReason` по-русски + `errorMessage` truncate с title), размер КБ/МБ/ГБ (чистый `backupFormat.ts`), времена, кем запрошен; null → «—», не нули; пустое состояние «Бэкапов ещё нет»; удаление — ТОЛЬКО Admin (гейт `useMe`, образец RecordingSection) через `AlertDialog` (`DeleteBackupDialog.tsx`, необратимо — файл сносится с диска SQL-хоста; disabled у Running). 409-тосты через `matchConflictCode`: `BACKUP_ACTIVE`/`BACKUP_FOLDER_NOT_CONFIGURED` (+подсказка про «Параметры»)/`BACKUP_DELETE_FAILED` («запись сохранена»). **Точка входа:** отдельная иконка-кнопка `DatabaseBackup` с тултипом «Бэкапы базы» в ряду действий `InfobaseRow` (новый обязательный проп `onBackups`), видна **обеим ролям** — запуск = Viewer (ADR-27), поэтому НЕ пункт admin-дропдауна (он у Viewer вообще не рендерится; дропдаун не тронут); подключено на `/infobases` и карточке клиента (`TenantDetailPage`). **`/settings`:** секция `settings.sections.backup` «Резервные копии баз» — 4 ключа `Backup.FolderPath` (text, плейсхолдер `D:\Backups`) / `TtlHours` (1..8760) / `MaxParallel` (1..8) / `DiskSafetyMarginMb` (0..1048576) через штатный `SettingField`, диапазоны зеркалят `SettingDefinitions`. **i18n:** блок `backups.*` (статусы/причины провала по-русски/тосты/подтверждение/empty) + `settings.sections/labels/hints` + **5 ключей `audit.actions.Backup*`** (510–514) + зеркало union `AuditActionType`/`AUDIT_ACTION_TYPES` в `features/audit/types.ts` (фильтр предлагает бэкап-действия; цвета бейджей — существующий суффикс-маппинг). **Тесты (+24, всего 328 FE):** схема на сыром omit-null Queued-ответе + незнакомый статус не роняет список + не-строка отвергается; формат (варианты статусов + деградация + размеры); хуки (endpoint+схема, disabled id=null не ходит в сеть и не подхватывает чужой кэш, POST body/DELETE путь); `BackupsDialog` (статусы/«—»/размер, Admin-гейт удаления, Viewer без удаления, пустое состояние, оба 409-тоста с точными текстами). `pnpm test`/`type-check`/`lint`/`build` зелёные. **Live e2e на стенде ПРОЙДЕН ПОЛНОСТЬЮ** (реальный SQL Server `Server=.`, sysadmin подтверждён `IS_SRVROLEMEMBER`; БД стенда была пуста после db-reset → first-run admin из лога сидера + 3 реальные базы `MlcE2e_{Alpha,Beta,Gamma}` ~260/130/130 МБ, клиент «E2E Клиент» + 3 инфобазы через API): `Backup.FolderPath` задан через новую секцию UI и сохранён; кнопка → тост «поставлен в очередь» → Running с пульсом → Succeeded поллингом, `.bak` в `папка\<база>\` (timestamp-имя), размер в UI 119.3 МБ ≈ файлу; повторные бэкапы → **keep-latest файлово** (3 запуска Альфы — в подпапке ровно 1 свежий файл, новый-перед-удалением); дубль активной (двойной клик) → тост 409 «уже выполняется или стоит в очереди»; **потолок `MaxParallel=2` пойман поллингом**: 3 POST подряд → снимок `Alpha:Running + Beta:Running + Gamma:Queued`, Гамма стартовала после освобождения слота; Admin-удаление через AlertDialog → файл исчез с диска + строка из списка + тост; сброс папки → тост `BACKUP_FOLDER_NOT_CONFIGURED` (настройка восстановлена); **Viewer-прогон** (учётка `e2e-viewer` через `/users`, форс-смена пароля отработала): кнопка бэкапов видна, admin-дропдаун скрыт, в диалоге «Сделать бэкап» есть / удаления нет, бэкап под Viewer успешен (`requestedBy: e2e-viewer`); **Zod принял реальные omit-null ответы — консоль чистая** (0 warn/error за всю сессию, оба окна ролей); журнал аудита по-русски: «Бэкап запрошен» (с клиентом) / «Бэкап выполнен» / «Бэкап удалён». **Канон:** `05_UI_REQUIREMENTS.md` — новый §3.9 (точка входа/диалог/роли/статусы/настройки/Zod-граница) + упоминание секции настроек в §3.3; 04/OPERATIONS не дублировались (бэкенд описан в 076/077). **Попутно найден кандидат** (см. «Кандидаты»): журнал аудита рендерит сырые `audit.actions.User*`-ключи (стейл MLC-060/061), подтверждено на стенде — вне объёма, молча не чинилось. E2e-артефакты стенда (клиент/инфобазы/базы `MlcE2e_*`/папка `F:\MlcE2eBackups`/учётка `e2e-viewer`) оставлены для ручной проверки куратором. ADR-15/16/20/25/26/27/single-node/RU-only не затронуты.

---

## Трек «UX-пересборка панели под single-host» — отчёты задач (пополняется по мере закрытия)

- `MLC-081` (UX-A) — **Единая страница «Базы» = Инфобазы + Публикации, вкладка «IIS», `/publications` удалена**
  (аудит §3.1 ⭐ + §6; первая задача трека) — Done (2026-06-10). **Ключевой факт, сделавший слияние дешёвым:**
  страница «Публикации» и так питалась от `GET /api/v1/infobases` (флаттенила вложенный объект `publication`
  строки инфобазы) — слияние чисто презентационное, API/БД/миграции не тронуты (ограничение трека соблюдено).
  **Структура:** `/infobases` = заголовок «Базы» + вкладки «Базы» | «IIS» (shadcn `tabs` добавлен через
  `scripts/shadcn-add.ps1`; зависимость `radix-ui` уже была). Вкладка «Базы» — прежняя таблица инфобаз,
  обогащённая публикационными колонками: Название · Клиент · Сервер БД · Статус базы · Публикация
  (StatusBadge статуса проверки + tooltip «сайт/путь + детали проверки» + **иконка источника** webinst/
  Конфигуратор/неизвестен с tooltip-предупреждением о гейте перезаписи — отдельной колонки нет, аудит §6) ·
  Версия платформы · Проверено (`RelativeTime` от `lastCheckAt`) · Действия. Колонки «Имя БД» и «Обновлена»
  убраны из списка (живут в форме); «ID в кластере» в списке и не было. **Колонка «Сервер БД» оставлена
  намеренно** — её убирает `MLC-082` (явный пункт его постановки). Вкладка «IIS» — `IisManagementCard`
  (MLC-047) перенесена как есть. **Действия строки** собраны в один admin-дропдаун: Изменить · Проверить
  публикацию · Опубликовать · Сменить платформу · Перенести · Удалить; кнопка «Бэкапы» осталась отдельной
  иконкой вне дропдауна (видна Viewer — ADR-27). **Bulk переехал как есть** (бар, диалоги publish/change-platform,
  гейт перезаписи, прогресс/частичный успех), но выбор теперь хранит **объекты** `Map<publicationId,
  PublicationListItem>` (не голые id): список серверно пагинирован (25/стр против бывших 200 одним куском),
  объекты с перелистанных страниц должны оставаться доступны bulk-диалогам; «выбрать все» = текущая страница;
  снятие успешных после прогона — 1:1. **Механика под капотом:** `toPublicationListItem(InfobaseListItem)`
  (новый маппер в `features/publications/types.ts` + тест на соответствие полей и нормализацию omit-null) кормит
  существующие диалоги без их правки; `usePublications()`-запрос/`publicationsQueryKey` удалены, мутации
  check/publish/change-platform и IIS-операции инвалидируют `["infobases"]` (префикс покрывает все страницы
  фильтров). `InfobaseRow`/`InfobaseTableHeader` общие с карточкой клиента — новые пропсы (onCheck/onPublish/
  onChangePlatform/selection) **опциональны**, `/tenants/:id` не передаёт их и не получает чекбоксов/
  публикационных пунктов меню (объём не расширяли). Режим «По клиенту» сохранён (новые колонки есть, чекбоксов
  нет — режим уходит целиком в `MLC-085`). **Удалено:** маршрут+lazy-чанк `/publications`, пункт меню
  (`GlobeIcon`), `PublicationsPage/Table/FiltersBar`, `usePublicationsPage`, `urlState`+тест; i18n-ключи
  `publications.title/table/filters/empty`, `nav.publications`; `nav.infobases`/`infobases.title` → «Базы»;
  `infobases.fields.status` → «Статус базы». **Потеря (осознанная):** клиентский фильтр по статусу публикации
  с бывшей `/publications` не переехал — постановка колонок/фильтров его не содержит, а честная реализация
  поверх серверной пагинации требует API-фильтра (запрещено в этапе 1); зафиксировано курaтору как кандидат.
  **Проверка:** FE 326 тестов / type-check / lint зелёные; BE 583 (после MLC-080). **Live-прогон на dev-стеке
  (2026-06-10):** вход admin — меню без «Публикаций», «Базы» с вкладками; check из меню строки → POST
  `/publications/{id}/check` 200 + авто-refetch `["infobases"]`; выбор 2 строк → bulk-бар → диалог bulk-publish
  (счётчик/предупреждение) — отменён; одиночные «Опубликовать» (токен `сайт/путь`, предупреждение
  source≠Webinst) и «Сменить платформу» (Select версий) — открыты/отменены; вкладка IIS — статус W3SVC
  «Запущен», списки пулов/сайтов с graceful-ошибкой (бек без elevation — гоча CLAUDE.md, не регресс);
  группировка «По клиенту» рендерится с новыми колонками; вход **Viewer** (`e2e-viewer`) — без чекбоксов/
  bulk-бара/дропдауна/кнопки добавления, бэкапы видны, IIS-вкладка read-only (без кнопок управления);
  `/publications` → редирект `/` (wildcard); консоль чистая. Создание базы вживую не прогонялось (нет живого
  кластера 1С под discovery в dev) — форма открывается, её код в задаче не менялся. **Канон 05/06 НЕ правился**
  (решение куратора — финальный док-PR `MLC-086`); расхождение зафиксировано в накопителе `[Doc divergence]`
  трека. **+ влитая `MLC-080`** (Testing · P3, отдельный коммит): запас тайминг-чувствительных ожиданий в
  бек-тестах 5с → 30с — `BackupOrchestratorTests.WaitUntilAsync` (единая точка всех ожиданий файла, флейк
  `PumpOnce_clamps_max_parallel_to_definition_range` CI PR #61) и `SettingsSnapshotTests.
  Concurrent_cold_readers_load_only_once` (`store.Entered.Wait` 5с→30с, флейк CI PR #65); других тайминг-ожиданий
  в обоих файлах нет (`Task.Delay(10)` — интервал поллинга, `Delay(50)` — намеренная пауза конкуренции);
  зелёный путь не замедлен — ожидания выходят по условию/сигналу. 583/583 зелёные локально.

- `MLC-082` (UX-B) — **Форма инфобазы без выбора сервера; колонка «Сервер БД» снята** (аудит §4.1–4.2;
  вторая задача трека) — Done (2026-06-10). Frontend-only, API/БД/миграции не тронуты, контракт POST/PUT
  инфобазы прежний (`databaseServer` уходит в запрос как раньше). **Ключевое решение:** `databaseServer`
  остался полем формы react-hook-form, но **скрытым** (UI-поля нет) — Zod-схема `validation.ts` НЕ менялась
  (parity с `InfobaseValidationRules.cs` цел, parity-тесты не тронуты). Источник значения: **создание** —
  `Defaults.DatabaseServer` из каталога настроек (прежний асинхронный prefill-эффект); **редактирование** —
  текущее `infobase.databaseServer` (правка базы НЕ мигрирует её сервер на значение настройки молча —
  подтверждено вживую на e2e-базе с `(local)` ≠ настройке `localhost`: сохранение оставило `(local)`).
  **Невидимая ошибка закрыта явно:** настройка пуста → zod-ошибка падает на скрытое поле → submit блокируется
  с toast'ом `infobases.errors.databaseServerNotConfigured` («Сервер СУБД не задан. Укажите его в „Параметрах“»),
  запрос в сеть не уходит (новый тест); `databaseServer` исключён из `ADVANCED_ERROR_KEYS` (раскрывать
  «Дополнительно» больше незачем — поля там нет). **Форма:** `PublicationFieldset` — группа «СУБД (SQL Server)»
  удалена целиком (остались две группы: «Инфобаза» + «Публикация в IIS»), discovery-пропсы SQL-инстансов сняты;
  `useInfobaseForm` — `useSqlInstances` из формы удалён (хук жив — им пользуется `DatabaseServerField` на
  /settings, теперь его единственный потребитель); пикер «Имя БД» работает сразу — `useDatabases`
  дёргается со скрытым значением (настройка/значение базы), двухшаговый «сначала сервер, потом база» исчез;
  `disabledHint` пикера перефразирован: `discovery.databaseServerFirst` («Сначала укажите сервер БД») →
  `discovery.databaseServerMissing` («Сервер СУБД не задан в „Параметрах“») — показывается, только пока
  настройки не загрузились или настройка пуста. Per-base переопределения «Сайт IIS» / «Версия платформы» /
  «Виртуальный путь» / «Физический путь» — на месте со своими дефолтами; автоподстановка путей из имени БД — 1:1.
  **Таблица:** колонка «Сервер БД» снята из общих `InfobaseTableHeader`/`InfobaseRow` (оба места разом:
  /infobases flat+grouped и /tenants/:id; публикационные пропсы по-прежнему опциональны — гоча MLC-081
  учтена), `infobaseColumnCount` 7→6 (skeleton/colSpan выровнены). Значение сервера остаётся видимым в
  сабтайтле диалога бэкапов (`backups.subtitle`, «…на сервере {{server}}») — единственное место, где оно
  уже показывалось. **i18n:** удалены `infobases.fields.databaseServer`, `form.databaseServerLabel`,
  `form.databaseServerPlaceholder`, `form.groupDatabase`, `form.groupDatabaseHint`, `discovery.databaseServerFirst`;
  обновлены `form.advancedHint` (без «Сервер БД») и `form.databaseNameSubsystemHint` («сервер задаётся в
  „Параметрах“»); добавлены `infobases.errors.databaseServerNotConfigured` и `discovery.databaseServerMissing`;
  `infobases.errors.databaseServerRequired` оставлен — на него ссылается нетронутая Zod-схема. **Тесты (+1,
  всего 327 FE):** новый кейс «настройка не задана → submit блокируется, toast с точным текстом, POST не
  уходит»; прежние кейсы prefill/edit-стартовых значений работают без правок (скрытое поле живо в form
  state). `pnpm test` / `type-check` / `lint` зелёные. **Live-прогон на dev-стенде (2026-06-10, обе роли):**
  Admin — форма создания: Клиент → База кластера → Имя БД (пикер сразу Select с 7 базами `localhost`,
  discovery без выбора сервера), «Дополнительно» = 2 группы с дефолтами из настроек (`Default Web Site`,
  `8.5.1.1302`), автоподстановка `/mlc082-test` + физпути из имени БД; создание end-to-end — POST ушёл со
  скрытым `databaseServer: localhost`, тост, база в списке (тестовая база после проверки удалена);
  редактирование «Альфа (бэкап e2e)» — поля сервера нет, сохранение оставило `(local)`; таблица admin =
  чекбоксы + 6 колонок без «Сервер БД». Viewer (`e2e-viewer`) — read-only таблица без колонки на /infobases
  (flat и grouped) и /tenants/:id, шапка/строки выровнены. Консоль чистая (0 warn/error). Статусы публикаций
  «Ошибка проверки» — backend без elevation (гоча CLAUDE.md), не регресс. **Канон 05/06 НЕ правился**
  (решение куратора — финальный док-PR `MLC-086`); расхождение (группа «СУБД» в «Дополнительно», двухшаговый
  discovery, колонка «Сервер БД») дописано строкой в накопитель `[Doc divergence]` трека. Бек-хвосты §7
  аудита не тронуты (`GET /discovery/databases?server=` остался как есть — FE передаёт сервер из настройки).

- `MLC-083` (UX-C) — **/settings: секция «SQL Server», «Значения по умолчанию» расформирована** (аудит §4.3,
  §5; третья задача трека; объединённая сессия с `MLC-084`, один PR, коммиты раздельно) — Done (2026-06-10).
  Frontend-only, два файла: `SettingsPage.tsx` (массив `SECTIONS`) + `ru.json`. **Раскладка:** новая секция
  «SQL Server» с единственным полем `Defaults.DatabaseServer` — сразу после «Подключение к 1С / RAS» (порядок
  секций = таблица §5 аудита: 1С → SQL → лицензии → IIS → опрос → ретенция → бэкапы); `IIS.DefaultSiteName`
  переехал в «Публикации IIS» к `DefaultVrdRoot` (секции добавлено описание — раньше его не было); секция
  «Значения по умолчанию для новых баз» исчезла (оба поля нашли дома). **Семантика:** подпись/hint поля
  переписаны под «единственное место правды» — label «SQL-инстанс, на котором живут базы клиентов», hint
  «Единственное место, где задаётся сервер СУБД…» (слово «дефолт» из подачи ушло); описание секции дублирует
  это и упоминает, что форма базы и пикер «Имя БД» используют значение автоматически (стыковка с MLC-082).
  **Ключ настройки `Defaults.DatabaseServer` НЕ переименован** — это бек-хвост §7 п.4 (этап 2); `Performance.*`
  по-прежнему без UI; `DatabaseServerField` (discovery-пикер MLC-056) не тронут — сменилась только секция-дом.
  Развилки /settings не пересматривались: пер-контрольное сохранение без общей «Применить», `OneC.Cluster.Server`
  скрыт, SQL-discovery только localhost. **Проверка:** FE 326 / type-check / lint зелёные на коммите задачи
  (панель полностью рабочая после этого коммита — ограничение трека). **Live-прогон на dev-стенде (2026-06-10,
  admin):** 7 карточек в порядке §5; «SQL Server» = описание + hint + discovery-пикер (`localhost`,
  «Обновить»/«Ввести вручную»/«Сохранить»); пер-контрольное сохранение прогнано round-trip'ом на
  `IIS.DefaultSiteName` (значение дошло до БД через `GET /settings` и возвращено обратно); форма инфобазы —
  поля сервера нет, пикер «Имя БД» работает сразу от настройки; toast/hint «Сервер СУБД не задан в „Параметрах“»
  (MLC-082) текстуально не менялся и по-прежнему ведёт к месту настройки — поле находится на «Параметрах»
  в секции «SQL Server». Консоль чистая. **Канон 05/06 НЕ правился** (решение куратора — финальный док-PR
  `MLC-086`); расхождение (05 §3.8 описывает секцию «Значения по умолчанию» и подпись «Сервер СУБД по
  умолчанию») — строкой в накопитель `[Doc divergence]` трека.

- `MLC-084` (UX-D) — **Профиль в топбар, сайдбар перегруппирован в 8 пунктов; + влитая `MLC-079`
  (аудит-i18n `User*`)** (аудит §3.3, §3.5; четвёртая задача трека; объединённая сессия с `MLC-083`) —
  Done (2026-06-10). Frontend-only, три коммита `MLC-084: …`. **Топбар:** дропдаун пользователя теперь
  показывает логин + роль (словарь `users.roles`, незнакомая роль — сырым именем; после удаления /profile
  это единственное место, где пользователь видит свою роль), пункты «Смена пароля» (диалог `Dialog` с
  готовой `ChangePasswordForm`: `showReset=false`, `onSuccess` закрывает диалог; форма не правилась — только
  её doc-комментарии) и «Выйти». **Маршрут `/profile` удалён** (lazy-чанк + `ProfilePage.tsx`), wildcard
  уводит на «/»; i18n подчищен: `profile.title/subtitle/account.*`, `auth.signedIn`, `nav.profile` удалены;
  `profile.password.*`/`profile.errors.*`/`profile.passwordChanged` живы — их использует форма (диалог топбара
  + экран форс-смены MLC-059). **Сайдбар (§3.5):** Обзор (вне групп) · Мониторинг (Сеансы, Быстродействие,
  Отчёты) · Управление (Клиенты, Базы) · Система (Аудит, Пользователи†, Параметры†) — †только Admin; итого
  8 пунктов. `nav.groups` operations/configuration → monitoring/management; пункт и заголовок дашборда
  выровнены на «Обзор» (рассинхрон «меню Обзор / страница Главная» всплыл на live-прогоне; только i18n
  `dashboard.title`, контент дашборда — объём `MLC-085`). **Влитая `MLC-079`:** union `AuditActionType` +5
  `User*` (слоты 103–107, переименование MLC-060/061), фильтр `AUDIT_ACTION_TYPES` предлагает все пять;
  `ru.json` — +5 ключей `User*` в терминологии раздела «Пользователи» («Учётная запись создана/отключена/
  включена», «Пароль сброшен», «Роль изменена»), 4 исторических `Admin(Created|Disabled|PasswordReset|Enabled)`
  удалены — int-строки 103–106 рендерятся новыми именами; **тест-зеркало** `auditActionTypes.test.ts`:
  `Record<AuditActionType, true>` (TS-исчерпываемость против union) ↔ ключи `audit.actions` сверяются в обе
  стороны (действие без перевода и осиротевший перевод валят тест) + все 5 `User*` в фильтре + фильтр без
  дубликатов. **Проверка:** FE 329 (+3) / type-check / lint зелёные. **Live-прогон на dev-стенде (2026-06-10,
  обе роли):** Admin — дропдаун «admin / Администратор», смена пароля диалогом прогнана туда-обратно
  (пароль admin возвращён исходный, контрольный login 200 — НЕ сброшен через reset-admin); `/profile` → «/»;
  /audit — фильтр со всеми пятью `User*` по-русски (38 опций + «Любое действие»), историческая строка
  `UserCreated` рендерится «Учётная запись создана» (создание `e2e-viewer`), URL-state `?actionType=UserCreated`
  работает, сырых `audit.actions.*` нет. Viewer — для проверки создан через штатный UI отдельный
  `ux-viewer-check` (роль Viewer), экран форс-смены MLC-059 отработал (то же переиспользование
  `ChangePasswordForm`); сайдбар Viewer без админ-пунктов («Система» = только Аудит), дропдаун
  «Наблюдатель» + «Смена пароля» доступна, `/settings` → «/»; после прогона `ux-viewer-check` **отключён**
  через UI («Отключить» + подтверждение). Консоль чистая (0 warn/error). **Канон 05/06 НЕ правился**
  (решение куратора — финальный док-PR `MLC-086`); расхождение (05/06: пункт меню и страница «Профиль»,
  группы сайдбара «Операции/Конфигурация/Система», 11 пунктов) — строкой в накопитель `[Doc divergence]` трека.

- `MLC-085` (UX-E) — **Дашборд-обзор: кликабельные KPI, строка здоровья хоста, ссылки в топе клиентов,
  grouped-режим /infobases снят** (аудит §3.2, §3.4; пятая задача трека; объединённая сессия с `MLC-086`,
  два PR раздельно — это PR №1, код) — Done (2026-06-10). Frontend-only, коммиты `MLC-085: …`.
  **KPI:** сетка 6 карточек → ряд из 5 кликабельных KPI (`lg:grid-cols-5`), каждая — `Link` вокруг `Card`
  (hover `bg-muted/50` + focus-ring): «Клиенты» → `/tenants`, «Инфобазы» → `/infobases`, «Активные
  сеансы» → `/sessions`, «Использовано лицензий» и «Свободно лицензий» (производная от той же отчётной
  оси) → `/reports`. **Строка системы:** RAS-карточка вынесена из KPI-сетки в отдельный ряд
  (`lg:grid-cols-4`): «Статус RAS» (1 кол, как была, не ссылка) + новая **`HostHealthCard`** (3 кол) —
  упрощённые гейджи CPU/RAM/диск на переиспользованных `MetricGauge`/`thresholds` (пороги-светофор ADR-26
  как на /performance, детальные подписи опущены — карточка отвечает «есть ли проблема»); вся карточка —
  ссылка на `/performance` («какая и из-за кого» — там; атрибуция по семьям на дашборд не тянулась,
  граница аудита §3.4); ошибка запроса — мягкий текст `dashboard.host.error`, дашборд не ломается.
  **`useDashboardHostHealth`:** тот же `GET /performance/host` + `hostMetricsSnapshotSchema` (граница
  MLC-016), `queryKey` общий с `useHostMetrics` (страницы не смонтированы одновременно; при переходе
  дашборд ↔ /performance кэшированный снимок виден сразу), `refetchInterval` =
  `DASHBOARD_HOST_REFETCH_MS` = **45 000 мс** (решение пользователя: 30–60 с, НЕ 5-секундный live) —
  тест фиксирует диапазон 30–60 с; `measuring=true` первой пробы → «измеряю…» на CPU/диске, RAM мгновенна
  (семантика 1:1 с `SaturationGauges`). **Топ клиентов:** имя — `Link` на `/tenants/:id`. Сабтайтл
  дашборда переписан под обзор («Клиенты, лицензии и здоровье хоста — с переходами в разделы») — хвост
  MLC-084. **/infobases:** режим «По клиенту» удалён (`grouping.ts` + тест удалены, переключатель
  «Список/По клиенту» снят — сгруппированный взгляд = `/tenants/:id`); фильтр по клиенту переехал в URL
  (`?tenantId=`, `useSearchParams` + replace) — на отфильтрованный список можно ссылаться извне, сброс
  чистит URL. **Клиенты:** колонка «Базы» — ссылка на `/infobases?tenantId={id}`. **i18n:** +
  `dashboard.host.title/error`, обновлён `dashboard.subtitle`, удалены `infobases.view.flat/grouped`.
  **Zod-граница (ограничение трека):** новых эндпоинтов нет; в `HostMetricsSnapshot` все поля —
  value-типы (nullable нет, опускать при `WhenWritingNull` нечего) — допущение зафиксировано
  схема-тестами на wire-payload'ах (первая проба `measuring=true` с нулями/пустыми группами + прогретый
  снимок): появится nullable-поле в DTO → parse упадёт → `omittable()` (урок MLC-067/071). **Тесты
  (334 FE, 62 файла):** `DashboardPage.test.tsx` (href'ы всех 5 KPI, host-карточки → /performance,
  топ-клиента → /tenants/:id; «измеряю…» ровно на 2 гейджах при `measuring=true`, нулей нет) +
  `useDashboardHostHealth.test.tsx` (каденция в диапазоне, схема в запросе, 2 wire-parse); прогон
  `pnpm test` / `type-check` / `lint` зелёный. **Live-прогон на dev-стенде (2026-06-10, обе роли):**
  Admin — «измеряю…» на свежеподнятом backend воспроизведён дважды (CPU/диск пульсируют, RAM честно
  66 % сразу), следующий опрос сменил на значения (CPU 9 %, диск 0 мс); каденция по network: 1 запрос
  `/performance/host` на ~10 запросов `dashboard/summary` (5 с) ≈ 45–50 с — НЕ 5-секундная; клики всех
  4 KPI прожаты живьём; колонка «Базы» у «E2E Клиент» → `/infobases?tenantId=…` (3 строки, фильтр
  предзаполнен, «Сбросить» возвращает полный список и чистит URL); переключателя вида на /infobases нет;
  консоль чистая. Топ клиентов на стенде пуст (0 активных сеансов) — ссылка закреплена компонентным
  тестом. Viewer — `ux-viewer-check` включён на прогон и **после прогона снова отключён**; под ним
  `dashboard/summary` / `performance/host` / `infobases` / `tenants` — 200 (новые элементы дашборда
  ролевых ветвлений не имеют, видимы обеим ролям; admin-гейты страниц не менялись). Backend для прогона
  поднимался без elevation (окна штатного стенда оказались закрыты) — статусы публикаций могли
  отметиться «Ошибка проверки» (гоча CLAUDE.md), не регресс, самочинится elevated-перезапуском стенда.
  **API/БД/миграции не тронуты** (`GET /performance/host` использован как есть). **Канон 05/06 НЕ
  правился** — расхождение (дашборд без переходов и без здоровья хоста, grouped-режим инфобаз) дописано
  строкой в накопитель `[Doc divergence]` трека; накопитель целиком закрывает `MLC-086` (PR №2).

- `MLC-086` (UX-F) — **Финальный док-PR: канон 05/06 переписан под новый UI, накопитель
  `[Doc divergence]` трека закрыт** (шестая, последняя задача этапа 1; объединённая сессия с `MLC-085`,
  отдельный PR №2 после вливания PR №1) — Done (2026-06-10). Docs-only, коммиты `MLC-086: …`;
  источник содержания — накопитель (5 строк `MLC-081..085`) + живой код/UI. Канон правлен present-tense,
  без changelog-хвостов (историю несёт git). **`05_UI_REQUIREMENTS.md`:** §2 — добавлен абзац
  **user-меню топбара** (логин + роль — единственное место, где пользователь видит свою роль; «Смена
  пароля» диалогом на той же `ChangePasswordForm`, что и форс-смена; «Выйти»; страницы/маршрута
  /profile нет) — закрывает строку `MLC-084`; §3.1 «Main Dashboard» → **«Обзор»**: 5 кликабельных KPI
  с адресами переходов, ряд системы «RAS + host-health card» (упрощённые `MetricGauge`, опрос 45 с
  против 5-секундного live, общий React-Query-ключ с /performance, «измеряю…» на первой пробе,
  граница «есть ли проблема» ↔ «какая и из-за кого»), ссылки в топе клиентов — закрывает строку
  `MLC-085` (дашборд); §3.2 — счётчик «Базы» = ссылка на `/infobases?tenantId=…`; §3.3 «Infobases &
  Publications» → страница **«Базы»**: вводный абзац о слиянии (публикация 1:1 с базой, отдельной
  страницы «Публикации» нет), вкладки «Базы» | «IIS», состав колонок (Публикация-статус + иконка
  источника · Версия платформы · Проверено), flat-only таблица с URL-фильтром `?tenantId=`
  (grouped-режима нет — паспорт клиента `/tenants/:id`), публикационные операции в меню строки/bulk-баре
  («Источник»-колонка → иконка в «Публикации»), select-all = текущая страница (выбор переживает
  листание), IIS-карточка — вкладкой, форма из трёх полей **без поля сервера** (значение скрыто:
  создание — из настройки, правка — текущее; пустая настройка блокирует submit toast'ом), пикер
  «Имя БД» работает сразу, «Дополнительно» = две группы («Инфобаза» + «Публикация в IIS», группы
  «СУБД» нет), раскладка /settings = фактическая (…«SQL Server» — единственное место правды
  SQL-инстанса с discovery-пикером, ключ `Defaults.DatabaseServer` не переименован до этапа 2;
  «Публикации IIS» = `DefaultVrdRoot` + `DefaultSiteName`; секции «Значения по умолчанию» нет) —
  закрывает строки `MLC-081/082/083`; §3.6/§3.8 — группа сайдбара «Операции» → «Мониторинг».
  **`06_UI_DESIGN.md`:** §5 — сайдбар 8 пунктов (Обзор вне групп · Мониторинг · Управление · Система,
  без «Профиль»; user-меню в топбаре); §6 — вводный список таблиц без «Publications»/«future
  Administrators» (Sessions, Audit, Clients, «Базы», Users), правило URL-фильтров уточнено
  (две сериализации в URL: `?tenantId=` на «Базах» и `?actionType=` в аудите; прочие фильтры — только
  component state), строка про «По клиенту»-группировку снята; §2 (таблица стека) и §3 — дашборд
  рендерит KPI и host-гейджи через `Card`+`Progress`/`MetricGauge` (никаких чартов), host-карточка
  переиспользует пороги и «измеряю…»-семантику гейджей — «same resource, same colour, both screens».
  **Реестр:** накопитель `[Doc divergence]` закрыт — пять строк сняты, осталась пометка
  «закрыто `MLC-086` (2026-06-10)» со ссылкой на этот отчёт; NEXT TASK → «не назначена»
  (все задачи этапа 1 `MLC-081..086` выполнены; **секция трека НЕ перенесена в архив — закрытие
  трека делает куратор**). **Границы соблюдены:** `01/03/04` и `DECISIONS.md` не тронуты (этап 2,
  §9 аудита; single-host в ADR не фиксируется), бек-хвосты §7 не тронуты. Сознательно оставлены
  вне объёма (не из накопителя, до-трековые мелкие неточности): «~15s» в подаче поллинга сеансов
  (05 §2 / 06 §1) и словарная строка «Отключить администратора» в 06 §12 — кандидаты на этап 2 /
  холистик, молча не правились. **Проверка:** pnpm-прогона нет (docs-only); вычитка диффа канона
  на соответствие живому UI (структура меню/страниц сверена с фактическим рендером стенда из
  live-прогонов `MLC-083/084/085`).

## Трек «UX-пересборка панели под single-host» — секция реестра (закрыт 2026-06-10, перенесено из PROJECT_BACKLOG.md)

**Вводная.** Фактическая топология — один сервер 1С + один SQL Server на хосте панели; интерфейс
пересобирается и упрощается (решение пользователя 2026-06-10: все прочие задачи сняты с повестки
под этот трек). UX-аудит выполнен и согласован: `.claude/plans/ux-audit-single-host.md` (§3–5 —
предложения и решения пользователя, §7 — опись бек-хвостов, §10 — черновик нарезки). Этап 1 —
**только frontend**; этап 2 (бек-чистка по описи §7 + ADR «Single-host topology») **не нарезан** —
gated на «пользователь пожил с новым UI и подтвердил single-host окончательно». До этого момента
single-host в ADR не фиксируется (сохраняем возможность отката).

**Ограничения трека — входят в каждую постановку:**
- Каждая задача оставляет панель полностью рабочей (порядок A→E выбран под это).
- Никаких изменений API/БД/миграций; бек-хвосты §7 не трогать даже «по мелочи»
  (единственное исключение — тестовые таймауты в составе `MLC-081`: тесты не API/БД).
- Обе роли (Admin/Viewer) проверяются в каждой задаче.
- Развилки /settings НЕ пересматривать молча: пер-контрольное сохранение без общей «Применить»;
  поле status в форме остаётся; `OneC.Cluster.Server` скрыт; SQL-discovery только localhost.
- Новые данные на FE — Zod `.nullish()`/omittable + схема-тест на omit-null ответе (урок MLC-067/071).

**Канон во время трека (решение куратора 2026-06-10):** 05/06 в каждом PR **не** правятся — канон
обновляется одним финальным док-PR (`MLC-086`). Обоснование: структура 05/06 переписывается целиком,
пер-PR правки перелопачивали бы одни и те же секции пять раз, а возможность отката делает ранние
правки канона потенциально выброшенными. Исполнителю разрешено отступать от канона, где тот мешает
решению; каждая задача при Done добавляет строку в запись ниже.

**[Doc divergence] UX-трек vs канон 05/06** — **закрыто `MLC-086` (2026-06-10)**: канон 05/06
переписан present-tense под фактический UI, все пять накопленных строк (`MLC-081`…`MLC-085`)
сняты как устранённые; содержание перечня — в отчёте `MLC-086` в архиве.

**Темп исполнения (решение пользователя + куратора 2026-06-10):** остаток трека — двумя
объединёнными сессиями вместо четырёх: сессия 1 = `MLC-083`+`MLC-084` (один PR, коммиты
раздельно по задачам), сессия 2 = `MLC-085`+`MLC-086` (два PR: код и канон раздельно —
канон правится после приёмки финального UI). Жизненный цикл реестра — по-прежнему **на задачу**:
отдельный статус Done, отдельный полный отчёт в архив, отдельная Done-строка и строка дивергенций.

**Задачи трека (исполнять по порядку):**

- `MLC-081` (UX-A) · Frontend · M · **Done (2026-06-10)** — **«Базы» = Инфобазы + Публикации**: `/infobases`
  («Базы», вкладки «Базы» | «IIS») несёт публикационные колонки/действия/bulk, `/publications` удалена;
  +`MLC-080` (запас тайминг-ожиданий бек-тестов 5с→30с) отдельным коммитом. Гочи следующим задачам:
  bulk-выбор хранит объекты `Map<pubId, PublicationListItem>` (серверная пагинация); мутации публикаций/IIS
  инвалидируют `["infobases"]` (ключ `["publications"]` удалён); `InfobaseRow` общий с `/tenants/:id` —
  публикационные пропсы опциональны; колонку «Сервер БД» снимает `MLC-082`; grouped-режим жив до `MLC-085`;
  фильтр по статусу публикации не переехал (нужен API-фильтр — этап 2, кандидат у куратора).
  Полный отчёт — в архиве.
- `MLC-082` (UX-B) · Frontend · S · **Done (2026-06-10)** — **форма инфобазы без выбора сервера** (§4.1–4.2):
  поле «Сервер БД» и колонка таблицы сняты; `databaseServer` — скрытое поле формы (создание — из
  `Defaults.DatabaseServer`, правка — текущее значение базы, молчаливой миграции сервера нет), пикер
  «Имя БД» работает сразу; API не менялся, валидация не тронута. Гочи следующим: пустая настройка →
  submit блокируется toast'ом «задайте в Параметрах» — `MLC-083` делает её «единственным местом правды»;
  discovery `sql-instances` из формы ушёл, остался только на /settings. Полный отчёт — в архиве.
- `MLC-083` (UX-C) · Frontend · XS–S · **Done (2026-06-10)** — **/settings: секция «SQL Server»,
  «Значения по умолчанию» расформирована** (§4.3, §5): `Defaults.DatabaseServer` — своя секция с подписью
  «SQL-инстанс, на котором живут базы клиентов» (место правды, не «дефолт»), discovery-пикер цел;
  `DefaultSiteName` → «Публикации IIS» (+описание секции); порядок секций = §5. Гочи следующим: ключи
  настроек НЕ переименованы (бек-хвост §7 п.4); правка — только `SECTIONS` в `SettingsPage.tsx` + `ru.json`
  (label/hint по ключу настройки). Полный отчёт — в архиве.
- `MLC-084` (UX-D) · Frontend · S · **Done (2026-06-10)** — **профиль в топбар + сайдбар 8 пунктов;
  + влитая `MLC-079`** (§3.3, §3.5): user-меню топбара = логин + роль (словарь `users.roles`) + «Смена
  пароля» диалогом (`ChangePasswordForm` как есть) + «Выйти»; `/profile` удалён (wildcard → «/»); сайдбар:
  Обзор (вне групп) · Мониторинг · Управление · Система. Аудит: union/фильтр +5 `User*` (103–107),
  `Admin*`-ключи 103–106 удалены из i18n, тест-зеркало `auditActionTypes.test.ts` (union ↔ `audit.actions`
  в обе стороны). Гочи следующим (`MLC-085/086`): пункт и заголовок дашборда теперь «Обзор» (i18n
  `nav.dashboard`/`dashboard.title`; сабтайтл дашборда не трогали — объём 085); `profile.password.*`/
  `profile.errors.*` живы — их делят диалог топбара и экран форс-смены. Полный отчёт — в архиве.
- `MLC-085` (UX-E) · Frontend · S · **Done (2026-06-10)** — **дашборд-обзор** (§3.2, §3.4): 5 KPI —
  кликабельные карточки (Клиенты/Инфобазы/Сеансы → свои разделы, обе лицензионные → `/reports`); ряд
  системы = «Статус RAS» + `HostHealthCard` (CPU/RAM/диск на переиспользованных `MetricGauge`/`thresholds`,
  клик → `/performance`, опрос 45 с — `DASHBOARD_HOST_REFETCH_MS`, queryKey общий с `useHostMetrics`,
  «измеряю» на первой пробе); имена в топе клиентов → `/tenants/:id`. Grouped-режим `/infobases` удалён
  (`grouping.ts` снят), фильтр клиента — в URL `?tenantId=`; колонка «Базы» у клиентов — ссылка на
  отфильтрованный список. Гочи `MLC-086`: сабтайтл дашборда новый; `infobases.view.*` из i18n удалены;
  host-снимок без nullable-полей — wire-формы зафиксированы схема-тестами. Полный отчёт — в архиве.
- `MLC-086` (UX-F) · Docs · S · **Done (2026-06-10)** — **финальный док-PR**: 05/06 переписаны
  present-tense под новый UI — 05 §3.1 «Обзор» (кликабельные KPI + здоровье хоста), §3.3 «Базы»
  (вкладки, объединённая таблица, форма без сервера, /settings с секцией «SQL Server»), §2 user-меню
  топбара, группы «Мониторинг» в §3.6/3.8; 06 §5 сайдбар 8 пунктов, §6 (URL-фильтры `?tenantId=` /
  `?actionType=`, без grouped-строки), §2/§3 — `MetricGauge` на дашборде. Накопитель `[Doc divergence]`
  закрыт (строки сняты, пометка осталась). `01/03/04` и `DECISIONS.md` не тронуты (этап 2, §9 аудита).
  Полный отчёт — в архиве.

**Кандидаты, найденные по ходу трека** (переходят в нарезку этапа 2):
- (с `MLC-081`) Вернуть фильтр «статус публикации» на объединённой странице «Базы»: бывший клиентский
  фильтр `/publications` не переехал — список серверно пагинирован, честный фильтр требует параметра
  в `GET /api/v1/infobases` (API — этап 2). Ценность: быстрый поиск «всё, что не Published».
  **Триаж куратора (2026-06-10): принят как кандидат этапа 2** — номер получит при нарезке этапа 2
  вместе с описью §7.
- (с `MLC-086`) Две до-трековые мелочи канона, сознательно не правленные в объёме 086: «~15s» в подаче
  поллинга сеансов (05 §2 / 06 §1) и словарная строка «Отключить администратора» в 06 §12 — кандидаты
  на этап 2 / UI-холистик.

**Решения куратора по отложенным (2026-06-10):** `MLC-073` (полировка «Быстродействия») и
`MLC-062` (движок таблиц `@tanstack/react-table`) в трек **не вошли** — остаются в «Открытых
опциях» по своим триггерам: аудит §3.6 явно выводит «Быстродействие» из объёма трека, а смена
движка таблиц умножила бы риск и ревью самой крупной задачи UX-A.

## Триаж отложенных опций 2026-06-10 (куратор, после закрытия этапа 1 UX-трека)

Решение пользователя: «то, что надо закрыть, — закрыть, от лишнего избавиться, нужное оставить».
Принцип триажа: реестр не дублирует канон и архив; опция остаётся в реестре, только если у неё
есть конкретный измеримый триггер и содержание, которого нет в других документах.

**Сняты с реестра:**
- `MLC-006(a)` — строка-указатель «промоутнута в MLC-025» удалена: факт поглощения зафиксирован
  в самой записи `MLC-025` и в архиве; отдельная строка — мусор.
- `MLC-011(a)` (use-cases в Application) — решение целиком несёт **ADR-20** (anti-corruption
  граница; use-case-слой вводится при появлении второго потребителя правил). Дублирование в
  реестре нарушало правило «канон не дублировать». Возврат — правкой ADR-20 при триггере
  (второй транспорт gRPC/CLI/mass-import или worker вне HTTP).
- `MLC-028` (подготовка к multi-cluster/multi-node, XL) — направление прямо противоположно
  подтверждаемому курсу single-host (UX-трек этап 1 выполнен, этап 2 gated на окончательное
  подтверждение). Single-node — locked operational constraint (`DECISIONS.md` «Deployment
  topology»), пункт «Multi-cluster / multi-node» уже есть в `ROADMAP.md` «Backlog / deferred».
  Смена топологии в любом случае = новый трек с re-review каждого адаптера, а не «взять опцию».

**Оставлены в реестре («Открытые опции»):** `MLC-025` (codegen/Zod — измеримый триггер),
`MLC-026` (генерация FE-полей настроек — триггер по размеру каталога), `MLC-027` (i18n
namespaces — триггер по размеру файла), `MLC-073` (полировка «Быстродействия» — дешёвая,
конкретная, ценность понятна), `MLC-062` (движок таблиц — ядро будущего UI-холистик-трека,
постановка в архиве). **Живут в ROADMAP, не в реестре:** RAS Strategy B (`MLC-036`),
multi-node, UI-долги канона 06 (tanstack/recharts/ESLint-StatusBadge), PERF-08+ (индекс
перф-трека). **Этап 2 UX-трека** — gated, добавлен в ROADMAP «Backlog / deferred».

---

## Трек «UX-пересборка, этап 2: single-host бек-чистка» — отчёты задач

- `MLC-087` (ST-A) — **SQL-инстанс: настройка `Sql.Server` — единственный источник** (опись §7
  п.2/4/5) — Done (2026-06-10). Сессия 1 этапа 2 (объединённая с `MLC-088`, один PR, коммиты
  раздельно — это коммит №1). **Переименование ключа `Defaults.DatabaseServer` → `Sql.Server`:**
  `SettingKey.SqlServer` + запись каталога `SettingDefinitions` (whitelist; описание «SQL-инстанс,
  на котором живут базы клиентов»; без сидируемого дефолта — зависит от инсталляции, паттерн
  `OneC.RAS.ExePath`); сидер (идемпотентный) на свежей БД создаёт строку под новым ключом.
  **Миграция значения** `20260610173315_MLC087RenameSqlServerSetting` — data-only (структура
  таблицы `Settings` не менялась → EF сгенерил пустой Up/Down, наполнен вручную): `UPDATE dbo.Settings
  SET [Key]='Sql.Server', [Description]=… WHERE [Key]='Defaults.DatabaseServer'` — **значение
  сохраняется** (не пересоздание); reversible (Down — симметричный UPDATE назад); на свежей БД
  затрагивает 0 строк (сеется позже под новым ключом) — корректно. Файлы миграции нормализованы
  (UTF-8 без BOM + LF; снапшот после нормализации идентичен HEAD — модель не менялась). **`GET
  /discovery/databases`:** убран query-параметр `server` — сервер берётся из настройки `Sql.Server`
  на бекенде через `ISettingsSnapshot`; пустая настройка → `Available:false` + текст «Сервер СУБД не
  задан. Укажите его в разделе „Параметры“» (форма уходит в ручной ввод имени БД). **Контракт
  сужен:** `ISqlDatabaseDiscovery.ListDatabasesAsync(CancellationToken)` — без параметра сервера;
  реализация `SqlDatabaseDiscovery` инжектит `ISettingsSnapshot`, читает `Sql.Server`,
  `ArgumentException.ThrowIfNullOrWhiteSpace` как defense-in-depth (эндпоинт гейтит до вызова).
  Сервер-резолюция read'ится в двух местах (эндпоинт — для точного сообщения-гейта; сервис — для
  использования) — кэшированный снапшот, дубль-чтение ничтожно, каждый слой самодостаточен. `GET
  /discovery/sql-instances` и `ISqlInstanceDiscovery.FindLocalInstances()` (у него параметра сервера
  и не было — localhost-only) **не тронуты** — кормят пикер на /settings. **FE:** `useDatabases(enabled)`
  без `server`/`?server=` (queryKey `["discovery","databases"]`); `useInfobaseForm` — `useDatabases(open)`,
  `watchedDatabaseServer` снят из watch (скрытое поле `databaseServer` ещё живо — убирает `MLC-088`),
  prefill читает `Sql.Server`; `InfobaseFormDialog` — снят `disabledHint` поля «Имя БД» (всегда
  доступно, бекенд решает по `Available`); `/settings` секция «SQL Server» — `SECTIONS`/`FIELD_META`/
  спец-рендер `DatabaseServerField` и i18n `labels`/`hints` перевешены на ключ `Sql.Server` (пикер
  локальных SQL-инстансов цел). **Тесты:** `DiscoveryEndpointsTests` — мок `ISettingsSnapshot` отдаёт
  `Sql.Server`; пустая настройка → `Available:false` без вызова discovery; cancellation пробрасывается;
  `ListDatabasesAsync(ct)` без сервера. `DefaultPrefillCatalogTests` — `SettingKey.SqlServer` в
  каталоге, non-secret text, без дефолта. FE `useDiscovery.test` — фетч без `?server=`. `dotnet test`
  583 зелёных, FE `type-check`/`lint`/`test` (333) зелёные; `db-reset` — миграции накатываются чисто
  (включая `MLC087`). **Скрытое поле `databaseServer` формы и контракт API на этом шаге сохранены**
  (объём `MLC-088`). Канон не трогался (этап 2 → финальный док-PR `MLC-091`).

- `MLC-088` (ST-B) — **колонка `Infobase.DatabaseServer` удалена** (опись §7 п.1, решение куратора
  «чисто») — Done (2026-06-10). Сессия 1 этапа 2 (коммит №2, опирается на ключ `Sql.Server` из
  `MLC-087`). **Дроп колонки:** доменная сущность `Infobase` без `DatabaseServer`; EF-конфиг
  (`AppDbContext`) — снято `Property(x => x.DatabaseServer)` для Infobase (у `DatabaseBackup` —
  оставлено). Миграция `20260610175039_MLC088DropInfobaseDatabaseServer` (EF-сгенерённая,
  нормализована BOM/LF): Up `DropColumn`, Down `AddColumn nvarchar(200) NOT NULL DEFAULT ''`
  (значения во всех строках одинаковы и восстановимы из настройки `Sql.Server` — потери нет, см.
  решение куратора). **Хвост-grep `Infobase.DatabaseServer` по бекенду → единственный читатель
  колонки:** `BackupsEndpoints.StartAsync` (постановка бэкапа в очередь) — переключён с
  `infobase.DatabaseServer` на `settings.GetString(SettingKey.SqlServer)`; пустая настройка → новый
  409 `SQL_SERVER_NOT_CONFIGURED` (`ProblemCodes` + `Problems.SqlServerNotConfigured`, по образцу
  `BackupFolderNotConfigured`) до постановки. **Что НЕ тронуто (перечень из отчёта):**
  `DatabaseBackup.DatabaseServer` — это **отдельная** колонка (снимок сервера на записи бэкапа,
  используется в running-замке/retention/отображении), живёт самостоятельно; публикации/webinst
  адресуют **кластер 1С** (`OneC.Cluster.Server`/`RAS.Endpoint`), не SQL-сервер; отчёты (`/reports`)
  и перф-проба (`SqlPerformanceProbe`) колонку инфобазы не читали (перф/бэкап коннектятся через
  `ConnectionStrings:Default`). **Контракты:** `databaseServer` убран из `InfobaseResponse`/
  `InfobaseListItemResponse`/`CreateInfobaseRequest`/`UpdateInfobaseRequest` + маппинга `ToResponse`
  + проекции списка + create/update entity-присвоений + хелпера `ValidateInfobase`. **Валидация —
  обе стороны:** `InfobaseValidationRules.DatabaseServerMaxLength` снят ↔ FE `validation.ts`
  (`DATABASE_SERVER_MAX_LENGTH` + zod-поле `databaseServer`) снят; parity-тесты (`InfobasesValidationTests`
  ↔ `validation.test.ts`) обновлены. **FE-форма:** скрытое поле `databaseServer` убрано целиком из
  `useInfobaseForm` (defaultValues create+edit, settings-эффект, оба submit-input'а, `defaultDatabaseServer`),
  **toast-гейт «Сервер СУБД не задан» удалён** — создание базы больше не шлёт сервер. **Где теперь
  живёт сообщение о пустой настройке** (переосмыслено per постановка): на **discovery имён БД**
  (`Available:false` + текст из `MLC-087`, форма показывает ручной ввод) и на **бэкапе** (409 на BE);
  в форме сообщения нет — поля сервера нет. `types.ts` (`infobaseSchema`/`CreateInfobaseInput`/
  `UpdateInfobaseInput`), `BackupsDialog` (подзаголовок без `{{server}}` — сервер один на панель,
  per-base показ — рудимент мульти-сервера) + i18n; орфанные i18n-ключи (`databaseServerRequired`/
  `databaseServerNotConfigured`/`discovery.databaseServerMissing`) удалены. Деталь из `MLC-082` «правка
  не мигрирует сервер базы молча» — **вопрос исчез** (колонки нет, нечего мигрировать). **Тесты:**
  perf-harness seed + ~15 тестовых фикстур `Infobase`/request очищены от `DatabaseServer` (бэкап-фикстуры
  `DatabaseBackup` не тронуты); `BackupsEndpointsTests` — `ConfiguredSettings` отдаёт `Sql.Server="SQL01"`,
  оркестратор-стабы матчат значение настройки, +тест «пустой Sql.Server → 409 SQL_SERVER_NOT_CONFIGURED».
  `dotnet test` 584 зелёных, FE `type-check`/`lint`/`test` (333) зелёные; `db-reset` — обе миграции
  (`MLC087`+`MLC088`) накатываются чисто. **Хвост-тест полноты** (`DatabaseServer` по `backend/src`
  вне миграций): остались только легитимные `DatabaseBackup.*` (entity/контракт/оркестратор/retention/
  AppDbContext-индекс) — ноль `Infobase.DatabaseServer`. Канон/ADR не трогались (финальный док-PR `MLC-091`).

- **Live-прогон на стенде `MLC-087`+`MLC-088`** (2026-06-10, реальный SQL `Server=.`, sysadmin
  подтверждён; БД стенда + DPAPI key ring забэкаплены в `F:\MlcStage2Backups\` ПЕРЕД прогоном —
  recovery-point; страховочный тег `v1-pre-singlehost-stage2`). **NB:** прежняя БД стенда была
  пересоздана `db-reset`'ом ещё на шаге «чистота миграций» (до бэкапа) — это штатный dev-DB, не
  прод; бэкап взят с текущего (мигрированного) состояния. **Миграция значения ключа —
  доказана replay'ем** (роллбэк до `MLC076` → `UPDATE Defaults.DatabaseServer='localhost\REPLAYTEST'`
  → накат `MLC087` → `Sql.Server`='localhost\REPLAYTEST', значение цело, старый ключ исчез →
  накат `MLC088`, колонка `Infobases.DatabaseServer` дропнута). **API e2e (реальный backend+SQL):**
  логин admin; `GET /settings` — `Sql.Server` есть, `Defaults.DatabaseServer` нет; `GET
  /discovery/databases` **без `server=`** → `Available:true`, список реальных БД (`MlcE2e_Alpha/Beta/
  Gamma`+`bd1/mitpro/test`); создание базы POST **без поля `databaseServer`** → 201, в ответе и в
  списке поля нет; правка (PUT, статус→Maintenance) — ок; бэкап `MlcE2e_Alpha` через UI-API → реальный
  `.bak` 119.3 МБ на диске, статус Succeeded, в записи `server=localhost` (из настройки); пустой
  `Sql.Server` → бэкап **409 `SQL_SERVER_NOT_CONFIGURED`** и `discovery/databases` `Available:false`
  с подсказкой; **Viewer** (`stage2-viewer`): список без `databaseServer`, бэкап разрешён (ADR-27,
  `requestedBy=stage2-viewer`), создание базы и правка настроек — 403. **UI (preview, Vite):** `/settings`
  секция «SQL Server» рендерится с новой подписью + значением `localhost` + discovery-пикером; форма
  «Новая инфобаза» — **без поля сервера**, пикер «Имя базы данных (SQL Server)» сразу отдаёт реальные БД
  (подпись «сервер задаётся в „Параметрах"»); сеть: `GET /api/v1/discovery/databases` **без** `?server=`;
  **консоль чистая** (0 warn/error). **Не прогнано и почему:** реальная webinst-публикация и проверка
  публикации в IIS — статус «Ошибка проверки» (на стенде не задан `OneC.RAS.ExePath` и backend не
  элевирован под IIS); это **не относится к `MLC-087/088`** (публикация адресует кластер `OneC.*`, не
  SQL-сервер; RAS/webinst в этой сессии не менялись — `OneC.Cluster.Server` снимает `MLC-089`).
  **E2e-артефакты стенда** (тенант «Stage2 E2E», инфобаза «Stage2 Alpha», `stage2-viewer`, бэкапы в
  `F:\MlcStage2Backups`, `Sql.Server=localhost`, `Backup.FolderPath`) оставлены для проверки куратором.

- `MLC-089` (ST-C) — **удалить ключ `OneC.Cluster.Server`** (опись §7 п.3) — Done (2026-06-10).
  Сессия 2 этапа 2 (PR кода, коммит №1). **Вводная single-host:** кластер 1С и RAS живут на одном
  хосте, поэтому отдельный ключ адреса кластера для строки соединения webinst избыточен (`MLC-088`
  уже отметил, что `ResolveClusterServer` берёт host из `RAS.Endpoint` при пустом
  `OneC.Cluster.Server` — здесь снимается сам ключ). **Снято:** `SettingKey.OneCClusterServer` (const)
  + запись каталога `SettingDefinitions` (whitelist) — ключ был и так скрыт из UI, FE не трогался.
  **`ResolveClusterServer` (`Infrastructure/Publishing/WebinstArgs.cs`):** сигнатура сужена с
  `(clusterServerSetting, rasEndpoint)` до `(rasEndpoint)` — деривирует host из `OneC.RAS.Endpoint`
  (`host:port` → `host`, порт RAS для строки соединения с кластером не подходит), бросает
  `InvalidOperationException` при пустом RAS; fallback-логика «явная настройка vs RAS» удалена.
  Вызов в `OneCWebinstPublisher.PublishAsync` — без чтения `SettingKey.OneCClusterServer`. **Миграция**
  `20260610191046_MLC089DropOneCClusterServerSetting` (структура `Settings` не менялась → EF сгенерил
  пустой Up/Down, наполнен вручную; нормализована UTF-8 без BOM + LF; снапшот модели после нормализации
  идентичен HEAD — модель не менялась): Up `DELETE FROM dbo.Settings WHERE [Key]='OneC.Cluster.Server'`
  (чистит осиротевшую row; на свежей БД 0 строк — корректно), Down **roll-forward only** (`throw
  NotSupportedException`, по образцу `DropIisServiceAccountSetting` — ключ снят с каталога, его
  пересоздание не часть отката). **Юнит-тесты `WebinstArgsTests`** переведены на новую семантику:
  `ResolveClusterServer` берёт host из RAS (с портом/без), бросает при пустом/blank RAS; старые тесты
  «явная настройка приоритетнее» и `(null,null)` заменены. **Хвост-grep `Cluster.Server` по
  `backend/src`** (вне миграции) → остались только комментарии в `WebinstArgs.cs`/`WebinstArgsTests.cs`,
  ноль читателей ключа. **Live webinst НЕ прогнан** (как и в сессии 1, постановка это допускает):
  `OneC.RAS.ExePath` на стенде не задан, backend неэлевирован — покрыто юнитами. Канон/ADR не
  трогались (финальный док-PR `MLC-091`).

- `MLC-090` (ST-D) — **фильтр «статус публикации»** (принятый кандидат с `MLC-081`) — Done (2026-06-10).
  Сессия 2 этапа 2 (PR кода, коммит №2). **Backend:** параметр `publishStatus` в `GET /api/v1/infobases`
  — **server-side фильтр на пагинации** (коррелированный `EXISTS` по `LastCheckStatus` публикации,
  публикация 1:1 с инфобазой → счёт `Total` честный, фильтр до `Skip/Take`). Тип параметра — `string?`
  с **ручным парсом** (`Enum.TryParse` + `Enum.IsDefined`), как `actionType` на `/audit`: гоча CLAUDE.md
  — DataAnnotations в minimal API в runtime не валидируются, поэтому значение проверяется в эндпоинте,
  мусор/out-of-range → `ValidationProblem` 400. `ListAsync` поднят `private`→`internal` (тестируемость)
  + тип результата `Results<Ok<…>, ValidationProblem>`. **Swagger:** параметр виден как optional query
  string (то же лечение, что `actionType`; отдельного механизма per-param описаний в SwaggerGen
  проекта нет — XML/annotations не подключены, доп. инфра вне объёма трека). **FE:** UI-фильтр на
  странице «Базы» рядом с фильтром клиента; **решение по URL-состоянию — статус в URL** (`?publishStatus=`,
  как `?tenantId=`) для консистентности, deep-link и шаринга ссылкой; общий `changeFilterParam`
  сбрасывает на 1-ю страницу, кнопка «Сбросить» чистит оба фильтра. Опции в порядке «проблемные
  первыми» (Не опубликована / Ошибка проверки / Не проверялась / Опубликована) — ориентир ценности
  «быстро найти всё, что не Published». `useInfobases(tenantId, publishStatus, page, pageSize)` —
  параметр в query+queryKey (префикс `["infobases"]` цел, инвалидация всех фильтров); `TenantDetailPage`
  обновлён под новую сигнатуру (`null` под статусом). i18n: `infobases.filters.publishStatus`/
  `allStatuses`/`publishStatusOptions.*`. **Zod не тронут:** ответ API не меняется
  (`publication.lastCheckStatus` уже отдавался) — схема-тест на omit-null не требуется. **Тесты:**
  новый `InfobaseListFilterTests` (5: фильтр по статусу, без фильтра = все, композиция с `tenantId`,
  мусор+out-of-range → 400, case-insensitive). `dotnet test` **589** зелёных, FE
  `type-check`/`lint`/`test` (**333**) зелёные.

- **Live-прогон на стенде `MLC-089`+`MLC-090`** (2026-06-10, реальный SQL `Server=.`, БД
  `MitLicenseCenter`). **Бэкап ПЕРЕД миграцией** (recovery-point, поверх прошлого): БД
  `MitLicenseCenter_20260610_stage2.bak` (COPY_ONLY) + DPAPI key ring (`%LocalAppData%\MitLicenseCenter\
  keys`, 2 xml) → `F:\MlcStage2Backups\`. **Миграция `MLC089` накатана** на БД стенда (`ef database
  update`, единственная Pending; 087/088 уже стояли); проверка `dbo.Settings`: `OneC.Cluster.Server`
  — **0 строк** (удалён), `Sql.Server` — на месте. **MLC-090 API e2e (admin, реальный backend+SQL):**
  `GET /infobases` без фильтра → 2 базы; `?publishStatus=Published` → только `mitpro`,
  `?publishStatus=NotPublished` → только `Stage2 Alpha`, `Error`/`Unknown` → 0; невалидные
  `?publishStatus=Garbage` и `=99` → **HTTP 400**; композиция `tenantId`+`publishStatus` — покрыта
  юнитом. (Для наглядной дискриминации двум публикациям стенда временно проставлены разные статусы
  через SQL — `LastCheckStatus` производное, самовосстановимо ближайшей проверкой IIS.) **MLC-090 UI
  (Chrome, реальный SPA):** на «Базах» виден новый фильтр «Любой статус»; выбор «Опубликована» →
  в таблице только `mitpro` + URL `?publishStatus=Published`; deep-link `?publishStatus=NotPublished`
  → таблица = `Stage2 Alpha`, дропдаун гидратирован «Не опубликована», появилась кнопка «Сбросить».
  **Viewer:** фильтр-`Select` рендерится **вне** гейта `isAdmin`, эндпоинт списка авторизован для
  `Roles.Viewer` → путь роленезависим; отдельным логином Viewer не проверялся (пароль `stage2-viewer`
  в постановку сессии 2 не входил). **Live webinst (MLC-089) не прогонялся** — см. отчёт `MLC-089`
  (RAS не настроен/backend неэлевирован, постановка это допускает).

- `MLC-091` (ST-E) — **финальный док-PR этапа 2** (канон + ADR) — Done (2026-06-10). Сессия 2 этапа 2,
  **отдельный PR №2 после вливания PR кода** (squash `e0d7f7b`). Docs-only — кода не менял.
  **Новый ADR-28 «Single-host topology — one host, one 1C cluster, one SQL instance»** (DECISIONS.md,
  перед Locked Operational Constraints): фиксирует подтверждение пользователя 2026-06-10 и
  формализует прежний Locked-констрейнт «Deployment topology — single-node»; перечисляет вычищенные
  хвосты (§7 аудита) — `Sql.Server` единственный источник (колонка `Infobase.DatabaseServer` дропнута,
  discovery без `server=`), `OneC.Cluster.Server` снят (адрес кластера webinst из `RAS.Endpoint`-хоста);
  **Rejected** — держать generic мультисервер «на всякий»; **Locked** — любой возврат к мультихосту
  требует revoke ADR-28 + ре-ревью адаптеров. **Затронутые ADR (без переписывания истории решений):**
  ADR-17 (форма инфобазы) — добавлена **Update-нота**: поле SQL-сервера промотировано из UI-only
  prefill в runtime-resolved global (`Sql.Server`) — это ровно тот revision-trigger, что ADR-17 сам
  заложил («single-host подтверждён → revoke для поля»); ADR-28 supersedes ADR-17 для SQL-поля, два
  других prefill-ключа (`IIS.DefaultSiteName`/`OneC.DefaultPlatformVersion`) остаются UI-only.
  ADR-4 (публикации) — без правки тела, в ADR-28 отмечено, что изменился лишь источник `Srvr=`
  (RAS-хост). **Канон present-tense:** `01_PROJECT_CONTEXT` — добавлен блок «Topology — single-host
  (ADR-28)»; `03_DOMAIN_MODEL` — поле `Infobase.DatabaseServer` убрано (отмечено: инстанс в
  `Sql.Server`, колонка дропнута MLC-088); `04_INFRASTRUCTURE` — webinst connstr из `RAS.Endpoint`,
  каталог §4 синхронизирован (`Defaults.DatabaseServer`→`Sql.Server`, строка `OneC.Cluster.Server`
  убрана → **24 ключа = `SettingDefinitions`**), абзац form-prefill переписан (Sql.Server —
  single source, два UI-only ключа, секции «Значения по умолчанию» нет), discovery-контракт
  (`databases` без `server=`, берёт `Sql.Server`); попутно `05_UI_REQUIREMENTS` — форма без
  SQL-поля + **фильтр статуса публикации MLC-090** в таблице «Базы», секция «SQL Server» на
  `Sql.Server`; `OPERATIONS` §публикации — адрес кластера из `RAS.Endpoint`. **[Doc divergence]
  `Performance.Recording*`** (04 §4) — подтверждён **закрытым**: три ключа присутствуют в каталоге
  (строки Recording*), каталог = `SettingDefinitions`. **Чистка Swagger/DataAnnotations discovery** —
  выполнена ещё в MLC-087 (`GetDatabasesAsync` без query-параметра, комментарии present-tense,
  DataAnnotations про server нет) → в MLC-091 правок не потребовала. **Две до-трековые мелочи:**
  поллинг сеансов «~15s»→«~5s» (05 §2 + 06 §1; факт `useSessionsSnapshot` `refetchInterval: 5_000`,
  MLC-044, согласован с hot ~4с); «Отключить администратора»→«Отключить пользователя» (06 §12 словарь
  + §7 пример — раздел переименован в «Пользователи», действие в коде `users.actions.disable`=«Отключить»).
  **ХВОСТ-ТЕСТ ПОЛНОТЫ** (`grep -E "DatabaseServer|Cluster.Server|sql-instances|server=" backend/src`,
  вне миграций): все остатки **легитимны** — (1) `DatabaseBackup.DatabaseServer` — снимок сервера на
  записи бэкапа, отдельная сущность (`BackupOrchestrator`, `BackupRetentionJob`, `AppDbContext`
  Property+индекс, `DatabaseBackup.cs`, `BackupsContracts`, `BackupsEndpoints`); (2) `sql-instances` —
  `DiscoveryEndpoints.GetSqlInstances` (пикер /settings); (3) `WebinstArgs.cs` — комментарий,
  поясняющий снятие ключа. **Ноль** `Infobase.DatabaseServer`, **ноль** использования ключа
  `OneC.Cluster.Server`, **ноль** `?server=` в discovery. **Секция трека в реестр-архив НЕ
  перенесена** (закрытие трека — за куратором, per постановка). `NEXT TASK` не переставлялся.

## Трек «UX-пересборка, этап 2: single-host бек-чистка» — секция реестра (закрыт 2026-06-10, перенесено из PROJECT_BACKLOG.md)

**Вводная.** Пользователь **подтвердил single-host окончательно (2026-06-10)** — триггер этапа 2
сработал. Объём: бек-хвосты по описи §7 аудита (`.claude/plans/ux-audit-single-host.md`) + ADR
«Single-host topology» + канон 01/03/04 + кандидаты из архивной секции этапа 1. Изменения API/БД
**разрешены** — строго в объёме описи, без попутных рефакторингов.

**Решения куратора по развилкам описи (2026-06-10):**
- §7 п.1 — колонку `Infobase.DatabaseServer` **убрать с миграцией** (чистый вариант: single-host
  подтверждён, значения колонки одинаковы и восстановимы из настройки).
- §7 п.4 — ключ `Defaults.DatabaseServer` **переименовать с миграцией значения** (например, в
  `Sql.Server`); настройка остаётся **единственным местом правды** — НЕ деривация из
  `ConnectionStrings:Default` (она убила бы секцию «SQL Server» на /settings из `MLC-083`).

**Ограничения трека — входили в каждую постановку:**
- Каждая задача оставляет панель полностью рабочей.
- API/БД меняются только в объёме описи §7 + кандидатов; новых фич нет (исключение — `MLC-090`,
  принятый кандидат).
- Правишь валидацию инфобаз — **обе стороны** (BE `InfobaseValidationRules.cs` ↔ FE
  `validation.ts`) + parity-тесты.
- После `dotnet ef migrations add` — нормализовать файлы миграции (UTF-8 без BOM + LF, гоча CLAUDE.md).
- Обе роли (Admin/Viewer) проверяются; live-прогон на стенде.
- Настройки — только через whitelist `SettingDefinitions` (+сидер/миграция при переименовании/удалении ключа).

**Темп исполнения:** две сессии — сессия 1 = `MLC-087`+`MLC-088` (один PR, коммиты раздельно),
сессия 2 = `MLC-089`+`MLC-090` (PR кода) → `MLC-091` (PR канона+ADR после вливания). Жизненный
цикл реестра — на задачу.

**Задачи трека (исполнены по порядку):**

- `MLC-087` (ST-A) · Backend+Frontend · M · **Done (2026-06-10)** — **SQL-инстанс: настройка
  `Sql.Server` — единственный источник** (§7 п.2/4/5). Ключ `Defaults.DatabaseServer` → `Sql.Server`
  (миграция значения `MLC087RenameSqlServerSetting`, reversible, UPDATE сохраняет значение) +
  каталог/сидер + FE-привязка секции «SQL Server». `GET /discovery/databases` без `server=` —
  сервер из `Sql.Server`; `ISqlDatabaseDiscovery.ListDatabasesAsync` без параметра сервера.
  `Sql.Server` читается через `ISettingsSnapshot` в `SqlDatabaseDiscovery` И в
  `DiscoveryEndpoints.GetDatabasesAsync` (гейт пустой настройки → Available:false); `sql-instances`
  и пикер /settings не тронуты. Полный отчёт — в архиве.
- `MLC-088` (ST-B) · Backend+Frontend · M · **Done (2026-06-10)** — **колонка `Infobase.DatabaseServer`
  удалена** (§7 п.1, «чисто»). Дроп миграцией `MLC088DropInfobaseDatabaseServer` (reversible, Down →
  колонка пустой ""). grep-читатели колонки: единственный — `BackupsEndpoints.StartAsync`, переключён
  на настройку `Sql.Server` (+ новый 409 `SQL_SERVER_NOT_CONFIGURED` при пустой).
  `DatabaseBackup.DatabaseServer` (снимок сервера на записи бэкапа) — отдельная колонка, НЕ тронута;
  публикации/webinst используют `OneC.*`; отчёты/перф колонку не читали. Контракты POST/PUT/GET,
  валидация (`DatabaseServerMaxLength` ↔ FE `validation.ts`) + parity, скрытое поле формы и
  toast-гейт — сняты. Пустая настройка ловится на discovery (Available:false) и бэкапе (409).
  Полный отчёт — в архиве.
- `MLC-089` (ST-C) · Backend · S · **Done (2026-06-10)** — **ключ `OneC.Cluster.Server` удалён**
  (§7 п.3). Снят из `SettingKey`+`SettingDefinitions` (FE не трогался — был скрыт из UI); миграция
  `MLC089DropOneCClusterServerSetting` чистит row из `dbo.Settings` (roll-forward only).
  `ResolveClusterServer` (`WebinstArgs.cs`) сужен до host из `RAS.Endpoint`. Полный отчёт + live — в архиве.
- `MLC-090` (ST-D) · Backend+Frontend · S · **Done (2026-06-10)** — **фильтр статуса публикации**
  (принятый кандидат с `MLC-081`). Параметр `publishStatus` в `GET /api/v1/infobases` (server-side,
  коррелированный `EXISTS`; ручной парс enum → 400 на мусор, как `actionType` на `/audit`); UI-фильтр
  на «Базах» + URL-состояние (`?publishStatus=`). Ответ API не изменён (Zod цел). Полный отчёт + live — в архиве.
- `MLC-091` (ST-E) · Docs · S · **Done (2026-06-10)** — **финальный док-PR этапа 2**. Новый
  **ADR-28 «Single-host topology»** (+ Update-нота в ADR-17: поле SQL-сервера промотировано —
  условие ревизии сработало; ADR-4 — `Srvr=` из RAS-хоста); канон `01/03/04` present-tense под
  single-host, +попутно 05 (форма без сервера, фильтр MLC-090). Каталог 04 §4 = 24 ключа, синхрон с
  `SettingDefinitions` ([Doc divergence] `Performance.Recording*` закрыт); discovery Swagger чист
  ещё с MLC-087. Мелочи: поллинг сеансов «~15s»→«~5s»; «Отключить администратора»→«Отключить
  пользователя». **Хвост-тест полноты пройден:** только легитимные остатки (`DatabaseBackup.*`-снимок,
  `sql-instances` пикер /settings, комментарий в `WebinstArgs`). Полный отчёт — в архиве.

**Примечание о CI:** на момент сессии 2 GitHub Actions не стартовал джобы (биллинг аккаунта,
«payments failed») — PR #80/#81 влиты `--admin` с подтверждения пользователя; источник правды —
локальные прогоны (BE 589/589, FE 333, type-check, lint — зелёные).

## Трек «Нераспределённые базы: discovery-first добавление» — отчёты задач (пополняется по мере закрытия)

- `MLC-092` (UB-A) — **endpoint нераспределённых баз + игнор-лист (backend)** — Done (2026-06-11).
  Сессия 1 трека (один PR кода BE). **Доменная модель:** сущность `HiddenClusterInfobase`
  (`Domain/Infobases/`) — игнор-лист служебных баз кластера: `ClusterInfobaseId` (PK, без
  суррогатного Id), `Name` (снапшот имени на момент скрытия — блок «Скрытые» рендерится из БД
  даже при недоступном RAS), `HiddenAtUtc`, `HiddenBy`. EF-конфигурация inline в
  `AppDbContext.OnModelCreating` (`nvarchar(200)`/`nvarchar(256)`, **без FK** — база кластера
  панели не принадлежит). **Миграция** `20260610212042_MLC092HiddenClusterInfobases` — reversible
  (Up `CreateTable` + PK, Down `DropTable`), файлы нормализованы (UTF-8 без BOM + LF, гоча CLAUDE.md).
  **Endpoint** `GET /api/v1/infobases/unassigned` (новый слайс `UnassignedInfobasesEndpoints`,
  Admin-only): `IClusterClient` инжектится напрямую (прецедент `DiscoveryEndpoints`, ADR-20 —
  интерфейс-адаптер, не `rac.exe`). Diff = снапшот RAS **минус** заведённые `Infobase.ClusterInfobaseId`
  **минус** скрытые. Ответ `{ Items[], HiddenItems[], Available, Error, CheckedAtUtc }`; элемент
  Items = `{ ClusterInfobaseId, Name, Description }`, HiddenItems плюс `HiddenAtUtc`/`HiddenBy`.
  **Серверный TTL-кэш** (`UnassignedInfobasesCache`, singleton по образцу `ClusterUuidCache` MLC-041):
  кэшируется **только снапшот RAS** вместе с `CheckedAtUtc` фактического опроса, TTL **60 с**
  (константа `UnassignedInfobasesCache.Ttl`, НЕ ключ настроек); `?refresh=true` — мимо кэша.
  **Diff (заведённые/скрытые) считается на каждый запрос** — create/hide/unhide видны сразу, не
  ждут истечения TTL (тест `Diff_is_not_cached_hide_is_visible_immediately`). RAS недоступен
  (`Available:false` от адаптера ИЛИ исключение) → `Available:false` + санитизированный русский
  Error (сырой stderr `rac.exe` — только в журнал, source-gen logger; паттерн discovery MLC-009),
  не пустой список; отмена (`OperationCanceledException`) пробрасывается. Неуспешный снапшот тоже
  кэшируется на TTL (выравнивает нагрузку на RAS при сбое). **Mutations** (Admin-only):
  `POST …/{clusterInfobaseId}/hide` (body — `Name`-снапшот, длина 200 проверяется руками — гоча
  minimal API: DataAnnotations в runtime не валидируются), `DELETE …/{clusterInfobaseId}/hide`.
  **Гарды:** hide заведённой в панель базы → 409 `UNASSIGNED_ALREADY_ASSIGNED`; повторный hide → 409
  `UNASSIGNED_ALREADY_HIDDEN` (+ uniqueness-backstop по `PK_HiddenClusterInfobases` — новое значение
  `UniqueIndexViolation.HiddenClusterInfobasePk`); unhide несуществующей → 404; пустое/слишком длинное
  имя → 400. **Аудит:** `UnassignedInfobaseHidden = 14`, `UnassignedInfobaseUnhidden = 15` (группа
  Infobase, int **заморожены**), server-scope (`TenantId = null` — база ещё не принадлежит клиенту),
  через `HttpContext.AuditAsync` + формулировки в `AuditDescriptions`. **Чистка игнор-листа** в
  `InfobasesEndpoints.CreateAsync`: при создании Infobase с `ClusterInfobaseId` из игнор-листа строка
  удаляется тем же `SaveChanges` (заведённая база перестала быть «нераспределённой»). Регистрация —
  `Program.cs` (`AddSingleton<UnassignedInfobasesCache>` + `MapUnassignedInfobasesEndpoints`).
  **Тесты** (`UnassignedInfobasesEndpointsTests` 17 + `UnassignedInfobasesAuthorizationTests` 3):
  diff (исключение заведённых/скрытых), кэш (повтор в TTL — 1 опрос; истёкший TTL — 2; refresh
  обходит; неуспех кэшируется), `Available:false` без утечки имени сервера/сырого stderr/исключения,
  отмена пробрасывается, hide пишет строку+аудит-14, hide заведённой→409, двойной hide→409, пустое
  имя→400, имя >200→400, unhide пишет аудит-15, unhide unknown→404, чистка при создании, Admin-only
  на всех трёх маршрутах (метаданные политики). `dotnet test` **610/610** зелёных локально
  (CI красный по биллингу — известно, не чиним). **Канон/ADR не трогались** — отдельный PR `MLC-094`
  после вливания `MLC-093` (FE).

- **Live-прогон на стенде `MLC-092`** (2026-06-11, реальный SQL `Server=.`, БД `MitLicenseCenter`,
  реальный RAS — на стенде доступен, отдал 2 нераспределённые базы кластера `bd1`/`test`).
  **Бэкап ПЕРЕД миграцией** (COPY_ONLY, recovery-point): `MitLicenseCenter_20260611_pre-mlc092.bak`
  → `F:\MlcStage2Backups\`. **Миграция накатана** (`ef database update`, единственная Pending);
  схема `dbo.HiddenClusterInfobases`: `ClusterInfobaseId uniqueidentifier NOT NULL` (PK),
  `Name nvarchar(200)`, `HiddenAtUtc datetime2`, `HiddenBy nvarchar(256)`; запись в
  `__EFMigrationsHistory`. **Аутентифицированный API e2e под admin** (пароль сброшен
  `reset-admin.ps1 -Unlock` с разрешения пользователя 2026-06-11, новое значение — в памяти
  `dev-stand-admin-credentials`; запрет на сброс снят): `GET /infobases/unassigned` → `available:true`,
  2 базы в `items`, **`description` опущено** (null-omit, подтверждает контракт для FE-Zod `.nullish()`);
  `hide bd1` → 204, `GET` → bd1 ушёл в `hiddenItems` (`hiddenBy:admin`, `hiddenAtUtc`), `items` −1,
  **`checkedAtUtc` не изменился** (diff живой поверх кэша снапшота); повторный `hide` → 409
  `UNASSIGNED_ALREADY_HIDDEN`; пустое имя → 400; `unhide` несуществующей → 404; `unhide bd1` → 204,
  `?refresh=true` → bd1 вернулся в `items`, новый `checkedAtUtc` (обход кэша). **Чистка игнор-листа:**
  `hide test` → завёл инфобазу с `clusterInfobaseId=test` (201) → `test` пропала и из `items` (заведена),
  и из `hiddenItems` (строка игнор-листа удалена в `CreateAsync`); `hide` уже заведённой `test` → 409
  `UNASSIGNED_ALREADY_ASSIGNED`. **Аудит:** в `dbo.AuditLogs` — строки `ActionType=14`/`15`,
  `Initiator=admin`, `TenantId=NULL` (server-scope), русские формулировки. **Без cookie** все три
  маршрута → 401 (Admin-гейт). **Уборка:** тестовая инфобаза удалена, `HiddenClusterInfobases` = 0 строк,
  обе базы снова нераспределены — стенд в исходном состоянии (immutable-аудит 14/15 сохранён by design).

- `MLC-093` (UB-B) — **баннер нераспределённых баз + диалог разбора + discovery-first
  добавление (frontend)** — Done (2026-06-11). Сессия 2 трека, PR #87 кода FE. **Хук + типы**
  (`features/infobases/unassigned/`): `useUnassignedInfobases` — TanStack-query на
  `GET /api/v1/infobases/unassigned`, серверный TTL-кэш (MLC-092) — главный (малый `staleTime`
  10 c); кнопка «Обновить» бьёт мимо кэша через `?refresh=true` (флаг в `useRef`, сбрасывается
  после первого опроса, чтобы фоновые инвалидации шли по кэшу, а не дёргали RAS);
  `useHideUnassignedInfobase`/`useUnhideUnassignedInfobase` через `useInvalidatingMutation` с
  инвалидацией ключа `["infobases","unassigned"]` (под префиксом `["infobases"]` — create/update/
  delete prefix-матчем тоже его задевают, заведённая база уходит из списка сразу). Zod-схема с
  `omittable(description)` — API опускает null-поля (урок MLC-067/071), `description` базы
  кластера nullable. **Баннер** (`UnassignedBanner`): warning-семантика (amber, 06 §3; иконка
  `AlertTriangle`), счётчик (плюрализация ru `_one/_few/_many`) + свежесть `<RelativeTime>`
  («Проверено N сек назад», тултип — точное время, 06 §8) + «Обновить»/«Разобрать»; родитель
  рендерит строго при `isAdmin && available && count>0` (ложного нуля нет). **Диалог**
  (`UnassignedInfobasesDialog`): обычный `Dialog` (действия обратимы, `AlertDialog` не нужен —
  06 §7); строка = имя + UUID-моноширинный (06 §4) + опц. описание; действия «Назначить»
  (→ форма с префиллом) и «Скрыть» (`EyeOff`, в игнор-лист без подтверждения); свёрнутый блок
  «Скрытые: N» с «Вернуть» (`RotateCcw`) — рендерится всегда, даже при `available:false` (из
  БД-снапшота); пустое состояние «Все базы разобраны» (06 §6/§9); при `available:false` —
  заметка о недоступном RAS; футер: «Ввести вручную» (fallback на пустую форму) + свежесть +
  «Обновить»; конфликты hide (`UNASSIGNED_ALREADY_ASSIGNED`/`UNASSIGNED_ALREADY_HIDDEN`) через
  `matchConflictCode` → тост. **Префилл** (`InfobaseFormDialog`/`useInfobaseForm`): новый prop
  `prefill` (по образцу `defaultTenantId`) — предвыбор базы кластера (UUID, тот же источник, что
  пикер) + имя как название; best-effort угадывание `databaseName` одноразовым эффектом, когда
  подъедет discovery БД (exact case-insensitive по `useDatabases` → штатная деривация virtual/
  physical path); ключ формы включает `prefill.clusterInfobaseId` (remount на каждое «Назначить»).
  **Страница «Базы»**: баннер между фильтрами и таблицей; «Добавить инфобазу» = discovery-first
  (`handleOpenAdd`: при `available` → диалог разбора, иначе сразу пустая форма — поведение не
  деградирует); query `enabled` только админу; диалог под гейтом `isAdmin`. `/tenants/:id` не
  тронут. i18n `ru.json`: словарь §12 («Назначить»/«Скрыть»/«Вернуть»/«Разобрать»/«Ввести
  вручную»), баннер/диалог/подзаголовок про лимиты/ошибки. **Тесты Vitest**: `UnassignedBanner`
  (счётчик+свежесть, колбэки); `UnassignedInfobasesDialog` (назначить→onAssign, скрыть→POST
  hide со снапшотом, блок скрытых раскрывается+вернуть→DELETE, «Ввести вручную»→fallback, пустое
  состояние, `available:false`+блок скрытых); `InfobasesPage` гейтинг баннера (показ при
  admin+available+count>0; скрыт при count=0/`available:false`/Viewer); префилл (UUID + guess
  имени БД). **347/347 зелёных** локально, `type-check` + `lint` чисты (CI красный по биллингу —
  известно, не чиним). Канон/ADR не трогались — отдельный PR `MLC-094`.

- `MLC-094` (UB-C) — **канон + ADR-29 (docs)** — Done (2026-06-11). Сессия 2 трека, отдельный
  PR канона после вливания `MLC-093`. **ADR-29 «Discovery-first infobase adding + cluster-base
  ignore-list»** (`DECISIONS.md`, формат как ADR-27/28): решение (баннер/диалог/discovery-first
  default-флоу; `Infobase.TenantId` остаётся NOT NULL — «назначить» = обычный create с префиллом;
  read-endpoint diff RAS−заведённые−скрытые; серверный TTL-кэш 60 c как код-константа; таблица
  игнор-листа; аудит 14/15; UI Admin-only); **rejected** — nullable `TenantId` (ветка null-tenant
  через лицензирование/сеансы/запросы ради состояния, эквивалентного «ещё не создана»), фоновый
  снапшот-джоб (always-on store + вторая поверхность дрейфа для вопроса, на который отвечает
  кэш-опрос — резон ADR-26 «live needs no history»), ключ настройки для TTL (60 c — тюнинг-
  константа, каталог whitelist-only); **связь ADR-4** (read-only наблюдение, без auto-fix — панель
  показывает дрейф и даёт оператору разобрать, не правит кластер) и **ADR-16** (чтение кластера
  только через единственный RAS-адаптер). Обратная зона (б) явно вне трека. **Канон present-tense
  без changelog**: `03_DOMAIN_MODEL` — новая сущность §8 `HiddenClusterInfobase` (PK
  `ClusterInfobaseId`, `Name`-снапшот, без FK; чистка при создании Infobase), enum `ActionType`
  14/15 (группа Infobase 10–15, frozen), frozen-список enum-стабильности, 409-коды
  `UNASSIGNED_ALREADY_ASSIGNED`/`UNASSIGNED_ALREADY_HIDDEN`, биндинг-контракт эндпоинтов;
  `04_INFRASTRUCTURE` — подсекция «Unassigned cluster bases» под «1C Cluster Integration» (diff,
  TTL-кэш 60 c только снапшота + diff на каждый запрос, honest degraded `Available:false`,
  мутации hide/unhide + гарды + аудит); `05_UI_REQUIREMENTS` §3.3 (баннер/диалог/discovery-first
  «Добавить базу» с fallback и деградацией; Admin-only) + §3.2 (пометка: на `/tenants/:id` флоу
  прежний); `06_UI_DESIGN` §10 (иконки `EyeOff`/`RotateCcw`) + §12 (domain-словарь «Разобрать»/
  «Скрыть»/«Вернуть»/«Ввести вручную»; фраза «Проверено N сек назад»); `ROADMAP` — в «Дрейф
  панель↔кластер» направление (а) помечено **закрытым** треком (ADR-29), (б) — **обязательным**.
  Валидация инфобаз и каталог настроек не трогались.

- **Live-прогон на стенде `MLC-093`/`MLC-094`** (2026-06-11, реальный backend :5080 + RAS
  доступен, FE Vite :5174 с прокси на API; стенд отдал 2 нераспределённые базы `bd1`/`test`).
  **FE под Admin** (вход через форму, креды из памяти `dev-stand-admin-credentials`): на «Базах»
  **баннер** между фильтрами и таблицей — «2 базы кластера не заведены в панель — их сеансы не
  считаются в лимиты. · Проверено N секунд назад · Обновить · Разобрать» (реальное число баз
  стенда). **«Разобрать»** открывает диалог «Нераспределённые базы кластера»: 2 строки имя +
  моноширинный UUID + «Скрыть»/«Назначить»; футер «Ввести вручную» + «Обновить». **«Назначить»**
  для `bd1` → форма «Новая инфобаза» с **префиллом**: клиент `mtpro`, база кластера предвыбрана
  `bd1`, **имя БД угадано `bd1`** (exact case-insensitive из discovery) — форма не сабмитилась,
  чтобы не плодить артефакты на стенде (полный цикл create→строка в таблице→счётчик −1 проверен в
  live `MLC-092`). **API под admin-cookie**: `?refresh=true` даёт новый `checkedAtUtc` (22:22:47→
  22:23:05, опрос мимо кэша) при неизменном кэш-времени без флага; `hide bd1` → 204 → `bd1` в
  `hiddenItems` (`hiddenBy:admin`), `items` −1; повторный `hide` → 409 `UNASSIGNED_ALREADY_HIDDEN`
  (рус. detail); пустое имя → 400 (рус.); `unhide` несуществующей → 404; `unhide bd1` → 204, после
  `?refresh=true` `bd1` вернулась в `items`. **Аудит** `dbo.AuditLogs`: строки
  `UnassignedInfobaseHidden`/`UnassignedInfobaseUnhidden` (=14/15), initiator `admin`, рус.
  формулировки. **Гейт**: без cookie все маршруты → 401 (Admin-only `RequireAuthorization`);
  Viewer→403 покрыт `UnassignedInfobasesAuthorizationTests` (BE) + гейтинг баннера для Viewer —
  Vitest. **`available:false`-ветка** (RAS остановлен → баннер исчезает, «Добавить» открывает
  ручную форму, диалог показывает заметку о недоступном RAS + «Ввести вручную») покрыта Vitest;
  физически RAS на стенде не останавливался, чтобы не трогать живой кластер. **Уборка:**
  `hiddenItems` пуст, обе базы снова нераспределены, своих инфобаз не создавал — стенд в исходном
  состоянии (immutable-аудит 14/15 сохранён by design).

## Трек «Обратный дрейф панель↔кластер» — отчёты задач (пополняется по мере закрытия)

Направление (б) пункта «Дрейф панель↔кластер» (`ROADMAP.md`): база, удалённая/пересозданная
в кластере 1С, остаётся в панели «здоровой» записью без сигнала («мёртвая душа»; сеансы
реальной базы не считаются в лимит). Read-only наблюдение, без auto-fix (дух ADR-4). Спека —
часть 2 план-файла `C:\Users\andre\.claude\plans\glittery-floating-turtle.md`. Одна сессия:
`MLC-095`+`MLC-096` (один PR кода #91, коммиты раздельно) → `MLC-097` (PR канона #92).

- `MLC-095` (RD-A) — **`MissingItems` в ответе unassigned (backend)** — Done (2026-06-11, PR #91).
  Расширен ответ существующего `GET /api/v1/infobases/unassigned` (списочный `GET /infobases`
  не тронут — критичная Zod-граница). DTO `MissingInfobaseDto { InfobaseId, TenantName, Name,
  ClusterInfobaseId }` + `MissingItems: IReadOnlyList<…>` в `UnassignedInfobasesResponse`.
  `GetUnassignedAsync`: при `Available:true` — обратный diff (записи `Infobases`, чьих
  `ClusterInfobaseId` нет в снапшоте кластера), join имени клиента; при `Available:false` —
  пустой список (сбой опроса RAS ≠ пропавшие базы, ложных красных меток нет). Прямой и обратный
  diff питаются одной проекцией `PanelInfobaseRow` (один join Infobases×Tenants вместо двух
  запросов), сортировка MissingItems по клиенту+имени. Кэш/`refresh` — без изменений. **Без
  миграций, мутаций, новых аудит-кодов и настроек.** +3 теста слайса (обратный diff с join
  имени; пусто при `Available:false`; запись «и в панели и в кластере» не в MissingItems и не в
  Items) → `dotnet test` 613/613 зелёный. Гейт остался: response-record получил 3-й список —
  оба конструктора в `GetUnassignedAsync` обновлены (Available:false-ветка отдаёт пустой
  `MissingItems`).

- `MLC-096` (RD-B) — **баннер + метка + диалог обратного дрейфа (frontend)** — Done (2026-06-11,
  PR #91). Zod-схема unassigned: + `missingInfobaseSchema` (все поля `z.string()` non-null — BE
  их не опускает, `omittable` не нужен) и `missingItems` в response-схеме. `MissingInfobasesBanner`
  (семантика `danger`/rose, иконка `AlertTriangle`, `<RelativeTime>`-свежесть) в **том же слоте**
  `InfobasesPage`, что жёлтый `UnassignedBanner`, и на **том же** query (без второго запроса) +
  кнопка «Показать». `MissingInfobasesDialog` (обычный `Dialog`, 06 §7): строка = клиент · имя ·
  UUID-моно; «Удалить» → существующий `DeleteInfobaseDialog` (AlertDialog-подтверждение по имени
  + аудит) с инвалидацией `["infobases","unassigned"]` (убирает строку и метку). `DeleteInfobaseDialog`
  принят к минимальному контракту `{ id, name }` (`DeletableInfobase`) — запись дрейфа может быть
  на другой странице пагинации, полного `InfobaseListItem` нет. Метка `StatusBadge danger` «Не
  найдена в кластере» в `InfobaseRow` (ячейка статуса, тултип со временем проверки) — рендер по
  membership-набору `Set<clusterInfobaseId>`, собранному на странице строго при
  `isAdmin && available` (Viewer и `available:false` → набор пуст → меток нет). Баннер — при
  `isAdmin && available && count>0`. i18n `infobases.missing.*` (label, tooltip, banner с
  plural-формами, dialog). +8 тестов Vitest (баннер компонента; диалог компонента → onDelete;
  page-гейтинг: баннер+метка при available+missing, нет при пустом/`available:false`/Viewer;
  метка по membership) → `pnpm test` 355/355, `type-check`, `lint` зелёные.

- `MLC-097` (RD-C) — **канон (docs)** — Done (2026-06-11, отдельный PR #92 после вливания #91).
  **ADR-29 Update-нота** в `DECISIONS.md`: направление (б) реализовано **тем же** наблюдательным
  механизмом (`MissingItems` из того же кэшируемого снапшота; только при `Available:true`;
  read-only, без персиста/мутаций/аудит-кодов/миграций) — новый ADR не нужен, это завершение
  ADR-29; обновлены «Relation to ADR-4/ADR-16» (оба направления, без нового RAS-опроса) и
  «Rejected» (серверный фильтр «не найдена» и новые аудит-коды отклонены; зона (б) помечена
  закрытой). Канон present-tense без changelog: `04_INFRASTRUCTURE` — форма ответа
  `{ Items, HiddenItems, MissingItems, … }` + подпункт «Reverse diff» (условие `Available:true`,
  тот же снапшот, `GET /infobases` не тронут); `05_UI_REQUIREMENTS` §3.3 — подпункт «Reverse
  drift» (метка `danger`, красный баннер в одном слоте с жёлтым, диалог «Показать»→«Удалить»
  через существующий delete-флоу, Admin-only, только при reachable+count>0, серверный фильтр —
  отложен); `06_UI_DESIGN` §12 — «Показать» (domain) и «Не найдена в кластере» (statuses);
  `ROADMAP.md` — пункт «Дрейф панель↔кластер» сжат до строки-факта «закрыт полностью» (оба
  направления, `MLC-092..094` + `MLC-095..097`). Валидация инфобаз и каталог настроек не
  трогались.

- **Live-прогон на стенде `MLC-095`..`MLC-097`** (2026-06-11, реальный backend :5080 +
  `Server=.`/БД `MitLicenseCenter`, RAS **доступен** — `available:true`). Под Admin-cookie
  (login :5080 → 200, роль Admin): базовый `GET /infobases/unassigned?refresh=true` →
  `items:[]`, `missingItems:[]`, `available:true`. **Заведена запись с несуществующим UUID
  кластера** `99999999-…-9999` (имитация «Ввести вручную», POST `/infobases`, клиент `mtpro`,
  имя «E2E Призрак MLC-097») → 201. Повторный `unassigned?refresh=true` → `missingItems` ровно
  `[{ infobaseId, tenantName:"mtpro", name:"E2E Призрак MLC-097", clusterInfobaseId:"9999…" }]`,
  `available:true`, `items:[]` (реальные базы кластера меток/нераспределённости не получили —
  ложного жёлтого тоже нет). **Удаление** записи (DELETE `/infobases/{id}`) → 204; следующий
  `unassigned?refresh=true` → `missingItems:[]`, `mtpro.infobaseCount` снова 3. Гейт Admin-only:
  без cookie все маршруты → 401 (подтверждено в логе `RolesAuthorizationRequirement … Admin`).
  FE-рендер (красный баннер/метка/тултип, гейтинг Viewer/`available:false`/пусто, диалог→delete)
  — покрыт Vitest, в браузере в эту сессию не гонялся (живой SPA пользователя не трогал); ветка
  `available:false` (RAS-down) — покрыта тестами BE+FE, физически RAS не останавливал. **Уборка:**
  тестовая запись удалена, стенд в исходном состоянии; backend, поднятый для прогона, заглушён.

## Трек «GUI-установщик (Inno Setup)» — отчёты задач (пополняется по мере закрытия)

Цель трека (`MLC-098..103`): заменить ручную установку (`artifacts/0.1.0-beta/INSTALL.md`) на
графический установщик (`.exe`, мастер) для single-host Windows. Согласовано 2026-06-11: движок
**Inno Setup**; бэкенд **сам отдаёт SPA** (IIS не нужен для хостинга страницы панели — только для
управляемых публикаций 1С); backend публикуется **self-contained**. Полная спека трека —
`.claude/plans/installer-track.md`.

- `MLC-098` — **Бэкенд сам отдаёт SPA (same-origin)** — Done (2026-06-11). Фундамент трека:
  один процесс Kestrel отдаёт и `/api/*`, и страницу SPA. Спека — `.claude/plans/zesty-tinkering-fog.md`.
  - **Код (`Program.cs`).** `app.UseStaticFiles()` вставлен **после** блока HSTS/HTTPS-redirect
    (`if (enforceHttps) {…}`) и **до** `app.UseAuthentication()` — статика (логин-страница + бандлы)
    грузится анониму; хэшированные `/assets/*` кэшируются надолго этим middleware. `app.MapFallback(…)
    .AllowAnonymous()` вставлен **после** `app.UseHangfireDashboard("/hangfire", …)` и **до** блока
    `RecurringJob.AddOrUpdate(...)` — SPA history-fallback: зарезервированный путь → 404; нет
    `WebRootPath`/`index.html` (dev / `SkipSpaBuild`) → 404; иначе `index.html` с `Cache-Control:
    no-cache, no-store, must-revalidate` + `text/html; charset=utf-8` через `SendFileAsync`.
  - **`SpaFallback.cs` (новый, internal static).** Чистый предикат `IsReservedPath(PathString)` —
    зеркало `TransportSecurity.cs`: `StartsWithSegments("/api"|"/hangfire", OrdinalIgnoreCase)`.
    `/api` (REST + `/api/docs`) и `/hangfire` НЕ перехватываются fallback'ом → неизвестный `/api/*`
    отдаёт честный 404, а не замаскированный `200 HTML`; дашборд Hangfire держит свою авторизацию.
  - **`MitLicenseCenter.Web.csproj`.** MSBuild-таргеты `BuildSpa` (`pnpm install --frozen-lockfile`
    + `pnpm build`) и `CopySpaToPublish` (`AfterTargets="Publish"`, `DependsOnTargets="BuildSpa"`):
    копирует `frontend/dist` в `$(PublishDir)wwwroot` (обходит капризный Static-Web-Assets pipeline;
    `dotnet build`/`test`/dev НЕ триггерят → inner-loop быстрый, без node/pnpm/`wwwroot`). Свойства
    `FrontendDir`/`SpaDistDir`/`SkipSpaBuild`/`PrebuiltSpaDist`; `<Error>` при отсутствии
    `$(SpaDistDir)\index.html`. Опт-ауты: `-p:SkipSpaBuild=true` (publish без SPA, SPA → 404) и
    `-p:PrebuiltSpaDist=<dist>` (готовый dist без pnpm — для CI/инсталлятора).
  - **`.gitignore`.** Добавлен `backend/src/MitLicenseCenter.Web/wwwroot/` (генерируемый артефакт,
    существует только в выводе publish, не в дереве исходников).
  - **Тест.** `Web/SpaFallbackTests.cs` (зеркало `TransportSecurityTests`): `[Theory]` на
    `IsReservedPath` — positive `/api`, `/api/v1/health`, `/api/v1/unknown`, `/api/docs`, `/hangfire`,
    `/API/V1/HEALTH`; negative `/`, `/login`, `/tenants`, `/tenants/42`, `/settings`, `/applications`,
    `/hangfireish`. Интеграционный мини-пайплайн НЕ добавлялся (дисциплина host-boot тестов тянет SQL;
    end-to-end закрыт прогоном опубликованного артефакта), `WebApplicationFactory<Program>` не
    использовался.
  - **Канон (present-tense, в том же ходу).** Новый **ADR-30** «Backend hosts the SPA (same-origin);
    IIS не нужен для страницы панели» (решение + связь с ADR-1/12/20/28, отклонённые альтернативы:
    IIS+ARR для статики / отдельный статик-сервер / `UseDefaultFiles`+SWA-in-tree; node/pnpm как
    publish-time предусловие; фундамент инсталлятора). `04_INFRASTRUCTURE.md` §3 — топология: страницу
    отдаёт backend (Kestrel) same-origin с API, IIS — только для управляемых публикаций 1С.
    `OPERATIONS.md` — новая секция «Backend hosts the SPA» (build-предусловия publish: node ≥22.13 +
    pnpm 11 или `SkipSpaBuild`/`PrebuiltSpaDist`; `wwwroot` едет в артефакте; кэш `index.html`
    no-cache / хэш-ассеты cache-forever; `Security:EnforceHttps` без изменений; dev по-прежнему
    vite :5173) + обновлены шаги «Deployment is manual» (publish собирает фронт, отдельного
    статик-шага нет).
  - **Anti-corruption граница (ADR-20).** Раздача статики не добавила запрещённых зависимостей
    (нет `System.Diagnostics.Process` / `Microsoft.Web.Administration` / Infrastructure-адаптеров) —
    NetArchTest `LayerBoundaryTests` остался зелёным.
  - **Проверка.** `scripts\build.ps1 -Configuration Release` — зелёный (~59 с): BE build 0
    warnings, `dotnet test` 626/626 (включая 13 новых кейсов `SpaFallbackTests`), format чистый;
    FE 355/355 тестов, lint/type-check/build зелёные (баннеры `pnpm : $ …` в stderr — известный
    ложный «красный», CLAUDE.md). Самодостаточность публиша: `dotnet publish … -c Release -o <tmp>`
    (~6 с) → `<tmp>\wwwroot\index.html` + `<tmp>\wwwroot\assets\` (46 файлов) присутствуют.
  - **Вне scope (следующие задачи трека).** `MLC-099` self-contained single-file publish;
    `MLC-100..102` Inno Setup (служба, SQL-страница, захват пароля admin); `MLC-103` деинсталляция.
    Гоча для них: `dotnet publish` теперь требует node ≥22.13 + pnpm 11 (если не задан Skip/Prebuilt);
    инсталлятор-пайплайн, вероятно, использует `-p:PrebuiltSpaDist=<dist>`.

- `MLC-099` — **Self-contained single-file publish + publish-скрипт** — Done (2026-06-11). Задача 2/6
  трека: повторяемый скрипт, собирающий готовый к установке артефакт без .NET на хосте. Спека —
  `.claude/plans/mlc-099-self-contained-publish.md`.
  - **`scripts/publish-release.ps1` (новый, UTF-8 с BOM).** Обёртка над `dotnet publish` в стиле
    `build.ps1`: успех нативного шага — только по `$LASTEXITCODE`, вокруг вызова снят
    `$ErrorActionPreference='Stop'` (обход stderr-спама PS 5.1). Параметры: `-OutputDir` (дефолт
    `artifacts\<version>\backend`; `<version>` читается из тега `<Version>` в `backend\Directory.Build.props`
    через `[xml]`), `-Configuration` (дефолт `Release`), `-FrameworkDependent` (switch),
    `-SkipSpaBuild` (→ `-p:SkipSpaBuild=true`), `-PrebuiltSpaDist <путь>` (→ `-p:PrebuiltSpaDist=…`).
    Пути нормализуются хелпером `Resolve-AbsolutePath` (rooted-путь — как есть, относительный — от CWD;
    наивный `Join-Path CWD <absolute>` ронял `GetFullPath` на «format is not supported» — поймано на
    первом прогоне дефолтного абсолютного `-OutputDir` и исправлено). Итоговый вывод: путь артефакта,
    размер `MitLicenseCenter.Web.exe`, наличие `wwwroot\index.html`.
  - **Дефолт — self-contained single-file win-x64.** `dotnet publish … -r win-x64 --self-contained true
    -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true`.
    Рантайм .NET 10 вшит в exe → хосту .NET не нужен. `-FrameworkDependent` публикует без
    `-r/--self-contained/PublishSingleFile` (нужен .NET 10 на хосте). **Trimming сознательно НЕ включён**
    (`PublishTrimmed` не задаётся): EF Core / Hangfire / Identity рефлексивны, обрезка их ломает.
  - **csproj не менялся** — флаги single-file/self-contained живут в скрипте, а не в проекте, чтобы
    обычный `dotnet publish`/inner-loop и framework-dependent-вариант не ломались. Таргет
    `CopySpaToPublish` (MLC-098, `AfterTargets="Publish"`) сам кладёт `wwwroot` рядом с exe.
  - **Канон (present-tense, в том же ходу).** `docs/DECISIONS.md` — **Update-нота к ADR-14**:
    `publish-release.ps1` — build/packaging-тулинг (производит артефакт), **не** CD/деплой-автоматизация;
    ручной деплой и отложенный `Deploy-MitLicenseCenter.ps1` без изменений (снимает код↔doc-расхождение —
    перечень `scripts/` в ADR-14 разошёлся с деревом). `docs/OPERATIONS.md` — «Deployment is manual»
    шаг 2 переписан на `scripts/publish-release.ps1` (self-contained по умолчанию, `-FrameworkDependent`
    как опция, trimming off); секция «Backend hosts the SPA» — пункт про сборку артефакта скриптом +
    опт-ауты в switch-форме (`-SkipSpaBuild` / `-PrebuiltSpaDist`). `CLAUDE.md` — строка в «Командах».
  - **Проверка.** `scripts\build.ps1 -Configuration Release` — зелёный (exit 0; csproj не менялся).
    Self-contained публиш (~16 с инкрементально): `MitLicenseCenter.Web.exe` **55.6 МБ** (рантайм вшит),
    `wwwroot\index.html` + `wwwroot\assets\` (46 файлов), `appsettings.Production.json` (template) +
    `web.config` присутствуют; артефакт = 1 exe + 4 pdb + 3 appsettings + web.config + SPA (~58.5 МБ всего).
    Framework-dependent публиш (`-FrameworkDependent`): без вшитого рантайма (россыпь dll, exe-стартер
    лёгкий), требует .NET 10 на хосте. Полный запуск exe против SQL — приёмочный шаг инсталлятора (MLC-100+),
    здесь не выполнялся.
  - **Вне scope (следующие задачи трека).** `MLC-100` Inno Setup каркас (служба, ACL key ring, firewall);
    `MLC-101` страница SQL + генерация `appsettings.Production.json`; `MLC-102` захват пароля admin;
    `MLC-103` деинсталляция. Открытые вопросы куратора (учётка службы, код-подпись, апгрейд поверх) — к `MLC-100+`.

- `MLC-100` — **Inno Setup каркас: установщик (файлы + служба + firewall + старт, с обновлением)** — Done (2026-06-11).
  Задача 3/6 трека: `.exe`-установщик, ставящий панель одной Windows-службой на single-host, с обновлением поверх.
  Спека — `.claude/plans/mlc-100-installer-skeleton.md`. Решения куратора (2026-06-11): служба под **LocalSystem**
  (выбор аккаунта → MLC-101), **без код-подписи** (SmartScreen приемлем для LAN), **сразу с обновлением** поверх.
  - **`installer/MitLicenseCenter.iss` (новый, UTF-8 с BOM — Inno 6 Unicode, кириллица в сообщениях/StatusMsg).**
    `[Setup]`: фикс. `AppId={B7E9F3A2-4C1D-4E8A-9F6B-2D5A8C3E1F40}` (детект апгрейда — **менять нельзя**),
    `AppName=MitLicense Center`, `AppVersion={#MyAppVersion}`, `DefaultDirName={autopf}\MitLicense Center`,
    `PrivilegesRequired=admin`, `OutputBaseFilename=MitLicenseCenter-Setup-{#MyAppVersion}`,
    `ArchitecturesInstallIn64BitMode=x64`. `[Languages]` — Russian (`compiler:Languages\Russian.isl`).
    `#ifndef PublishDir → #error` (скрипт без артефакта не компилируется по ошибке вызова).
  - **`[Files]`.** `{#PublishDir}\*` → `{app}` (`recursesubdirs createallsubdirs ignoreversion`, `Excludes:
    appsettings.Production.json`); отдельно `appsettings.Production.default.json` → `{app}\appsettings.Production.json`
    с флагом **`onlyifdoesntexist`** (апгрейд не затирает правки оператора). Дефолт-конфиг — новый
    `installer/appsettings.Production.default.json` (`Server=.`/`Trusted_Connection=True`/`Urls=http://+:8080`/
    `EnforceHttps=false`). `[Dirs]` — `{commonappdata}\MitLicenseCenter` с `uninsneveruninstall` (key ring, purpose
    `mlc.settings.v1`; под SYSTEM дефолтных ACL достаточно — явный grant отложен на MLC-101).
  - **Служба (через `{sys}\sc.exe`, нативной у Inno нет).** `[Run]`: на чистой установке (`Check: not ServiceExists`)
    `sc create MitLicenseCenter binPath= "{app}\MitLicenseCenter.Web.exe" start= auto DisplayName= "MitLicense Center"`
    (obj не задаём → **LocalSystem**); на апгрейде (`Check: ServiceExists`) службу не пересоздаём — только
    `sc config … binPath=` (выравнивание пути); затем `sc description …`, `sc start …`. Firewall: `[Run]`
    `netsh advfirewall firewall add rule name="MitLicense Center" dir=in action=allow protocol=TCP localport=8080`.
  - **Обновление поверх (`[Code]`).** `PrepareToInstall` зовёт `StopServiceAndWait`, **до** `[Files]` — иначе exe
    залочен. `ServiceExists`/стоп реализованы через P/Invoke `advapi32` (`OpenSCManagerW`/`OpenServiceW`/
    `ControlService`/`QueryServiceStatus`/`CloseServiceHandle`): служба останавливается и цикл ждёт фактического
    `SERVICE_STOPPED` (до ~30 с, 60×500 мс). Сохраняются: `appsettings.Production.json`, key ring, БД. На старте
    службы миграции накатываются **fail-fast (ADR-18)**.
  - **`[UninstallRun]`.** `sc stop` + `sc delete MitLicenseCenter` (RunOnceId), удалить firewall-правило
    (`netsh … delete rule`). БД и `{commonappdata}\MitLicenseCenter` (key ring) **оставить** (keep-data/полировка — MLC-103).
  - **`scripts/build-installer.ps1` (новый, UTF-8 с BOM, стиль build.ps1/publish-release.ps1).** Параметры:
    `-Configuration` (Release), `-OutputDir` (дефолт `artifacts\<version>`), `-SkipPublish`. Шаги: (1) если не
    `-SkipPublish` → `publish-release.ps1 -OutputDir artifacts\<version>\backend` (self-contained); (2) `Find-Iscc`:
    PATH → `%LOCALAPPDATA%\Programs\Inno Setup 6\ISCC.exe` → `C:\Users\andre\…` → `%ProgramFiles(x86)%` →
    `%ProgramFiles%`; нет → ошибка с `winget install JRSoftware.InnoSetup`; (3) `ISCC /DMyAppVersion=<version>
    /DPublishDir=<артефакт> /O<OutputDir> installer\MitLicenseCenter.iss`; (4) путь + размер Setup.exe. Версия из
    `<Version>` `backend\Directory.Build.props` (та же `[xml]`-логика, что в publish-release.ps1). Успех нативных
    шагов — по `$LASTEXITCODE` (снят `Stop` вокруг вызова, обход stderr-спама PS 5.1).
  - **Канон (present-tense, в том же ходу).** `docs/DECISIONS.md` — **ADR-31 «GUI installer (Inno Setup) for the
    single host»** (одна служба LocalSystem, same-origin per ADR-30, firewall, апгрейд с сохранением
    конфига/ключей/БД, миграции fail-fast ADR-18, без код-подписи; отклонено MSI/WiX, IIS+ARR, подпись в v1, ввод
    SQL сейчас) + **Update-нота к ADR-14** (установщик пакует install/upgrade, но CI→host CD по-прежнему отложен).
    `docs/OPERATIONS.md` — секция «GUI installer (ADR-31)» (сборка, что ставит, семантика апгрейда, деинсталл,
    SmartScreen). `CLAUDE.md` — `build-installer.ps1` в «Командах». `docs/PROJECT_BACKLOG.md` — `MLC-100` → Done,
    NEXT очищен, трек не закрыт (3/6).
  - **Проверка.** `scripts\build.ps1 -Configuration Release` — зелёный (C#/csproj не менялись). `build-installer.ps1`
    → ISCC компилирует `.iss` **без ошибок** → `artifacts\<version>\MitLicenseCenter-Setup-<version>.exe`. Реальный
    тест-инсталл службы/firewall — приёмочный шаг оператора (инвазивно), здесь не выполнялся: только компиляция + ревью `.iss`.
  - **Вне scope (следующие задачи трека).** `MLC-101` интерактивная страница (SQL + тест подключения, hostname/port,
    генерация `appsettings.Production.json`); `MLC-102` захват/показ первого пароля admin; `MLC-103` деинсталляция-полировка
    (keep-data prompt, ярлыки).

- `MLC-106` — **Bootstrap создаёт БД, если её нет (на пустом инстансе)** — Done (2026-06-11).
  - **Проблема.** На по-настоящему пустом SQL-инстансе (БД ещё нет) служба **падала**, БД не создавалась.
    Первопричина: `DependencyInjection.cs:52` включает `EnableRetryOnFailure(maxRetryCount: 3)`, а `IdentitySeeder`
    зовёт `db.Database.MigrateAsync()` — известная ловушка EF Core: на несуществующей БД ошибка 4060 «cannot open
    database» трактуется retry-стратегией как транзиентная и ретраится, затем `MigrateAsync` бросает → fail-fast →
    APPCRASH. `MigrateAsync` сам НЕ создаёт БД под retry-стратегией. В dev баг маскировал `db-reset.ps1` (создаёт БД
    заранее); на стенде БД всегда уже была. Затрагивало и установщик, и ручной деплой (OPERATIONS обещал «БД создаётся
    при первом старте» — было неправдой для пустого инстанса).
  - **Сделано** (backend; хелпер + ранний вызов + тест + канон). (1) Новый
    `backend/src/MitLicenseCenter.Infrastructure/Persistence/DatabaseBootstrapper.cs` (статический,
    `Microsoft.Data.SqlClient` — допустим в слое Persistence): `EnsureDatabaseCreatedAsync(connectionString, ct)` —
    извлекает `InitialCatalog` через `SqlConnectionStringBuilder`; пусто → no-op; строит master-строку (тот же билдер,
    `InitialCatalog="master"`, креды/`Encrypt`/`TrustServerCertificate` сохранены — приём `SqlDatabaseDiscovery`),
    открывает `SqlConnection` и выполняет `IF DB_ID(@name) IS NULL EXEC('CREATE DATABASE [<экранир. имя>]')` — имя БД
    проверяется параметром `@name` в `DB_ID`, в DDL подставляется экранированное (`]`→`]]`) имя (DDL не принимает имя
    объекта параметром; параметр + экранирование — defense-in-depth). Существующая БД не трогается. Один statement к
    master обходит EF-ловушку 4060 целиком (retry ни при чём). Чистые под-хелперы `internal GetDatabaseName(cs)` /
    `ToMasterConnectionString(cs)` (`InternalsVisibleTo MitLicenseCenter.Tests.Unit` уже был). (2) `Program.cs` — вызов
    **сразу после `var app = builder.Build();`**, ДО `app.UseHangfireDashboard` / `RecurringJob.AddOrUpdate` (Hangfire
    `SqlServerStorage` тоже коннектится к БД) и до `IdentitySeeder`/`SettingsSeeder`. **Гейт:** только при непустой
    `app.Configuration.GetConnectionString("Default")` (InMemory-тесты через `WebApplicationFactory` строку не задают →
    пропуск; в dev строка есть, `IF DB_ID IS NULL` — no-op, т.к. `db-reset` уже создал БД). Обёрнут в собственный
    fail-fast `try/catch` с `app.Logger.LogCritical(...)` + `throw` (как существующий bootstrap-try). (3) Тест
    `backend/tests/MitLicenseCenter.Tests.Unit/Persistence/DatabaseBootstrapperTests.cs` — покрывает чистые под-хелперы:
    извлечение имени БД, пустой `InitialCatalog`, замена `Database`→`master`, сохранение SQL-кред / `Encrypt` /
    `TrustServerCertificate` / Integrated Security, и `EnsureDatabaseCreatedAsync` = no-op при пустом каталоге (6 тестов,
    все зелёные). Сам `CREATE DATABASE` юнитом не тестируется (SQL-Server-специфичен — ручная приёмка на стенде).
  - **Канон.** `docs/DECISIONS.md` **ADR-18** — новый буллет «Database creation precedes migrations (MLC-106)»
    (первопричина 4060+retry; ранний `CREATE DATABASE` к master до Hangfire/Migrate; existing БД не трогается; гейт на
    непустую `Default`; нужны права `CREATE DATABASE` — `sysadmin`/`dbcreator`, уже требуется каноном); порядок bootstrap
    в «Decision» дополнен шагом создания БД. `docs/OPERATIONS.md` «Startup is fail-fast» — порядок bootstrap дополнен
    созданием БД + явная нота «БД не обязана существовать до первого старта (MLC-106)». `docs/PROJECT_BACKLOG.md` —
    `MLC-106` → Done (строка «Вне трека» + гоча), NEXT очищен, счётчик `MLC-107` (новая секция трека не заводилась —
    standalone follow-up к закрытому треку «GUI-установщик»).
  - **Проверка.** `scripts\build.ps1 -Configuration Release` — non-smoke зелёные, включая новые `DatabaseBootstrapperTests`
    (smoke RAS — environmental, не блокер). Реальный `CREATE DATABASE` / тест-инсталл — приёмочный тест оператора на стенде
    (`DROP DATABASE MitLicenseCenter` → `Start-Service MitLicenseCenter` → служба стартует, БД создана, миграции применены,
    `admin` засеян).
  - **Гочи.** Транзитивный `Microsoft.Data.SqlClient` доступен тесту через ссылку на Infrastructure (EF SqlServer) — отдельный
    PackageReference в Tests.Unit не нужен. NetArchTest зелёный: сырой `SqlClient` в `Infrastructure.Persistence` — это слой
    доступа к БД, не адаптер к 1С/IIS (ADR-20). `.cs` — без BOM, LF; `TreatWarningsAsErrors` — 0 варнингов.

- `MLC-107` — **Установщик: надёжное создание службы + чистый конфиг при переустановке** — Done (2026-06-11).
  - **Проблема (стенд, лог установки).** `sc create … obj= "sa"` → exit **1057** (`sa` — SQL-логин, не Windows-аккаунт:
    оператор выбрал Windows-режим для SQL-логина), далее `sc description`/`sc start` → 1060, и установка завершилась
    «успехом» **без службы** — потому что шаги жили в `[Run]` и их провал проходил молча. Второй баг: `appsettings.Production.json`
    генерится в `[Code]` (не `[Files]`) → деинсталлятор его не удалял, а `WriteProductionConfig` пропускал запись
    (skip-if-exists безусловно) → при переустановке залипал старый конфиг.
  - **Сделано (`installer/MitLicenseCenter.iss`, только `.iss`).** Создание/конфиг/старт службы и firewall перенесены из
    `[Run]` в `[Code]` (`ConfigureService`, в конце `CurStepChanged(ssPostInstall)`) через `Exec(sc.exe/netsh.exe, …, rc)`
    с проверкой `rc`: на провале `sc create` (чистая установка) — `MsgBox` (особый текст на **1057** с подсказкой выбрать
    «(B) SQL-аутентификация») + `RaiseException` (откат установки — нет «успеха без службы»); `sc config` (апгрейд) —
    предупреждение; `netsh add` — предупреждение; `sc start` — предупреждение со ссылкой на Журнал событий (ADR-18), без
    Abort. `[UninstallDelete]` сносит `{app}\appsettings.Production.json`; `WriteProductionConfig` пропускает запись только
    на апгрейде (`ServiceExists`), на чистой установке — перезаписывает. Подписи режимов A/B и подсказки на странице
    учётных данных уточнены (Windows-аккаунт vs SQL-логин). Backend не тронут.
  - **Канон.** `docs/DECISIONS.md` **ADR-31** — новый буллет «Service registration happens in `[Code]` with return-code
    checks (MLC-107)» + обновлены буллеты Upgrade/Uninstall (чистка/перезапись конфига). `docs/OPERATIONS.md» «GUI installer» —
    подсказка «(B) для SQL-логина / 1057» в шагах мастера. `docs/PROJECT_BACKLOG.md` — `MLC-107` → Done (строка «Вне трека»),
    NEXT очищен, счётчик `MLC-108`.
  - **Проверка.** ISCC компилирует `.iss` чисто (Successful compile; `Setup.exe` ~57 МБ). Backend не менялся → `build.ps1`
    как на main. Реальная установка (SQL-режим → служба под LocalSystem; Windows-режим с неверным аккаунтом → внятная ошибка;
    переустановка → конфиг перезаписан) — приёмочный тест оператора.
  - **Гоча восстановления (куратор).** Исполнитель оборвался по лимитам до коммита/компиляции; куратор восстановил работу,
    дописал бэклог-Done и **поймал ошибку компиляции** (в doc-комментарии `ConfigureService` строка начиналась с `[Run]`, и
    были `{sys}`-консты внутри `{ }`-комментария — ISCC «Invalid section tag»; переписано без `[`-в-начале и без `{...}`),
    затем влил. Урок для `.iss`-комментариев — известная грабля из CLAUDE.md.

- `MLC-105` — **Установщик: распознавать уже-инициализированную БД (не игнорировать пароль молча)** — Done (2026-06-11).
  - **Проблема.** «Чистая установка» (службы нет) ≠ «пустая БД»: деинсталл не трогает SQL-базу, поэтому новый
    `Setup.exe` может бить в БД с прежними учётками. Сидер задаёт операторский пароль admin **только при сидинге
    пустой БД** (`userManager.Users.AnyAsync()` == false); если пользователи уже есть — он их не трогает и **удаляет**
    `.secret`, не применяя его → заданный в мастере пароль молча игнорируется. Воспроизведено на стенде 2026-06-11
    (БД с прошлым `admin` + `stage2-viewer`; вход новым паролем не проходил; вылечено `reset-admin`).
  - **Сделано** (только `installer/MitLicenseCenter.iss`, `[Code]`; backend не тронут). (1) Функция-проба
    `DatabaseHasPanelUsers: Boolean` — тем же приёмом, что `TestSqlConnection`: connstr (на выбранную БД, **не** master)
    кладётся в `'...'`-литерал временного `.ps1`, `powershell -NoProfile -ExecutionPolicy Bypass -File`, скрипт удаляется
    сразу. PS-сниппет открывает `System.Data.SqlClient`-соединение и выполняет
    `IF OBJECT_ID('auth.Users','U') IS NULL SELECT 0 ELSE SELECT COUNT(*) FROM auth.[Users]`, пишет число во временный
    out-файл (out-файл тоже удаляется); >0 → есть пользователи. Зеркалит any-user условие сидера. Креды/режим — как в
    тесте: B — SQL-логин, A — Integrated Security установщика. **Fail-open:** БД недоступна / ещё не создана / ошибка
    запроса / не открылось → `False` («не инициализирована»), поведение не хуже текущего. (2) Глобальный флаг
    `DbAlreadyInitialized` — вычисляется **один раз** в `NextButtonClick` при уходе со страницы «Сеть» (`PageNet`), только
    если `not ServiceExists`; проба **не** гоняется на каждый `ShouldSkipPage`. (3) `ShouldSkipPage(PageAdmin)` →
    `ServiceExists or DbAlreadyInitialized` (страница пароля пропускается и на апгрейде, и на уже-инициализированной БД).
    (4) Один раз — информационный `MsgBox`: БД `<имя>` уже содержит установку панели, заданный пароль admin **не** будет
    применён, существующие учётки сохраняются, сменить — `reset-admin` (OPERATIONS) или ставить на пустую БД. (5)
    `WriteInitialAdminPassword` — в начале `if ServiceExists or DbAlreadyInitialized then Exit` (не пишем `.secret`, если
    применять некуда). (6) Финальный экран (`wpFinished`) — ветка для `DbAlreadyInitialized and not ServiceExists`: «учётные
    записи в существующей базе сохранены — войдите прежними кредами (или сбросьте пароль admin утилитой reset-admin)» + URL.
  - **Канон.** `docs/DECISIONS.md` **ADR-31** — новый под-буллет «Existing-database recognition (MLC-105)» (проба `auth.Users`,
    fail-open, пропуск страницы пароля + предупреждение, `.secret` не пишется; «чистая установка» ≠ «пустая БД»; гоча —
    проба завязана на имя таблицы `auth.Users`; правка строки `ShouldSkipPage` = `ServiceExists or DbAlreadyInitialized`).
    `docs/OPERATIONS.md` секция «GUI installer» — шаг 5 мастера уточнён + новый буллет «Installing onto a database that
    already has a panel install». `docs/PROJECT_BACKLOG.md` — `MLC-105` → Done (строка «Вне трека»), NEXT очищен, счётчик
    `MLC-106`; новая секция трека не заводилась (трек «GUI-установщик» закрыт, это standalone follow-up).
  - **Проверка.** `scripts\build-installer.ps1` — ISCC компилирует `.iss` без ошибок, `Setup.exe` собран.
    `scripts\build.ps1 -Configuration Release` — non-smoke зелёные (backend не менялся; smoke RAS — environmental).
    Реальная установка на непустую/пустую БД — приёмочный тест оператора.
  - **Гочи.** `.iss` — UTF-8 **с BOM**. В Pascal избегали строк-комментариев, начинающихся с `[`, и brace-констант внутри
    `{ }`-комментариев. Пароли/connstr не логируются; временные `.ps1` и out-файл удаляются.

- `MLC-104` — **Backend осведомлён о Windows-службе (корректива к MLC-100)** — Done (2026-06-11).
  - **Проблема.** Установщик MLC-100 регистрирует exe службой через `sc create`, но `Program.cs` не звал
    `UseWindowsService()`. Не-service-aware процесс не сигналит SCM `SERVICE_RUNNING`, поэтому `sc start`
    падает по таймауту (ошибка **1053**) и служба не встаёт — блокер тест-инсталла скелета MLC-100 и всего установщика.
  - **Сделано.** (1) `backend/Directory.Packages.props` — `PackageVersion Include="Microsoft.Extensions.Hosting.WindowsServices"
    Version="10.0.8"` (в линию с прочими `Microsoft.Extensions.*` / EF / ASP.NET Core 10.0.8). (2)
    `backend/src/MitLicenseCenter.Web/MitLicenseCenter.Web.csproj` — `PackageReference` без версии (central package
    management). (3) `Program.cs` сразу после `var builder = WebApplication.CreateBuilder(args);` —
    `builder.Host.UseWindowsService(o => o.ServiceName = "MitLicenseCenter");` с комментарием. Вызов —
    **no-op в консоли/dev** (детектит запуск под SCM), поэтому `dev.ps1` / inner-loop не меняются; под службой хост
    (а) корректно отвечает SCM на start/stop, (б) ставит content root = каталог exe (находит `appsettings.Production.json`/
    `wwwroot` рядом), (в) шлёт логи в **Windows Event Log** (оператор там видит fail-fast `Critical` и первый пароль admin).
  - **Канон.** `docs/DECISIONS.md` — правка **ADR-31** (новый буллет «Backend is service-aware (MLC-104)»: `sc create`
    работает именно благодаря `UseWindowsService`, иначе `sc start` → 1053; content root, Event Log). `docs/OPERATIONS.md`
    секция «GUI installer» — буллет «Diagnosing service start» (под службой логи → Event Log: fail-fast Critical + первый
    пароль admin). `docs/PROJECT_BACKLOG.md` — `MLC-104` → Done, NEXT очищен, трек не закрыт.
  - **Проверка.** `scripts\build.ps1 -Configuration Release` — non-smoke зелёные (623). Smoke `RacExecutableSmokeTests`
    (Category=Smoke) зависят от живого 1С RAS на стенде — environmental, к задаче не относятся. Реальный старт службы под
    SCM — приёмочный тест-инсталл оператора.
  - **NetArchTest.** `Microsoft.Extensions.Hosting.WindowsServices` — framework-расширение хостинга, не Infrastructure-адаптер
    / `Process` / `Web.Administration` → `LayerBoundaryTests` остаются зелёными.
  - **Вне scope.** Интерактивный мастер (SQL/hostname/аккаунт) — `MLC-101`; показ пароля admin в мастере — `MLC-102`.

- `MLC-101` — **Интерактивный мастер установщика: учётные данные SQL + сеть** — Done (2026-06-11).
  - **Проблема.** Скелет MLC-100 ставил дефолт-конфиг (`Server=.` Trusted, служба LocalSystem). На хосте, где
    у LocalSystem нет прав на SQL, служба падает на fail-fast bootstrap (подтверждено на стенде: APPCRASH,
    MSSQL «SYSTEM не удалось открыть базу MitLicenseCenter»). Решение оператора (2026-06-11): установщик
    **спрашивает учётные данные** заранее созданного аккаунта с нужными правами — **либо ОС-аккаунт** (служба
    под ним, Trusted SQL), **либо SQL-логин** (служба LocalSystem, SQL-аутентификация).
  - **Сделано (`installer/MitLicenseCenter.iss`, расширен поверх MLC-100, всё прежнее сохранено — AppId,
    `[Files]` Excludes, апгрейд через `PrepareToInstall`, uninstall, firewall).** Кастомные `[Code]`-страницы
    мастера (`InitializeWizard`): **(1) SQL Server** — `CreateInputQueryPage` (инстанс, дефолт `.`; БД, дефолт
    `MitLicenseCenter`). **(2) Аутентификация** — `CreateInputOptionPage` radio: (A) Windows (служба под
    указанным ОС-аккаунтом, `Trusted_Connection`) / (B) SQL (LocalSystem, SQL-логин). **(3) Учётные данные** —
    `CreateInputQueryPage` (аккаунт/логин + пароль `IsPassword=True`); подписи полей и подсказка
    (`SubCaptionLabel`/`PromptLabels`) перерисовываются под режим в `CurPageChanged`; кнопка «Проверить
    подключение» (`TNewButton`) + цветная метка-результат (`TNewStaticText`). **(4) Сеть** —
    `CreateInputQueryPage` (порт, дефолт `8080`; `AllowedHosts`, дефолт `*`). Валидация и **гейт Next** в
    `NextButtonClick` (непустые поля, порт 1..65535, успешный тест подключения для страницы учётных данных).
  - **Тест подключения (без правки backend).** `TestSqlConnection` пишет временный `.ps1` со сниппетом
    `New-Object System.Data.SqlClient.SqlConnection; $c.Open()` (есть в PS 5.1) и зовёт
    `powershell.exe -NoProfile -ExecutionPolicy Bypass -File … (exit 0/1)`. Строка подключения передаётся через
    **переменную окружения `MLC_CONN`** (наследуется дочерним процессом), а **не** через командную строку —
    пароль не светится в списке процессов и не нужно экранировать кавычки; переменная очищается сразу после
    `Exec`. Тест — к `Database=master` (БД панели может не существовать). Режим **B**: полноценный тест
    введёнными SQL-creds. Режим **A**: тест достижимости инстанса под Integrated Security установщика-админа +
    честная подпись, что права самого сервис-аккаунта проверятся при первом старте (fail-fast → Event Log).
  - **Применение выбора (`CurStepChanged(ssPostInstall)` + `[Run]` через `{code:…}`-параметры).**
    `WriteProductionConfig` генерирует `appsettings.Production.json` из ввода (`SaveStringToFile`, UTF-8 без BOM):
    `ConnectionStrings:Default/Hangfire` по режиму (A: `Trusted_Connection=True`; B: `User Id`/`Password`; оба
    `Encrypt=True;TrustServerCertificate=True;Application Name=…`), `Urls=http://+:<port>`, `AllowedHosts`,
    `Security:EnforceHttps=false`/`EnableSwagger=false`. **Skip-if-exists** (`FileExists` → `Exit`) — апгрейд не
    затирает правки оператора (паритет с прежним `onlyifdoesntexist`; дефолт-template
    `appsettings.Production.default.json` удалён из репо). Служба: `GetScCreateParams`/`GetScConfigParams`
    формируют параметры `sc` — режим A добавляет `obj= "<аккаунт>" password= "<пароль>"`, режим B — LocalSystem
    (config-ветка явно возвращает `obj= "LocalSystem"` на случай смены A→B при апгрейде). `GrantServiceAccountRights`
    (только режим A): `icacls "%ProgramData%\MitLicenseCenter" /grant "<аккаунт>":(OI)(CI)M` на key ring (право
    Log-on-as-a-service SCM выдаёт сам при валидном `sc config obj=/password=`; иначе оператор — `secpol.msc`).
    Firewall и `Urls` — на выбранный порт (хардкод 8080 убран; старое одноимённое правило снимается в
    `ssPostInstall` перед `add` — идемпотентность при смене порта).
  - **Экранирование.** `JsonEscape` (бэкслеши + двойные кавычки для JSON-значений — инстанс `.\SQLEXPRESS`,
    пароли), `CmdQuoteInner` (удвоение `"` для значений внутри кавычек в командной строке `sc`/`netsh`/`icacls`).
    Пароли нигде не логируются.
  - **Канон.** `docs/DECISIONS.md` **ADR-31** переписан: лид (конфиг из ввода мастера + выбранный порт);
    два новых буллета вместо «Service account = LocalSystem»/«Default config, not a wizard prompt» — «Interactive
    wizard collects credentials + network (MLC-101)» (два режима, предусловие — оператор создаёт аккаунт заранее,
    права `sysadmin`, тест подключения) и «Config + service from wizard input» (генерация, ACL, плейнтекст под ACL)
    + «Connection test = PowerShell, not a new CLI verb»; буллет апгрейда (skip-if-exists + re-apply account/port);
    Rejected (установщик не создаёт принципала; нет нового CLI-verb). `docs/OPERATIONS.md` секция «GUI installer» —
    буллеты «Precondition — create the SQL principal first», «Wizard steps», обновлённые «What it installs»/«Upgrade».
    `scripts/build-installer.ps1` `.DESCRIPTION` + `CLAUDE.md` команда — описание мастера. `docs/PROJECT_BACKLOG.md`
    — `MLC-101` → Done (сжатая строка), NEXT очищен, трек не закрыт (4/6).
  - **Проверка.** ISCC компилирует `installer\MitLicenseCenter.iss` без ошибок → `Setup.exe`. `scripts\build.ps1
    -Configuration Release` — non-smoke зелёные (C# не менялся); smoke `RacExecutableSmokeTests` зависят от живого
    1С RAS — environmental. Реальный тест-инсталл с вводом creds — приёмочный шаг оператора (инвазивно, не делалось).
  - **Вне scope.** Показ первого пароля `admin` на финальном экране мастера — `MLC-102` (сейчас — из Event Log);
    деинсталл-полировка (keep-data prompt, ярлыки) — `MLC-103`.

- `MLC-102` — **Пароль admin задаёт оператор в мастере (вместо случайного)** — Done (2026-06-11).
  - **Проблема.** До этого `IdentitySeeder` на пустой БД всегда генерил случайный 24-символьный пароль и писал его
    в лог (`EventId 1001`) — оператор доставал из Event Log. Решение оператора: мастер установщика спрашивает пароль
    admin, сидер берёт его при первом старте. Random+лог остаётся fallback для не-инсталляторных путей (dev, `db-reset`,
    ручной деплой).
  - **Backend (`IdentitySeeder.cs`).** В `EnsureSeededAsync`: резолвим путь одноразового файла из
    `IConfiguration["Seed:InitialAdminPasswordFile"]` (дефолт = `Path.Combine(CommonApplicationData,
    "MitLicenseCenter", "initial-admin.secret")` — та же прод-конвенция, что и key ring). При сидинге (нет
    пользователей): если файл есть и непустой → читаем (Trim), используем как пароль, **best-effort удаляем** файл
    (try/catch на `IOException`/`UnauthorizedAccessException`), логируем `LogSeededAdminWithOperatorPassword`
    (новый `[LoggerMessage]` **EventId 1002**, Warning, БЕЗ значения пароля); иначе → прежняя ветка
    `GenerateInitialPassword()` + `LogSeededAdmin` (EventId 1001). Невалидный по политике пароль → существующий
    `ThrowIfFailed` (fail-fast). **Cleanup:** если пользователи уже есть (сидинг не нужен), но файл существует →
    удаляем его best-effort (не оставлять висящий секрет; ранний `return`). Файл читается-удаляется даже если
    содержимое пустое (одноразовый контракт). Dev/тесты не задеты: по дефолтному ProgramData-пути файла нет → random.
  - **Тесты (`IdentitySeederTests.cs`, +4).** Реальный `UserManager`/`RoleManager` над EF InMemory + `IConfiguration`
    с конфиг-ключом на temp-файл (харнес `SeederHarness`, передаёт корневой провайдер — сидер сам делает scope):
    (1) валидный пароль в файле → admin создан с ним + файл удалён; (2) файла нет → random (заданный пароль не
    подходит); (3) невалидный по политике (`"short"`) → `InvalidOperationException` + файл всё равно удалён;
    (4) повторный прогон с висящим файлом при наличии юзера → файл удалён, ровно 1 пользователь (второй admin не создан).
  - **Установщик (`installer/MitLicenseCenter.iss`, UTF-8 BOM).** Новая страница `PageAdmin` «Учётная запись
    администратора» (`CreateInputQueryPage` после `PageNet`): пароль (`IsPassword`) + подтверждение, подсказка
    политики. `ShouldSkipPage(PageAdmin.ID) = ServiceExists` — на апгрейде admin уже есть, страницу пропускаем.
    Валидация на Next (`NextButtonClick`): непустой, == подтверждению, `AdminPasswordMeetsPolicy` (Pascal посимвольно:
    длина ≥12 + наличие upper/lower/digit/special). `WriteInitialAdminPassword` в `CurStepChanged(ssPostInstall)`
    (только при `not ServiceExists`, **ДО** `GrantServiceAccountRights` — чтобы `icacls (OI)(CI)M` на каталог накрыл
    и файл): `SaveStringToFile({commonappdata}\MitLicenseCenter\initial-admin.secret, AdminPassword, False)` (UTF-8
    без BOM; каталог уже в `[Dirs]`). Финальный экран (`CurPageChanged(wpFinished)`): чистая установка — «Панель
    установлена… Войдите как admin с заданным паролем» + URL `http://localhost:<port>/` (`PanelUrl`); апгрейд — текст
    без пароля. `[Run]` postinstall-чекбокс «Открыть панель в браузере» (`{code:PanelUrl}`, `shellexec nowait
    skipifsilent`). Пароль нигде не логируется; на апгрейде `.secret` не пишется.
  - **Контракт одноразового файла.** Установщик (чистая установка) пишет `%ProgramData%\MitLicenseCenter\initial-admin.secret`
    (плейнтекст, UTF-8 без BOM, под ACL каталога). Сидер бэкенда на **первом старте** читает, создаёт admin, **удаляет**
    файл (best-effort) — транзиентный. Cleanup: если admin уже есть, висящий файл тоже удаляется. Пароль не попадает
    в лог (EventId 1002 фиксирует только факт). На апгрейде файл не создаётся (страница пропущена).
  - **Канон.** `docs/DECISIONS.md` **ADR-31** — новый буллет «Admin password set in the wizard (MLC-102)»
    (страница только чистая установка, одноразовый файл, сидер использует+удаляет, EventId 1002 без значения,
    random+1001 fallback, cleanup-контракт, конфиг-ключ, finish без пароля). `docs/OPERATIONS.md` — буллет
    «First-run admin password» (инсталляторный путь через `.secret` без Event Log vs fallback random+1001),
    «Wizard steps» шаг (5) + finish, «Diagnosing service start» (пароль из мастера, Event Log не нужен).
    `docs/PROJECT_BACKLOG.md` — `MLC-102` → Done (сжатая строка + гоча одноразового файла), NEXT очищен, трек не
    закрыт (5/6).
  - **Проверка.** `scripts\build.ps1 -Configuration Release` — non-smoke зелёные, включая 4 новых `IdentitySeederTests`;
    smoke `RacExecutableSmokeTests` зависят от живого 1С RAS (environmental, не блокер). `scripts\build-installer.ps1`
    → ISCC компилирует `.iss` без ошибок → `Setup.exe`. Реальный тест-инсталл — приёмочный шаг оператора.
  - **Вне scope.** Деинсталл-полировка (keep-data prompt: предложить удалить БД + key ring; ярлыки меню) — `MLC-103`.

- `MLC-103` — **Деинсталляция + полировка установщика (закрытие трека 6/6)** — Done (2026-06-11).
  - **Задача.** Последняя задача трека «GUI-установщик». Довести деинсталляцию и UX: keep-data prompt при
    удалении (удалять ли ключи `%ProgramData%`), ярлык меню «Пуск» на URL панели, лог установки. Только
    `installer/MitLicenseCenter.iss` (backend не менялся). До этого uninstall сносил службу + firewall и
    жёстко оставлял БД + key ring без выбора; ярлыков и лога не было.
  - **Keep-data prompt (`CurUninstallStepChanged(usUninstall)`).** Если каталог `{commonappdata}\MitLicenseCenter`
    существует — `MsgBox(mbConfirmation, MB_YESNO or MB_DEFBUTTON2)` (дефолтная кнопка — **Нет** = сохранить):
    спрашиваем, удалять ли конфиг + **ключи шифрования** из `%ProgramData%\MitLicenseCenter`. Текст-предупреждение:
    без ключей секреты в `dbo.Settings` расшифровать нельзя, удалять только если БД тоже выводится из эксплуатации
    (ключи + БД — единый бэкап-юнит, ADR-15); БД SQL установщик **не трогает** (ручное удаление). При «Да» →
    `DelTree(dataDir, True, True, True)`; при «Нет» → каталог нетронут (поведение прежнего `uninsneveruninstall`,
    теперь по выбору оператора). Каталога нет → `Exit` без вопроса.
  - **Ярлык меню «Пуск» (`CreateStartMenuShortcut`, вызов в `ssPostInstall`).** `ForceDirectories(
    {commonprograms}\MitLicense Center)` + `SaveStringToFile(…\MitLicense Center.url, '[InternetShortcut]'#13#10
    'URL='+PanelUrl('')#13#10, False)` — интернет-ярлык на `http://localhost:<port>/` (порт из ввода мастера,
    реюз `PanelUrl`/`NetPort` из MLC-101/102). Через ручную запись `.url`, т.к. Inno `[Icons]` не умеет
    динамический URL. Ошибка записи не критична (`Log`, установка состоялась). Удаление — секция
    `[UninstallDelete]` `Type: filesandordirs; Name: {commonprograms}\MitLicense Center` (сносит ярлык + каталог).
  - **Лог установки.** `[Setup]` `SetupLogging=yes` — Inno пишет лог каждой установки/апгрейда во временный
    каталог (`%TEMP%\Setup Log*.txt`) для диагностики у оператора.
  - **Сохранено без изменений.** `[UninstallRun]` стоп+delete службы + удаление firewall-правила (RunOnceId);
    мастер MLC-101/102, генерация `appsettings.Production.json`, апгрейд поверх (`AppId`/`PrepareToInstall`),
    `initial-admin.secret`, ACL key ring, `UninstallDisplayName`/`UninstallDisplayIcon`. `.iss` остаётся UTF-8 BOM.
  - **Канон.** `docs/DECISIONS.md` **ADR-31** буллет «Uninstall is conservative (MLC-103)» — keep-data prompt
    (дефолт сохранить, предупреждение про бэкап-юнит), БД не трогается, ярлык «Пуск», `SetupLogging=yes`.
    `docs/OPERATIONS.md` секция «GUI installer» — новый буллет «Start menu + install log» + обновлён «Uninstall»
    (теперь спрашивает про ключи). `docs/PROJECT_BACKLOG.md` — `MLC-103` → Done (сжатая строка), NEXT очищен;
    **трек помечен завершённым 6/6, ожидает закрытия куратором** (перенос секции в архив + строка «Завершённые
    треки» + счётчик — отдельный ход куратора).
  - **Проверка.** `scripts\build.ps1 -Configuration Release` — non-smoke зелёные (backend не менялся; smoke
    `RacExecutableSmokeTests` — environmental, зависят от живого 1С RAS). `scripts\build-installer.ps1` → ISCC
    компилирует `.iss` без ошибок → `Setup.exe`. Реальная установка/удаление с проверкой prompt'а и ярлыка —
    приёмочный шаг оператора.

## Трек «Нераспределённые базы: discovery-first добавление» — секция реестра (закрыт 2026-06-11, перенесено из PROJECT_BACKLOG.md)

**Вводная.** Базы кластера 1С, не заведённые в панель, невидимы оператору, а их сеансы
молча отбрасываются реконсиляцией (`ReconciliationJob` пропускает несовпавшие
`ClusterInfobaseId`) и **не считаются ни в чей лимит лицензий**. Трек делает их видимыми
(баннер-счётчик на «Базах»), разбираемыми (диалог: назначить клиенту / скрыть служебную)
и переводит «Добавить базу» на discovery-first флоу. Слепая зона «обратного дрейфа»
(база удалена из кластера, но жива в панели) — **не в этом треке**; обязательность её
решения зафиксирована в `ROADMAP.md` («Дрейф панель↔кластер»). Полная спека трека +
макет — план-файл `C:\Users\andre\.claude\plans\glittery-floating-turtle.md`.

**Решения куратора (2026-06-11, не пересматривались):**
- `Infobase.TenantId` остаётся NOT NULL — состояния «база без клиента» в панели нет;
  «назначить» = создание через существующую форму с префиллом.
- `GET /api/v1/infobases/unassigned` (Admin-only): diff `IClusterClient.ListInfobasesAsync()`
  минус заведённые `ClusterInfobaseId` минус скрытые. Ответ
  `{ Items[], HiddenItems[], Available, Error, CheckedAtUtc }`; RAS недоступен →
  `Available:false` (не пустой список), ошибка санитизируется (паттерн discovery).
- Кэш серверный, TTL **60 с** (константа, НЕ ключ настроек); `?refresh=true` — мимо кэша.
- Игнор-лист: таблица `HiddenClusterInfobases` (`ClusterInfobaseId` PK, `Name`-снапшот,
  `HiddenAtUtc`, `HiddenBy`); `POST/DELETE /api/v1/infobases/unassigned/{id}/hide`.
  Гарды: hide заведённой → 409; повторный hide → 409; unhide несуществующей → 404;
  создание Infobase с `ClusterInfobaseId` из игнор-листа — строку удалить.
- Аудит: `UnassignedInfobaseHidden = 14`, `UnassignedInfobaseUnhidden = 15` (группа
  Infobase; int заморожены).
- UI Admin-only (Viewer не видит ничего нового, endpoint закрыт); строго канон 06:
  shadcn-only, lucide (`AlertTriangle`), семантика `warning`, словарь §12 («Назначить»,
  не «Завести»), `<RelativeTime>`-свежесть. Баннер на «Базах» только при
  `available && count > 0`. «Добавить базу» = discovery-first **только на «Базах»**
  (fallback «Ввести вручную»; при `available:false` — сразу форма); `/tenants/:id` не трогать.
- Валидация инфобаз и каталог настроек **не менялись**.

**Ограничения трека (входили в каждую постановку):** каждая задача оставляет панель
полностью рабочей; объём строго по постановке, без попутных рефакторингов; миграции
нормализовать (UTF-8 без BOM + LF); обе роли проверяются; live-прогон на стенде;
найденные кандидаты — записью в реестр, не в тот же ход.

**Темп исполнения:** две сессии — сессия 1 = `MLC-092` (PR кода BE, #85), сессия 2 =
`MLC-093` (PR кода FE, #87) → после вливания `MLC-094` (PR канона+ADR, #88).
Постановочные PR куратора: #84 (открытие трека), #86 (NEXT TASK на финал).

**Задачи трека (Done-строки на момент закрытия, дословно; полные отчёты — секция «отчёты задач» выше):**

- `MLC-092` (UB-A) · Backend · M · **Done (2026-06-11)** — endpoint unassigned + игнор-лист
  (BE). Сущность `HiddenClusterInfobase` + reversible-миграция; `GET …/unassigned`
  (diff − заведённые − скрытые; серверный TTL-кэш 60 с, `refresh=true`); `POST/DELETE …/hide`
  + гарды (409/404/400) + аудит 14/15; чистка игнор-листа в `CreateAsync`. `dotnet test`
  610 зелёных; миграция на стенде, бэкап `MitLicenseCenter_20260611_pre-mlc092.bak`.
  Полный отчёт + live — в `PROJECT_BACKLOG_ARCHIVE.md`. **Гочи для `MLC-093` (FE):**
  ① ответ — `{ Items[], HiddenItems[], Available, Error, CheckedAtUtc }`; `Items` =
  `{ clusterInfobaseId, name, description }` (**`description` nullable** — API опускает
  null-поля: Zod `.nullish()`, урок MLC-067/071), `HiddenItems` плюс `hiddenAtUtc`/`hiddenBy`.
  ② `available:false` ⇒ `Items` пуст, но `HiddenItems` приходят (блок «Скрытые» рендерить
  всегда); баннер прятать при `!available || items.length===0`. ③ Серверный кэш — главный
  (малый `staleTime`); `?refresh=true` для кнопки «Обновить»; инвалидировать query после
  create/hide/unhide. ④ hide шлёт `{ name }`-снапшот в body; коды конфликтов —
  `UNASSIGNED_ALREADY_ASSIGNED`/`UNASSIGNED_ALREADY_HIDDEN` (409), unhide unknown → 404.
  ⑤ Весь слайс Admin-only (Viewer → 401/403) — баннер/диалог под гейтом `isAdmin`.
- `MLC-093` (UB-B) · Frontend · M · **Done (2026-06-11)** — баннер + диалог + discovery-first
  добавление (FE). Хук `useUnassignedInfobases` (серверный TTL-кэш главный, малый staleTime;
  `?refresh=true` мимо кэша через ref-флаг; hide/unhide + инвалидация; Zod-схема с
  `omittable(description)`); `UnassignedBanner` (warning amber, `AlertTriangle`, счётчик +
  `<RelativeTime>` «Проверено», «Обновить»/«Разобрать»; рендер при `available && count>0`);
  `UnassignedInfobasesDialog` (строка = имя + UUID-моно; «Назначить»/«Скрыть»; свёрнутый
  «Скрытые: N» с «Вернуть»; пустое §6/§9; футер «Ввести вручную» + свежесть); префилл
  `InfobaseFormDialog`/`useInfobaseForm` (props `prefill` + guess `databaseName`); «Добавить»
  discovery-first c fallback (только «Базы»; `/tenants/:id` не тронут). i18n §12.
  **347/347 Vitest** + type-check + lint зелёные. PR #87 влит. Полный отчёт — в архиве.
- `MLC-094` (UB-C) · Docs · S · **Done (2026-06-11)** — канон + ADR отдельным PR. **ADR-29
  «Discovery-first infobase adding + cluster-base ignore-list»** (rejected: nullable
  `TenantId`, фоновый снапшот-джоб, ключ TTL; связь ADR-4 read-only / ADR-16); канон `03`
  (сущность `HiddenClusterInfobase`, enum 14/15, 409-коды, биндинг), `04` (endpoint+кэш 60с+
  аудит), `05` §3.2/§3.3 (баннер/диалог/флоу; `/tenants/:id` прежний), `06` §10 (иконки
  `EyeOff`/`RotateCcw`) + §12 (словарь «Разобрать»/«Скрыть»/«Вернуть»/«Ввести вручную»,
  фраза «Проверено N сек назад»), `ROADMAP` направление (а) закрыто / (б) обязательно.
  Present-tense. Полный отчёт — в архиве.

## Трек «Обратный дрейф панель↔кластер» — секция реестра (закрыт 2026-06-11, перенесено из PROJECT_BACKLOG.md)

**Вводная.** База, удалённая из кластера 1С, остаётся в панели «здоровой» записью
(статус «Активна», публикация на месте, сеансов 0) — без сигнала. «Мёртвые души» в учёте
+ кейс пересоздания базы (новый UUID → старая запись висит, сеансы реальной базы не в
лимите). Закрывает направление **(б)** пункта «Дрейф панель↔кластер — решить обязательно»
(`ROADMAP.md`); направление (а) закрыто треком `MLC-092..094`. Read-only наблюдение,
без auto-fix (дух ADR-4). Полная спека — план-файл
`C:\Users\andre\.claude\plans\glittery-floating-turtle.md`, **часть 2**.

**Решения куратора (2026-06-11, не пересматривались):**
- **Без таблиц/миграций/джобов/настроек** — статус на лету из существующего снапшота RAS
  (`UnassignedInfobasesCache`, TTL 60 с); новых спавнов `rac.exe` нет.
- **`GET /api/v1/infobases` не трогать** (критичная Zod-граница). Расширяется
  `GET /api/v1/infobases/unassigned`: третий список `MissingItems[]` — записи панели,
  чьего `ClusterInfobaseId` нет в кластере; элемент
  `{ InfobaseId, TenantName, Name, ClusterInfobaseId }`.
- `MissingItems` считается **только при `Available:true`**; RAS недоступен → пустой список
  (сбой опроса ≠ пропавшие базы). На FE то же: ни баннера, ни меток при `available:false`.
- **Без мутаций и новых аудит-кодов** — удаление записи существующим флоу удаления
  инфобазы (его подтверждение и аудит).
- UI Admin-only, строго канон 06: метка `StatusBadge danger` «Не найдена в кластере» на
  строке (рядом с бейджем «Статус базы», тултип со временем проверки; `danger` = `Drift`
  по 06 §3); красный баннер «N баз не найдены в кластере» рядом с жёлтым (тот же слот,
  тот же query, `AlertTriangle`); диалог по «Показать» (клиент · имя · UUID-моно,
  «Удалить» → существующий delete-флоу, инвалидация после).
- Серверный фильтр таблицы по «не найдена» — **не делался** (отложенная опция, по запросу
  оператора).
- Валидация инфобаз, каталог настроек, enum'ы аудита — не менялись.

**Темп исполнения:** одна сессия — `MLC-095`+`MLC-096` (один PR кода #91, коммиты
раздельно) → `MLC-097` (PR канона #92). Постановочный PR куратора — #90.

**Задачи трека (Done-строки на момент закрытия, дословно; полные отчёты — выше в архиве):**

- `MLC-095` (RD-A) · Backend · S · **Done** (2026-06-11, PR #91) — `MissingItems` в ответе
  `/infobases/unassigned`: обратный diff из того же кэшируемого снапшота RAS (записи
  `Infobases`, чьих `ClusterInfobaseId` нет в кластере; join имени клиента), только при
  `Available:true`, иначе пусто. DTO `{ InfobaseId, TenantName, Name, ClusterInfobaseId }`;
  общая проекция `PanelInfobaseRow` питает оба diff'а одним join'ом. `GET /infobases` не
  тронут. +6 тестов слайса (613/613). Гоча следующим: response-record расширен 3-м списком —
  все конструкторы `UnassignedInfobasesResponse` теперь с `MissingItems`.
- `MLC-096` (RD-B) · Frontend · S/M · **Done** (2026-06-11, PR #91) — Zod `missingItems`
  (non-null); `MissingInfobasesBanner` (danger) + `MissingInfobasesDialog` (клиент·имя·UUID,
  «Удалить» → существующий delete-флоу) в слоте рядом с жёлтым баннером, тот же query; метка
  `StatusBadge danger` «Не найдена в кластере» в `InfobaseRow` по membership-набору
  `Set<clusterInfobaseId>`. `DeleteInfobaseDialog` принят к контракту `{ id, name }` (запись
  дрейфа может быть на другой странице). Гейтинг `isAdmin && available && count>0`. +8 тестов
  Vitest (355/355), type-check/lint зелёные. i18n `infobases.missing.*`.
- `MLC-097` (RD-C) · Docs · S · **Done** (2026-06-11, PR #92) — ADR-29 Update-нота (направление
  (б) тем же наблюдательным механизмом, новый ADR не нужен); канон `04` (форма ответа +
  reverse diff + условие `Available`), `05` §3.3 (метка/баннер/диалог), `06` §12 («Не найдена
  в кластере», «Показать»); `ROADMAP.md` — «Дрейф панель↔кластер» закрыт полностью (оба
  направления). Present-tense.

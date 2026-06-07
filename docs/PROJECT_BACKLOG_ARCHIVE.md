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
- **Открытый риск (зафиксирован).** connstr-аутентификация: если серверная ИБ требует админа ИБ/кластера,
  webinst может потребовать `Usr=;Pwd=`. MVP публикует без auth — добавить вторым шагом (переиспользовав
  `OneC.Cluster.AdminUser/Password`) при появлении триггера.
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

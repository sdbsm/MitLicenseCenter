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

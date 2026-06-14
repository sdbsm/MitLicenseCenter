# MitLicense Center — Roadmap

Документ описывает актуальные планы развития и отложенный бэклог.
Горизонт — пострелизный цикл (v1.x и далее). Продуктовый контекст —
`01_OVERVIEW.md`; архитектурные ограничения — `DECISIONS.md`.

---

## Актуальные планы

### 1. Движок таблиц @tanstack/react-table

Библиотека `@tanstack/react-table` добавлена в зависимости, но не используется
в коде — таблицы собраны вручную на `shadcn/ui Table`. До перевода таблиц
на движок недоступны:

- меню видимости колонок;
- density-toggle (компактно / комфортно, с сохранением в `localStorage`);
- сериализация активных фильтров в URL (шаринг отфильтрованного вида ссылкой).

Любой из пунктов куратор может выделить в отдельную задачу в `PROJECT_BACKLOG.md`.

### 2. Графики на дашборде (recharts)

`recharts` подключена и используется на страницах «Отчёты» (`/reports`) и
«Быстродействие» (`/performance`). Дашборд (`/`) остаётся на карточках
и прогресс-барах. Перевод метрик дашборда на графики — опция по запросу оператора.

### 3. ESLint-правило для StatusBadge

`StatusBadge` — единственный способ отображать статусы в UI; правило его применения
поддерживается ревью, не линтером. Запланировано: кастомное ESLint-правило,
форсящее `StatusBadge` вместо сырых цветовых классов.

### 4. RAS Strategy B (оптимизация)

Текущее состояние: `rac.exe` запускается как отдельный процесс на каждый цикл.
Кросс-вызовой кэш UUID кластера уже сократил steady-state spawn rate примерно
вдвое; бюджет ≤ 26 proc/min выдерживается с запасом.

Следующий шаг — переход на долгоживущий TCP-сокет на порту 1545 (Strategy B).
Открывается после замера реальной латентности на production-нагрузке.

### 5. Multi-cluster / multi-node топология

Все адаптеры предполагают single-node (зафиксировано ADR-28). Расширение требует
полного пересмотра каждого адаптера и операционной модели. Брать только при явном
запросе со стороны оператора и после отзыва ADR-28.

### 6. Серверный фильтр «Базы» по метке «не найдена в кластере»

Метка «Не найдена в кластере» уже присутствует на странице Баз. Серверный фильтр
(join снапшота RAS в списочный запрос) делает отфильтрованный список быстрым при
большом числе баз. Брать по запросу оператора.

---

## Backlog / deferred — пострелизный рефакторинг R1–R12

> **Первоисточник и детальные обоснования:** `audit/2026-06/TECH-DEBT.md`.
> Каждый пункт таблицы реестра содержит код finding-а (SEC-01, BE-03 и т.д.),
> приоритет, риск и ссылку на аудит-чат-источник.
>
> **Статус (2026-06-13):** пред-релизный трек `MLC-108..117` и этап D1 (переписывание
> канона от кода) уже закрыли часть остатка. Ниже отмечено: ✅ — закрыто, **Остаток** —
> что осталось. Полностью закрыты **R1**, **R2** (`MLC-118`), **R3** (`MLC-119`), **R4** (`MLC-120`),
> **R5** (`MLC-121`), **R6** (`MLC-122`), **R7** (`MLC-123`), **R8** (`MLC-124`), **R9** (`MLC-125..127`),
> **R10** (этап D1 + верификация `MLC-128`), **R11** (`MLC-129..133`) и **R12** (`MLC-134..139`).
> **Весь трек R1–R12 закрыт полностью (`MLC-118..139`, 2026-06-13..14).**

Итерации упорядочены по приоритету и зависимостям. Дать старт любой итерации
может только куратор через постановочный PR в `PROJECT_BACKLOG.md`.

### R1 — Критический security-барьер ✅ ЗАКРЫТО

**Закрыто пред-релизным треком:** SEC-01 (`MLC-109` — SecurityStampValidator + немедленный
отзыв сессий); KEYRING/DOC-01/REL-04 + SEC-03/04 (`MLC-110` — решение «переносимый plaintext +
NTFS ACL», ADR-8 переписан честно, ACL установщика на `keys` и конфиг в обоих режимах);
SEC-02/REL-07 (`MLC-110` — `icacls` на `appsettings.Production.json`). Остатка нет.

### R2 — Валидационный барьер ✅ ЗАКРЫТО

**Зависимости:** R1 по дисциплине (параллелен).

✅ BE-03 (runtime-валидация `MaxConcurrentLicenses`) — закрыто `MLC-114`.
✅ BE-04 / FE-16 (max-длины строк в едином хелпере); SEC-11 / SEC-12 / UX-11 (path/connstr-метасимволы VirtualPath / PhysicalPathOverride / DatabaseName); BE-07 / SEC-13 (`;`/`=`/`"` в имени инфобазы → connstr) — закрыто `MLC-118` (синхронно BE + FE, parity-тесты, канон 03 §3.5).

### R3 — Аудит-целостность

**Зависимости:** независима (после R2 по смыслу).

✅ BE-10 (аудит неудачных входов) — закрыто `MLC-114`.
✅ BE-01 (атомарный аудит — enlist-примитив `IAuditLogger.Enlist`; два инвертированных call-site'а `TenantsEndpoints`/`InfobasesEndpoints` `DeleteAsync` теперь коммитят аудит одним `SaveChanges` с удалением); BE-11 (оживлено событие `LimitChanged = 201`); BE-25 (батчинг аудит-записей в `KillEnforcer` — один `SaveChanges` за цикл) — закрыто `MLC-119`.

**R3 закрыт полностью.**

### R4 — Тестовые слепые зоны ✅ ЗАКРЫТО

**Зависимости:** независима; лучше ставить до следующих рефакторингов.

✅ FE-01 (тесты `ChangePasswordForm` + ForcePasswordChange) — закрыто `MLC-115`.
✅ BE-09 (поведенческие тесты `AppendPublicationFieldErrors` + `UpdateAsync`); BE-12 (юнит-тест CP866/OEM-декода + контрпример mojibake); BE-14 (полный freeze-тест `AuditActionType` — все 43 члена + reflection-инвариант полноты); BE-24 (`TaskCompletionSource` вместо `Task.Delay`; timezone-assert/smoke-пустышка не обнаружены); FE-11 (kill, LoginPage, IIS-подсекция с прогоном Zod); FE-19 (поведенческий `StatusBadge` + толерантная Zod-граница `dashboard/summary` с wire-fixture omit-null) — закрыто `MLC-120`.

**Хвост (вне R4):** BE-15/DOC-32 — стейл-комментарий «UTF-8» в `SystemProcessRacRunner` при фактическом OEM/CP866-декоде; тест BE-12 защищает от действий по нему, правка комментария — в остатке R10.

**R4 закрыт полностью.**

### R5 — Офлайн-UX и обратная связь ✅ ЗАКРЫТО

**Зависимости:** R2 по части форм с inline-ошибками.

✅ UX-03 + FE-05 (различение 401/сеть/схемы: класс `ApiNetworkError`, `classifyError` на `QueryCache`/`MutationCache` — сетевой сбой → глобальный баннер `errors.network`, `ApiSchemaError` → обособленный `[ApiSchemaError]`-лог; `ProtectedRoute` больше не разлогинивает молча при нет-связи/схеме — экран «Повторить»); UX-04 (`applyFieldErrors` — единый разбор 400 ValidationProblem в inline-ошибки полей, порядок 409-code→400-field→toast; inline на экране входа); UX-17 (RAS-карточка — видимая подсказка + ссылка в «Параметры» при `!healthy`); UX-44 (текст ошибки публикации без технического ключа `OneC.RAS.Endpoint`) — закрыто `MLC-121`.

**R5 закрыт полностью.**

### R6 — Видимость лимитов ✅ ЗАКРЫТО

**Зависимости:** R5 по инфраструктуре уведомлений; R3 по BE-11.

✅ UX-02 (единый визуальный язык квоты — `lib/quota.ts` как единственный источник порогов 75/90 + severity→`StatusBadge`/прогресс-бар; акцент нарушителя на `/tenants`, карточке клиента и `/reports`, дашборд переведён на тот же хелпер); FE-03 (`useUpdateTenant` инвалидирует `[tenantsQueryKey, reportsQueryKey]`; `reportsQueryKey` вынесен в `reportsQueryKeys.ts` против цикла импорта); UX-46 (ссылка на карточку клиента из drill-down `/reports`; список и дашборд уже вели на `/tenants/:id`) — закрыто `MLC-122`. Потребление на клиента — live-оверлей на FE из снапшота сеансов (`useTenantConsumption` → `buildConsumedByTenant`, дубль канонического `LicenseConsumption.CountByTenant`); DTO клиента не расширялся, backend не тронут.

**R6 закрыт полностью.**

### R7 — Устойчивость джобов и службы ✅ ЗАКРЫТО

**Зависимости:** независима.

✅ BE-05 (per-item catch в `PublicationStatusRefreshJob`) — закрыто `MLC-114`.

✅ BE-19 (TTL-reaper зависших `Running`-бэкапов в насосе — `BackupOrchestrator.ReapStuckRunningAsync` каждый 5-сек тик, `Running` старше 6ч → `Failed`/`TimedOut` + снятие in-memory замка-на-базу; race-guard в `CompleteAsync`); BE-20 (**мисконцепция аудита** — `CancellationToken.None` это идиоматический Hangfire-плейсхолдер, подменяется реальным shutdown-токеном; регистрации не трогали, зафиксировано комментарием + характеризующим тестом); BE-21 (осознанный `[AutomaticRetry]`: 3+`Fail` для суточного housekeeping, `0` для `publication-status-refresh`/`cold-snapshot`); REL-22 (`Failed`-джобы Hangfire истекают за 30д; раздел «Джобы и устойчивость» в `OPERATIONS`); REL-03 (recovery-политика службы `sc failure` — основной механизм + производная **локальная** SQL-зависимость; жёсткий `depend=MSSQLSERVER` отвергнут — ломает именованные/удалённые инстансы) — закрыто `MLC-123`. Канон — **ADR-40** + расширенный **ADR-35** + `OPERATIONS` §1/§7.

**R7 закрыт полностью.**

### R8 — Релизный конвейер ✅ ЗАКРЫТ

**Зависимости:** независима (параллельна R7).

✅ REL-01 (очистка `OutputDir` в `publish-release.ps1` + sanity-чек состава) — закрыто `MLC-108`.

✅ DOC-08 (ключ `Urls: http://+:8080` в шаблоне `appsettings.Production.json` для ручного деплоя — без него Kestrel слушал бы дефолтный `localhost:5000`; GUI-инсталлятор пишет `Urls` сам, не затронут); REL-13 (`[InstallDelete]` чистит `{app}\wwwroot\assets\*` перед раскладкой новой версии — старые хэшированные SPA-ассеты не накапливаются; key ring/конфиг/БД вне `{app}\wwwroot` — не затронуты); REL-20 (личный путь к ISCC убран из `build-installer.ps1` — резолв `-IsccPath` → `$env:ISCC_PATH` → PATH → стандартные каталоги); REL-12 (новый workflow `release.yml`: `workflow_dispatch` + push тега `v*` → `build-installer.ps1` → Setup.exe-артефакт — CI-гейт упаковочного пути); REL-14 / REL-21 (`dependabot.yml` NuGet/npm/Actions weekly; `ci.yml` получил `format:check` фронта, информационные аудиты `dotnet list package --vulnerable` / `pnpm audit`, paths-фильтры для пропуска чисто-doc; SDK-чеклист перед релизом в `DEVELOPMENT`) — закрыто `MLC-124`. Канон — **ADR-14** update-нота + `DEVELOPMENT` §5/§6 + `OPERATIONS` §2. **Гоча (биллинг):** Actions красный — YAML авторинг, вступает в силу при включении биллинга; гейт остаётся локальный `build.ps1` + ручной прогон релизного пути.

**R8 закрыт полностью.** Хвост (не блокер): per-job paths-split (backend-job не гонять на чисто-frontend и наоборот) сделан частично — только doc-skip верхнего уровня; полный per-job фильтр требует `dorny/paths-filter` (отложено, Actions всё равно не работает по биллингу).

### R9 — Инсталлятор hardening ✅ ЗАКРЫТО (3/3 кластера)

**Зависимости:** R1 по ACL-части; остальное независимо. Крупная задача — разбита куратором на
кластеры (security-барьер бэкенда / чистка поставки+firewall / аккаунт службы).

✅ **Кластер 1 — security-барьер бэкенда** — закрыто `MLC-125` (2026-06-14): SEC-07 (security-заголовки
CSP/X-Frame-Options/X-Content-Type-Options/Referrer-Policy — middleware `SecurityHeaders`, CSP-исключение
`/api/docs`, **ADR-41**); SEC-08 (rate-limiting на `/auth/login` — per-IP fixed window 10/мин → 429 поверх
Identity-lockout, **ADR-42**). Покрыто интеграционными тестами через `WebApplicationFactory<Program>`
(новая тест-инфра). Канон — ADR-41/42 + `SECURITY.md` + `02_ARCHITECTURE` + `OPERATIONS`.

✅ **Кластер 2 — чистка поставки + firewall** — закрыто `MLC-126` (2026-06-14): REL-08 (pdb / web.config
подавлены publish-аргументами `DebugType=none`/`DebugSymbols=false`/`IsTransformWebConfigDisabled=true`,
`appsettings.Development.json` удаляется дефензивно; двойной sanity-чек состава — `publish-release.ps1` +
`build-installer.ps1`; `.iss [Files] Excludes` defense-in-depth); SEC-06 (firewall `profile=domain,private`,
не Public; `remoteip` не задаётся — multi-subnet LAN не регрессит); SEC-09 (localhost-bind за реверс-прокси —
**документированная ручная опция** в `OPERATIONS`, не изменение дефолта мастера); REL-17 (**ADR-43** — Inno
Setup свободна для коммерческого использования, ключ не нужен, REL-17 закрыт как «нет проблемы»). Канон —
ADR-43 + `OPERATIONS` (firewall/bind) + `INSTALL`.

✅ **Кластер 3 — аккаунт службы** — закрыто `MLC-127` (2026-06-14): REL-06 (явные требования к
сервис-аккаунту режима A в подписи мастера + пост-установочный чек-лист `INSTALL` §4 — SQL-доступ,
IIS Administrators/`inetsrv\config` read, SeServiceLogonRight; «Проверить подключение» идёт под
установщиком ≠ служба); SEC-05 (**ADR-44** — привилегии повышены по необходимости, low-priv аккаунт с
узким ACE отложен до runtime-трассировки `ServerManager` на стенде). Хвостом устранён doc-divergence
MLC-126: `SECURITY.md` §7/§9 п.4 синхронизированы с firewall `profile=domain,private`. Канон — ADR-44 +
`SECURITY.md` §6/§7/§9 + `INSTALL` §4.

**R9 закрыт полностью.** Отложенный low-priv follow-up (узкий ACE на `inetsrv\config` вместо
Administrators) — триггер: проведена runtime-трасса реальных чтений `ServerManager` на стенде (ADR-44).

### R10 — Ревизия канона ✅ ЗАКРЫТО

**Закрыто этапом D1** (документация переписана с нуля от кода, PR #142): security-слой
(DOC-01 / 02 / 11 / 14 — честный ADR-8, полный `SECURITY.md`, зафиксированное CSRF-решение);
ревизия 02 и 06 (DOC-03 / 12 + рудименты стадии выбора DOC-15..17 — хвосты ADR-4.1 REVOKED
удалены, 02 переписан); restore в OPERATIONS (DOC-09 / REL-05); ADR-3.3/7 (DOC-04 / 05);
frozen-таблица enum (DOC-06); 00_INDEX (DOC-07); прочие DOC-расхождения.

✅ **Остаток (верификация)** — закрыто `MLC-128` (2026-06-14): REL-11 (полнота §3/§4 OPERATIONS
верифицирована — наполнены D1); DOC-10 (коллизия EventId `1002` → `SettingsSeeder` = `1100`,
диапазон по компоненту; таблица EventId OPERATIONS §3 синхронизирована); BE-15/DOC-32 (шапка
`SystemProcessRacRunner` приведена к OEM/CP866 вместо стейл-«UTF-8»; декод не менялся, тест BE-12
сторожит); FE-17/18 (сирота `ComingSoonPage.tsx` + мёртвый ключ `common.comingSoon` удалены).

**R10 закрыт полностью.**

### R11 — Масштабируемость списков ✅ ЗАКРЫТО (4 кластера a–d)

**Зависимости:** независима; ставить после стабилизации схемы API. Крупная задача — разбита
куратором на кластеры. **Текущее состояние:** серверная пагинация уже есть на Audit/Infobases/
Tenants (page/pageSize + `PaginationBar`); R11 добивает остаток.

✅ **Кластер a** — закрыто `MLC-129` (2026-06-14): аудит — текстовый поиск + фильтр по инициатору
  (UX-20, `search`/`initiator` query, plain `Contains`→LIKE), переход на страницу N / первую-последнюю
  (UX-35), searchable-списки действий (UX-37) и клиентов (UX-38, новый `SearchableSelect`). Канон 05 §7.4.
  Урок: EF Core SQL Server не транслирует `Contains` со StringComparison (память).
✅ **Кластер b** — закрыто `MLC-130` (2026-06-14): серверная пагинация `/backups` +
  `/performance/recordings` (BE-17, paged-конверты, FE consume `.items`); поиск клиентов на `/tenants`
  (UX-05 — `search`, plain `Contains`→LIKE + поле поиска). Канон 05.
✅ **Кластер c** — закрыто `MLC-131` (2026-06-14): client-side сортировка/пагинация таблицы сеансов
  (`SessionsTable`/`useSessionsPage`) и диалога «не найдены» (`MissingInfobasesDialog`) — сеансы/missing
  это live-снапшоты; searchable фильтр клиентов сеансов (UX-14/15/38). Frontend-only.
✅ **Кластер d (основная часть)** — закрыто `MLC-132` (2026-06-14): Zod-валидация read-границ фич
  audit/discovery/infobases/publications/iis/reports/settings (omit-null + forward-compatible enum,
  типы `z.infer`); IIS-мутации сознательно без схемы. Канон 05 §5.3.
✅ **Хвост R11d** — закрыто `MLC-133` (2026-06-14): read-границы `useUsers`
  (`userListResponseSchema`) и `useUnassignedInfobases` (`unassignedInfobasesResponseSchema`) уже
  валидировались Zod с MLC-060/MLC-093 — продуктового кода не требовалось; §5.3 дополнен таблицей
  этих предсуществующих границ. **R11 закрыт полностью.**

**Состав:** UX-05 / BE-17 (серверный поиск по клиентам + пагинация `/backups`, `/performance/recordings`); UX-14 / 15 (пагинация и сортировка таблицы сеансов и диалога «не найдены»); UX-20 / 35 / 37 / 38 (аудит: текстовый поиск, фильтр по инициатору, переход на страницу N); FE-09 (схемная валидация ~35 эндпоинтов через Zod вместо `payload as T`).

### R12 — Полировка UX ✅ ЗАКРЫТО (6 кластеров a–f)

**Зависимости:** независима (последний цикл; доступность не зависит ни от чего). Крупная — разбита
куратором на кластеры; одна `NEXT TASK` за раз. Карта реального состояния — разведка куратора
2026-06-14 (часть находок уже была закрыта ранее: **FE-03** и **UX-46** — в `MLC-122`; **FE-08** /
**UX-27** / **UX-30** / **UX-25** / **UX-36** оказались фантомами при audit-first проверке).

✅ **Кластер a** — закрыто `MLC-134` (2026-06-14): FE-02 (`useCreateInfobase`/`useDeleteInfobase`
  инвалидируют `tenantsQueryKey`) + FE-07 (`UserFormDialog` сброс через `useEffect(open→reset)`;
  реальный призрак). FE-08 оказался фантомом (Radix размонтирует `DialogContent` → `ChangePasswordForm`
  сбрасывается сам), FE-03 закрыт ранее в MLC-122. Канон 05 §6.2. Frontend-only.
✅ **Кластер b** — закрыто `MLC-135` (2026-06-14): BE-13 — `PerfRecordingStatus`/`PerfRecordingStopReason`
  получили явные int-значения (= ordinal) + freeze-тесты `PerfRecordingEnumFreezeTests` (InlineData +
  reflection-полнота). Канон 04 §6.5 (сводная таблица 6 frozen-int enum'ов). Миграция не нужна.
✅ **Кластер c** — закрыто `MLC-136` (2026-06-14): BE-02 — optimistic concurrency на `Tenant`
  (rowversion-токен, строго аддитивная миграция `MLC136TenantRowVersion`, `DbUpdateConcurrencyException`→409
  `TENANT_CONCURRENCY_CONFLICT`, parity FE + omit-null). Infobase/Publication — follow-up. Канон 03/04/05.
✅ **Кластер d** — закрыто `MLC-137` (2026-06-14): UX-кластер D — «арендатор»→«клиент»,
  «сессия»→«сеанс», убрана англ. утечка «(recycle)»/выровнен глагол; UX-30 (англ. роли) — фантом (уже
  через `t("users.roles.*")`). Только i18n-тексты.
✅ **Кластер e** — закрыто `MLC-138` (2026-06-14): UX-кластер E — контраст `StatusBadge` поднят до WCAG AA
  (warning/neutral, с замерами), destructive-стиль на `StopRecordingDialog`/`IisConfirmDialog`, русские
  aria-label (pagination/sidebar/dialog). UX-27 — фантом (primary в тёмной теме 15.72:1). Канон 06.
✅ **Кластер f** — закрыто `MLC-139` (2026-06-14): UX-кластер F — UX-39 (кнопка «Отмена» в диалоге
  смены пароля топбара, opt-in проп `onCancel`); UX-25 (бэкап доступен из строки баз И карточки клиента) и
  UX-36 (срок хранения виден в `/settings` + near-cutoff баннер на `/audit`) — фантомы. Frontend-only.

**Состав (исходный):** UX-кластеры D/E/F; FE-07/08 (сброс диалогов); FE-02/03 (инвалидации кэша
счётчиков; FE-03 закрыт в MLC-122); BE-02 (optimistic concurrency — требует миграции); BE-13
(freeze-тесты для enum с `HasConversion<int>`).

**R12 закрыт полностью (6 кластеров a–f, `MLC-134..139`). С этим закрыт весь пострелизный трек
рефакторинга R1–R12 (`MLC-118..139`) — остаточный техдолг пред-релизного аудита отработан.**

---

## Permanently out of scope (ADR-15)

Планировщик резервного копирования, restore-оркестрация внутри панели и встроенная
2FA. Исключение: on-demand `COPY_ONLY`-бэкап (ADR-27). Вернуть любой пункт можно
только явным отзывом ADR-15.

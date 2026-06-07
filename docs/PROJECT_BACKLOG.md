# MitLicense Center — Project Backlog

Единый активный реестр задач. Этот файл **постоянно поддерживается в актуальном состоянии**
и читается первым при каждом следующем запуске работы над проектом.

Полные отчёты и постановки по уже **закрытым** задачам (MLC-001..024) вынесены в
`docs/PROJECT_BACKLOG_ARCHIVE.md` — этот файл держим тонким, чтобы не жечь контекст при
каждом старте. Канон проекта (`docs/01..06 + DECISIONS.md + ROADMAP.md + OPERATIONS.md`) —
источник правды по архитектуре v1. Бэклог не дублирует канон, а фиксирует улучшения поверх него.

## Как пользоваться

1. Прочитать этот файл.
2. Найти задачу, помеченную `NEXT TASK`.
3. Выполнить **только её**.
4. После выполнения: статус `Done`, перенести подробный отчёт в `PROJECT_BACKLOG_ARCHIVE.md`,
   а здесь заменить запись на строку в индексе «Закрыто».
5. Выбрать следующую задачу с максимальным ROI как `NEXT TASK`.
6. Не выполнять больше одной задачи за сессию без отдельного указания.

При обнаружении расхождения кода и документации — не исправлять автоматически:
завести запись `[Doc divergence]`, описать расхождение, предложить варианты.

## Когда заводить запись

Раздел «Как пользоваться» выше — про **исполнение** уже существующих записей; этот —
про **наполнение** реестра. Запись заводят, когда работа выходит за рамки текущей
активной задачи.

**Заводить запись** (кандидаты реестра):
- дефект корректности / безопасности / производительности, найденный вне текущей задачи;
- техдолг или улучшение поддерживаемости с понятным ROI (меньше дублирования / дешевле
  расширять / меньше дрейфа), не являющийся дефектом;
- расхождение код↔документация — обязательно как `[Doc divergence]` с вариантами,
  **не** чинить молча.

**Не заводить запись** (чинить на месте или не трогать):
- тривиальная мелочь по ходу текущей `NEXT TASK`, входящая в её объём, — правится сразу;
- вкусовщина без ROI, гипотезы «можно было бы красивее» без измеримой выгоды;
- то, что уже покрыто каноном `docs/` или `CLAUDE.md`, — в реестре не дублировать.

**Кто создаёт.** Net-new записи и `NEXT TASK` ведёт внешний чат-куратор (держит порядок
исполнения и выдаёт постановку). Чат-исполнитель закрывает одну задачу за сессию и
`NEXT TASK` сам не выставляет; найденные по ходу кандидаты — фиксирует записью и
отчитывается куратору, не берёт их в тот же ход.

## Формат записи

`ID` · `Category` (Architecture / Security / Performance / Maintainability / Testing /
Frontend / Backend) · `Priority` (P1 / P2 / P3) · `Severity` (Critical / High / Medium /
Low) · `Module` · `File(s)` · `Description` · `Impact` · `Recommendation` · `Status`.

**Статусы:** `Open` · `Approved` · `In Progress` · `Done` · `Rejected`.

**Приоритеты:** `P1` — исправить обязательно · `P2` — желательно · `P3` — можно отложить.

## Общая оценка

Зрелый, production-ready проект. Аудит безопасности и дефектов (MLC-001..019, 2026-06-01..03)
**закрыт** — критичных дыр нет, отчёты в `PROJECT_BACKLOG_ARCHIVE.md`.

Реестр техдолга и поддерживаемости (MLC-020..028, анализ 2026-06-03) — **закрыт**: MLC-020..024
выполнены, MLC-025..028 переведены в отложенные опции (брать по триггеру). Это **не** были дефекты
корректности/безопасности (поэтому нет P1), а снижение стоимости будущего развития. Архитектурный
фундамент крепкий (строгие adapter-границы к 1С/IIS, чистые статические хелперы, docs-as-canon) —
менять его не нужно.

**Рефакторинг-трек (открыт 2026-06-03).** Принят отдельный план долгосрочного улучшения
поддерживаемости — полные спеки в `C:\Users\andre\.claude\plans\distributed-orbiting-snail.md`
(ID `REF-01..REF-13`). Net-new задачи получили номера **MLC-029..036**; задачи, совпавшие с уже
отложенными опциями, переиспользуют существующие номера (REF-08→MLC-025, REF-09→MLC-026,
REF-10→MLC-027, REF-11→MLC-011(a), REF-13→MLC-028). Phase 1–2 (MLC-029..035) берутся сразу по
порядку исполнения; Phase 3–4 остаются gated на триггеры. Процесс ведёт внешний чат-куратор
(держит `NEXT TASK`); чат-исполнитель закрывает одну задачу за сессию по штатной дисциплине.

---

## Приоритизация по ROI

`ROI = (выгода для скорости развития: меньше дублирования / дешевле расширять / меньше дрейфа) ÷ трудоёмкость.`

1. ~~**MLC-021**~~ — **Done** (2026-06-03): Web-хелперы (backstop + каталог аудита + initiator).
2. ~~**MLC-022**~~ — **Done** (2026-06-03): единый источник правил валидации (BE `InfobaseValidationRules` + FE `validation.ts` + parity-тесты).
3. ~~**MLC-023**~~ — **Done** (2026-06-03): декомпозиция InfobaseFormDialog (`useInfobaseForm` + `PublicationFieldset` + `mapConflictToField`), поведение 1:1.
4. ~~**MLC-024**~~ — **Done** (2026-06-03): whitelist лицензионных app-id вынесен в `dbo.Settings` (`OneC.LicenseConsumingAppIds`); правка оператором без редеплоя.

Оставшиеся MLC-025..028 — **отложенные опции** (см. ниже), берутся по триггеру, не по умолчанию.

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
`C:\Users\andre\.claude\plans\eventual-hopping-pebble.md`. Берём по одной за сессию:

1. `MLC-057` — **Done (2026-06-08)** — Переключатель темы (light/dark/system): `ThemeProvider` (`next-themes`)
   в `App.tsx` + тумблер `ThemeToggle` в `Topbar` + i18n `theme.*`. Отчёт — в индексе «Закрыто» ниже.
2. `MLC-058` — **Done (2026-06-08)** — Раздел «Администраторы» (Backend API + Frontend UI в одном заходе):
   эндпоинты `/admins` (список/создать/сброс пароля/disable+enable) на готовой Identity, +4 слота аудита
   (103–106), оба guard'а, миграции нет; фронт `features/admins/`, admin-only роут + пункт сайдбара, показ
   временного пароля один раз. Отчёт — в индексе «Закрыто» ниже.

**Трек «Полировка панели v1.1» завершён 2/2 (2026-06-08).**

---

## NEXT TASK

> **Активной задачи нет.** Трек «Полировка панели v1.1» (`MLC-057..058`) **завершён 2/2 (2026-06-08)** —
> обе задачи в индексе «Закрыто» ниже. Следующую `NEXT TASK` выставляет внешний чат-куратор (исполнитель
> сам не выставляет).
>
> Закрытые треки (полировка панели `MLC-057..058`, полировка /settings `MLC-055..056`, отчёты
> `MLC-048..052`/`054`, экспорт `MLC-051..052`, рефакторинг `MLC-029..035`, перф `MLC-037..043`, плюс
> `MLC-044..047`/`053`) — в индексе «Закрыто» ниже и в архиве. **Отложенные опции**
> (`MLC-025/026/027/028/011(a)` + Phase 3–4 рефакторинг-трека / `MLC-036` RAS Strategy B, перф PERF-08+)
> остаются gated на триггеры — анализ 2026-06-08 подтвердил, что триггеры не сработали, по умолчанию не
> берутся.

---

## Открытые опции (deferred — не активные задачи)

Объём = новая работа; брать по появлению **триггера**, не по умолчанию.

- `MLC-006(a)` — OpenAPI-codegen TS-клиента — **промоутнута** в опцию `MLC-025`.
- `MLC-011(a)` — вынести бизнес-правила из Web-эндпоинтов в Application use-cases. Триггер:
  появление **второго потребителя** правил (второй транспорт gRPC/CLI/mass-import или worker
  вне HTTP); см. ADR-20. До триггера — `MLC-021` снимает дублирование внутри Web-слоя без
  введения use-case-слоя.
- `MLC-025` — **OpenAPI-codegen / расширение Zod-границ** (поглощает `MLC-006(a)`). Сейчас типы
  рукописные, Zod на 3 критичных границах (ADR-10.1, MLC-016). Триггер: рост API-поверхности /
  частые расхождения FE↔BE. Даёт инфраструктуру для `MLC-026`. Объём M.
- `MLC-026` — **генерация FE-полей настроек из `SettingDefinitions`**. Сейчас новый ключ касается
  enum + каталога + захардкоженного FE-рендера (`SettingsPage.tsx` `SECTIONS`/`FIELD_META`) + i18n
  (подтверждено на MLC-024). Зависит от `MLC-025`. Триггер: каталог > ~25–30 ключей (сейчас 17).
  Объём S.
- `MLC-027` — **разбить `i18n/ru.json` по фичам** (namespaces i18next). Сейчас один плоский файл,
  RU-only (locked). Триггер: файл > ~1000 строк или появление 2-го локаля. Объём S.
- `MLC-028` — **подготовка к multi-cluster / multi-node** (XL, архитектурная). Допущение
  «один кластер / один узел» зашито во все адаптеры (`ResolveClusterUuidAsync` берёт `records[0]`)
  и single-node маппинг. **НЕ трогать**, пока multi-node не на столе (single-node — locked
  operational constraint, `DECISIONS.md` «Deployment topology»). `IClusterClient` изолирует спавн —
  правильная точка расширения. Gated на смену топологического допущения (требует re-review каждого
  адаптера + контур согласования). Объём XL.

---

## Закрыто (MLC-001..024, 029, 030, 031, 032, 033, 034, 035, 037, 038, 039, 040, 041, 042, 043, 044, 045, 046, 047, 048, 049, 050, 051, 052, 053, 054, 055, 056, 057, 058) — индекс

Полные постановки и отчёты: **`docs/PROJECT_BACKLOG_ARCHIVE.md`**.

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

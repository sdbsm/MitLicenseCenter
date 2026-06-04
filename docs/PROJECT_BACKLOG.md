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

---

## NEXT TASK

> **Нет активных задач.** Рефакторинг-трек Phase 1–2 (MLC-029..035 / REF-01..07) закрыт полностью —
> последним выполнен `MLC-035` (REF-07, группировка `Web/Endpoints` по фиче; см. архив). Остаются только
> **отложенные опции** (`MLC-025/026/027/028/011(a)`, ниже) и **Phase 3–4** рефакторинг-трека
> (`REF-08..13` = `MLC-025/026/027/011(a)/028` + `MLC-036` RAS Strategy B) — все **gated на триггеры**,
> не берутся по умолчанию. Новую `NEXT TASK` не ставить, пока не сработает триггер одной из отложенных
> опций или не появится новая постановка от куратора.

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
  (подтверждено на MLC-024). Зависит от `MLC-025`. Триггер: каталог > ~25–30 ключей (сейчас 14).
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

## Закрыто (MLC-001..024, 029, 030, 031, 032, 033, 034, 035, 037) — индекс

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

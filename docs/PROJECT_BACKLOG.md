# MitLicense Center — Project Backlog

Единый активный реестр задач. Этот файл **постоянно поддерживается в актуальном состоянии**
и читается первым при каждом следующем запуске работы над проектом.

Полные отчёты и постановки по уже **закрытым** задачам (MLC-001..021) вынесены в
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

Текущий реестр (MLC-022..028) — результат **анализа технического долга и поддерживаемости**
(2026-06-03). Это **не** дефекты корректности/безопасности (поэтому нет P1), а снижение
стоимости будущего развития: устранение дублирования бизнес-логики, упрощение точек роста
сложности. Архитектурный фундамент крепкий (строгие adapter-границы к 1С/IIS, чистые
статические хелперы, docs-as-canon) — менять его не нужно; находки точечные.

---

## P2 — желательно (высокий ROI)

### MLC-022 — Единый источник правил валидации Infobase/Publication (FE↔BE)
- **Category:** Maintainability (дублирование правил)
- **Priority:** P2 · **Severity:** Medium
- **Module:** Infobase/Publication (формы + endpoints)
- **File(s):** `frontend/src/features/infobases/InfobaseFormDialog.tsx:56-113` (`buildSchema`,
  `PLATFORM_VERSION_PATTERN`, `GUID_PATTERN`); `backend/.../Endpoints/InfobasesEndpoints.cs:23-24,475-561`
  (`PlatformVersionRegex`, `ValidateInfobase`, `AppendPublicationFieldErrors`)
- **Description:** Один набор правил (regex версии платформы `^\d+\.\d+\.\d+\.\d+$`, virtual-path
  начинается с `/` и без пробелов, max-длины 200/260/50, абсолютность physical-path) описан в
  Zod-схеме фронта **и** в ручных валидаторах бэка (+ прозой в `03_DOMAIN_MODEL.md`). Источника
  правды нет.
- **Impact:** Сейчас консистентно. Любое новое поле/правило = правка 2–3 мест; рассинхрон тихий
  (одна сторона пропустит, другая отклонит). **Probability:** High (формы растут). **Cost if ignored:** M.
- **Recommendation:** Дёшево — централизовать regex/лимиты в общих константах на каждой стороне +
  тест паритета. Стратегически — связать с codegen из `MLC-025`. **Cost to fix:** M.
- **Status:** Open

### MLC-023 — Декомпозиция InfobaseFormDialog (787 строк)
- **Category:** Frontend (сложность React-компонента)
- **Priority:** P2 · **Severity:** Medium
- **Module:** Frontend / Infobases
- **File(s):** `frontend/src/features/infobases/InfobaseFormDialog.tsx`
- **Description:** God-component: режимы create+edit, Zod-схема, 4 discovery-поля, авто-подстановка
  с `touched`-ref + эффект догрузки настроек, точечная проверка занятости кластер-базы, раскрытие
  «Дополнительно», маппинг 409 → ошибки полей. Самый большой и связный компонент проекта.
- **Impact:** Это главная форма продукта; каждый новый атрибут Infobase/Publication оседает здесь,
  стоимость изменения растёт, риск регрессий в prefill-логике — тоже. **Cost if ignored:** M–L.
- **Recommendation:** Декомпозировать без смены поведения: `useInfobaseForm`
  (схема+дефолты+prefill+submit), `PublicationFieldset`, `mapConflictToField`. Discovery-поля уже
  абстрагированы — переиспользовать. Удобно делать вместе/после `MLC-022`. **Cost to fix:** M.
- **Status:** Open

---

## P3 — можно отложить

### MLC-024 — App-id whitelist лицензий → dbo.Settings
- **Category:** Backend / Расширяемость
- **Priority:** P3 · **Severity:** Low
- **Module:** 1C Cluster Adapter
- **File(s):** `backend/.../Clusters/RacExecutableRasClusterClient.cs:28-32` (`LicenseConsumingAppIds`)
- **Description:** Набор client-типов, потребляющих лицензию, — статический `HashSet` в коде.
  Изменение (новый тип клиента 1С, иная политика) = правка кода + редеплой, хотя по природе это
  настройка.
- **Impact:** 1С вводит/переименовывает client-типы; оператор не может подстроить без релиза.
  **Cost if ignored:** S.
- **Recommendation:** Перенести whitelist в `dbo.Settings` (настраиваемый список). Можно отложить
  до первого реального запроса. **Cost to fix:** S.
- **Status:** Open

### MLC-025 — OpenAPI-codegen / расширение Zod-границ (промоут MLC-006(a))
- **Category:** Maintainability / Frontend (связанность FE↔BE)
- **Priority:** P3 · **Severity:** Low
- **Module:** Frontend / API
- **File(s):** `frontend/src/lib/api.ts`, `frontend/src/lib/apiSchema.ts`, `frontend/src/features/*/types.ts`
- **Description:** Осознанный выбор ADR-10.1: типы рукописные, Zod лишь на 3 критичных границах
  (MLC-016). Чем шире API-поверхность, тем выше риск дрейфа на невалидируемых эндпоинтах. Эта
  задача **поглощает** ранее отложенную опцию `MLC-006(a)`.
- **Impact:** При росте команды/числа эндпоинтов ручная синхронизация контракта ловит баги поздно.
  **Cost if ignored:** M.
- **Recommendation:** Внедрить codegen из `/api/docs/v1/swagger.json` и/или расширить Zod-границы.
  Триггер (по ADR-10.1): рост поверхности / частые расхождения. Даёт инфраструктуру для `MLC-026`.
  **Cost to fix:** M.
- **Status:** Open

### MLC-026 — Генерация FE-полей настроек из SettingDefinitions
- **Category:** Maintainability · _(зависит от MLC-025)_
- **Priority:** P3 · **Severity:** Low
- **Module:** Settings (full stack)
- **File(s):** `backend/.../Application/Settings/SettingDefinitions.cs`;
  `frontend/src/features/settings/SettingField.tsx`; `Domain/Settings/SettingKey.cs`;
  `Infrastructure/Settings/SettingsSeeder.cs`; `frontend/src/i18n/ru.json`
- **Description:** Бэк централизован (`SettingDefinitions` ведёт валидацию+сид), но новый ключ
  всё равно касается enum + словаря + FE-рендера поля + i18n. ADR-17 вводит тонкое правило
  «form-prefill ключи не обрастают backend-ридерами» — легко нарушить по незнанию.
- **Impact:** Низкое-среднее (13 ключей, растёт медленно). **Cost if ignored:** S.
- **Recommendation:** Генерировать FE-описания полей из `SettingDefinitions` (через codegen из
  `MLC-025`). Триггер: каталог > ~25–30 ключей. **Cost to fix:** S.
- **Status:** Open

### MLC-027 — Разбить i18n/ru.json по фичам
- **Category:** Frontend
- **Priority:** P3 · **Severity:** Low
- **Module:** Frontend / i18n
- **File(s):** `frontend/src/i18n/ru.json` (540 строк, ~460 ключей)
- **Description:** Все строки UI в одном плоском файле без feature-namespacing. Только русский
  (locked), риск ограничен поиском ключей и merge-конфликтами при росте экранов.
- **Impact:** Низкое. **Cost if ignored:** S.
- **Recommendation:** Разбить по фичам (namespaces i18next). Триггер: файл > ~1000 строк или
  появление второго локаля. **Cost to fix:** S.
- **Status:** Open

### MLC-028 — Подготовка к multi-cluster / multi-node (XL, архитектурная)
- **Category:** Architecture / Масштабируемость
- **Priority:** P3 · **Severity:** Low (наблюдение)
- **Module:** 1C Cluster Adapter + Reconciliation
- **File(s):** `backend/.../Clusters/RacExecutableRasClusterClient.cs` (`ResolveClusterUuidAsync`
  берёт `records[0]` — только первый кластер); все адаптеры; маппинг в `ReconciliationJob.cs`
- **Description:** Допущение «один кластер / один узел» зашито во все адаптеры (резолв одного
  кластера, single-node маппинг). ROADMAP это признаёт. Multi-cluster/multi-node потребует
  переписать каждый адаптер + контур согласования + операционную модель.
- **Impact:** Нулевое сейчас (single-node — **locked** operational constraint, `DECISIONS.md`
  «Deployment topology»). При росте топологии — первый архитектурный потолок. **Cost if ignored
  сейчас:** XS.
- **Recommendation:** **НЕ трогать**, пока multi-node не на столе. `IClusterClient` уже изолирует
  спавн — правильная точка будущего расширения. Делать **последней**; gated на смену
  топологического допущения (требует re-review каждого адаптера). **Cost to fix:** XL.
- **Status:** Open (gated)

---

## Приоритизация по ROI

`ROI = (выгода для скорости развития: меньше дублирования / дешевле расширять / меньше дрейфа) ÷ трудоёмкость.`

1. ~~**MLC-021**~~ — **Done** (2026-06-03): Web-хелперы (backstop + каталог аудита + initiator).
2. **MLC-022** — высокий: убирает тихий дрейф правил при росте форм (M). → **NEXT TASK**.
3. **MLC-023** — средний-высокий: дешевле расширять главную форму (M); делать вместе/после 022.
4. **MLC-024** — средний: настройка лицензий без редеплоя (S).
5. **MLC-025** — средний (по триггеру): меньше ручной синхронизации контракта (M); даёт
   инфраструктуру для 026.
6. **MLC-026** — низкий-средний: зависит от 025 (S).
7. **MLC-027** — низкий: удобство навигации/merge (S).
8. **MLC-028** — низкий сейчас: XL, gated на смену топологии; делать последней.

---

## NEXT TASK

> **MLC-022 — Единый источник правил валидации Infobase/Publication (FE↔BE).**
> Высокий ROI: убирает тихий рассинхрон правил (regex версии платформы, virtual-path,
> max-длины, абсолютность physical-path) между Zod-схемой фронта и ручными валидаторами
> бэка по мере роста форм. Объём M.

---

## Открытые опции (deferred — не активные задачи)

Объём = новая работа; брать по появлению **триггера**, не по умолчанию.

- `MLC-006(a)` — OpenAPI-codegen TS-клиента — **промоутнута** в активную задачу `MLC-025`.
- `MLC-011(a)` — вынести бизнес-правила из Web-эндпоинтов в Application use-cases. Триггер:
  появление **второго потребителя** правил (второй транспорт gRPC/CLI/mass-import или worker
  вне HTTP); см. ADR-20. До триггера — `MLC-021` снимает дублирование внутри Web-слоя без
  введения use-case-слоя.

---

## Закрыто (MLC-001..021) — индекс

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

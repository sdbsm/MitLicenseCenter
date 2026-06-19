# MitLicense Center — фронтенд (SPA)

Гайд по устройству SPA: структура features, сетевой слой, конвенции
TanStack Query, zod/`omittable`, формы, маршрутизация, i18n, тесты.

---

## 1. Стек и версии

| Пакет | Версия (package.json) |
|---|---|
| react / react-dom | ^19.2.6 |
| vite + @vitejs/plugin-react | ^8.0.13 / ^6.0.2 |
| typescript | ^6.0.3 |
| @tanstack/react-query | ^5.100.10 |
| react-hook-form | ^7.76.0 |
| @hookform/resolvers | ^5.2.2 |
| zod | ^4.4.3 |
| radix-ui | ^1.4.3 |
| tailwindcss | ^4.3.0 |
| i18next + react-i18next | ^26.2.0 / ^17.0.8 |
| react-router | ^7.15.1 |
| recharts | ^3.8.1 |
| sonner | ^2.0.7 |
| vitest | ^4.1.7 |

Сборка: `tsc -b && vite build`; тул-чейн: Node ≥ 22.13, pnpm 11.0.8.

---

## 2. Организация исходников

```
frontend/src/
  App.tsx                 — корень: провайдеры QueryClient, I18n, Router, Toaster
  main.tsx                — точка входа
  routes/router.tsx       — createBrowserRouter, lazy-импорты страниц
  features/               — 15 фич (см. ниже)
  components/
    layout/               — AppShell, Sidebar, Topbar, ThemeToggle
    ui/                   — shadcn/radix примитивы (button, dialog, form, …)
    ui/data-table/        — DataTable на @tanstack/react-table + хуки (ADR-46)
    PageFallback.tsx
    PaginationBar.tsx
  lib/
    api.ts                — fetch-обёртка, ApiError, ApiNetworkError, ApiSchemaError
    apiErrors.ts          — matchConflictCode, applyFieldErrors, toastFormSubmitError
    apiSchema.ts          — omittable(), pagedResponseSchema()
    connectionStatus.ts   — module-store «нет связи» + useIsOffline()
    queryClient.ts        — единственный QueryClient + классификация ошибок на кэшах
    useInvalidatingMutation.ts — фабрика мутаций с инвалидацией
    pagination.ts         — pageLinkRange()
    utils.ts              — cn() (clsx + tailwind-merge)
  i18n/
    index.ts              — инициализация i18next (lng: "ru") + сборка словаря из ru/
    ru/                   — UI-тексты, по одному файлу на top-level ключ (common.json, nav.json, …)
  test/setup.ts           — jsdom-заглушки (ResizeObserver, matchMedia, …)
```

### 17 фич (`features/`)

| Фича | Страница / роль |
|---|---|
| `audit` | `/audit` — журнал операций |
| `auth` | `/login`, `ProtectedRoute`, `ForcePasswordChange`. Экраны входа и форс-смены пароля собраны на единых shadcn-примитивах (`Label`/`Input`/`Button`) и используют общий контейнер `AuthCardShell` (центрирование, карточка дизайн-системы, подвал с версией панели из `useHealth` — анонимный `/api/v1/health`, при недоступности строка не рендерится) |
| `backups` | диалог бэкапов на карточке инфобазы |
| `dashboard` | `/` — живой «Обзор»: виджет «Требует внимания», KPI-карточки (кликабельны; спарклайн лицензий + live-точка сеансов), тренд использования лицензий 7д, строка состояния системы (здоровье RAS + хоста + сервера 1С, `ServerHealthCard` — светофор `overall`, ссылка на `/server`), лента свежей активности |
| `discovery` | `DiscoveryField` — общий компонент автоподстановки |
| `health` | версия панели в подвале сайдбара (анонимный `/api/v1/health`) |
| `infobases` | `/infobases` — CRUD инфобаз и публикаций |
| `performance` | `/performance` — метрики хоста, 1С, SQL. Воронка: вердикт «почему тормозит» → светофор ресурсов (`SaturationGauges`) → **drill-down переключатель слоёв** Хост/1С/SQL (`PerformanceDrillDown`, контролируемые shadcn `Tabs`) → единый блок «Блокировки» (`PerformanceBlockingBlock`) → запись по требованию. Слой «Хост» = атрибуция семей процессов, «1С»/«SQL» = собственные live-источники (`OneCLoadSection`/`SqlLoadSection`). Авто-фокус (`useDrillDownFocus`) наводит на релевантный слой по вердикту (`culpritFamily`: OneC→1С, Mssql→SQL, иначе Хост), ручной выбор слоя «прибивает» его — авто-фокус больше не перебивает. Светофор кликабелен: клик по гейджу ресурса наводит drill-down на слой (`layerForResource`) — CPU/RAM ведут на слой доминирующей по ресурсу семьи (как авто-фокус), а Диск всегда на слой SQL (у диска нет атрибуции по семьям, доказательство дисковой нагрузки — IO-stall по базам — живёт в SQL-слое); клик пинит выбор, как ручной выбор вкладки. Все три слоя `forceMount` (неактивные скрыты `hidden`) — их polling 1С/SQL не прерывается в фоне, переключение мгновенно (MLC-207/208). SQL-слой, секция «Конкуренция за ресурсы» (`SqlContentionTables`): топ-ожидания показывают сырой `waitType` (моно) с приглушённой подписью-расшифровкой смысла под ним, когда тип распознан по префиксу (`waitCategory` в `sqlLoad.ts` → ключ `performance.sql.waits.meanings.*`; нераспознанный → без подписи); IO-stall по базам несёт колонку «Клиент» — связка база→инфобаза→клиент через ту же атрибуцию payload (`attributionFor`), что и активные запросы/нагрузка по базам, без BE-правок (MLC-209). Единый блок «Блокировки» (`PerformanceBlockingBlock`) между drill-down и записью сводит в одно место два сигнала контеншена, иначе разнесённые по слоям 1С и SQL: цепочки блокировок SQL (`blockingSessionId` → бейджи «ждёт сеанс N»/«блокирует», `lockChainRows`) и заблокированные сеансы 1С (`blockedByDbms`/`blockedByLs`, `blockedSessions`); читает оба live-источника повторно (общий кэш React Query по ключу — двойного polling нет), рендерится всегда (сквозной сигнал, не зависит от host-снимка); пусто (никто никого не ждёт) → нейтральное «блокировок нет» (хороший случай, без красных акцентов); акценты только через `StatusBadge`, без BE-правок (MLC-210) |
| `profile` | форма смены пароля (в ForcePasswordChange и профиле) |
| `publications` | мутации публикации/смены платформы/проверки IIS |
| `reports` | building-blocks отчётов (графики/сводки/хуки/`reportsUrlState`/`reportsQueryKeys`/`types`/`export`), переиспользуются «Сеансами» (вид «Использование за период»), «Базами» (вкладка «Размер баз») и «Обзором» (тренды). **Своего маршрута и пункта меню НЕТ** — раздел «Отчёты» растворён (MLC-196c, ADR-53); эндпоинты `/reports/*` сохранены |
| `server` | `/server` — раздел «Сервер» (MLC-214/215, ADR-54/55). Viewer наблюдает, Admin управляет сервером 1С и IIS. Экран с табами (`Tabs`, дефолт «Службы»). **Вкладка «Службы»**: общий индикатор здоровья узла (`ServerHealthBadge` — светофор `overall`: Healthy→success/Degraded→warning/Down→danger/Unknown→neutral), список серверов 1С (имя/версия/`StatusBadge`; Admin: «Запустить» прямой кнопкой, «Остановить»/«Перезапустить» через `OneCServerActionDialog` с `confirm:true`, ADR-55 «да/нет» + предупреждение о простое баз), сводки RAS/SQL/IIS только наблюдением (`available:false` → плашка ошибки, не падение); загрузка статуса (`useServerStatus`) живёт в этой вкладке. **Вкладка «IIS»**: детальное управление IIS (`IisManagementCard`, дом IIS = «Сервер», ADR-54) — пулы/сайты/`iisreset`, переехало сюда из «Баз» (MLC-215); карточка живёт в `features/server/iis/`, монтируется при активации вкладки (запросы `/iis/*` стреляют лениво). Zod-граница `/api/v1/server/*` (`useServerStatus.ts`) — nullable-поля через `omittable()`; `overall`/`state` — `z.string()` (forward-compat). Тот же `serverStatusQueryKey` делит плашку «Обзора» (`ServerHealthCard`) — один запрос на оба места |
| `sessions` | `/sessions` — live-снимок сеансов, kill |
| `settings` | `/settings` (Admin) — параметры системы; `SettingsPage` раскладывает ключи каталога настроек по секциям (`SECTIONS`/`FIELD_META`), включая `Enforcement.TerminateMessage` (свободный текст в секции «Опрос 1С» — причина+контакты для тонкого клиента 1С при завершении сеанса по лимиту, MLC-190); блок состояния службы RAS (`RasServiceCard` + `RasServiceActionDialog`, `useRasService.ts`, ADR-47); карточка обновлений (`UpdateCheckCard`, фича `updates`, ADR-50). Слева — sticky-навигация по секциям-якорям (8 якорей: подключение/SQL/IIS/опрос/хранение/бэкапы/служба RAS/обновления; подписи `settings.nav.*`), активный пункт подсвечивается scroll-spy (`IntersectionObserver`, монохром); на узких экранах навигация скрыта, контент доступен прокруткой (MLC-202) |
| `tenants` | `/tenants` — CRUD клиентов |
| `updates` | глобальный баннер «доступна версия» в `AppShell` (все роли) + карточка «Проверить сейчас» в `/settings` (Admin); ADR-50 |
| `users` | `/users` (Admin) — учётные записи |

Каждая фича содержит: `types.ts` (zod-схемы + выведенные типы),
`use*.ts` (TanStack Query-хуки), `*.tsx` (компоненты), `__tests__/`.

---

## 3. Сетевой слой

### 3.1 `lib/api.ts` — fetch-обёртка

Единая функция `api<T>(path, options)` принимает относительный `path` (всегда
`/api/v1/…`), добавляет `credentials: "include"` (cookie-сессия ASP.NET Identity),
автоматически ставит `Content-Type: application/json` при наличии `body`.

Сессия поддерживается cookie; явного токена в заголовках нет.

**Обработка 401.** При статусе 401 вызывается глобальный `onUnauthorized` (регистрируется
в `App.tsx` через `setUnauthorizedHandler`): кэш QueryClient очищается и браузер
перенаправляется на `/login`. Повторных запросов с 401 нет — retry-политика
`queryClient` выключает повторы при 401 и 403.

**Типизация ответов.** По умолчанию JSON cast-уется через `payload as T`.
На критичных границах передаётся необязательный параметр `schema: ResponseSchema<T>` —
тогда ответ проходит runtime-валидацию (`schema.parse(payload)`); при расхождении
бросается `ApiSchemaError`. Схемы живут в `features/<feature>/types.ts` и
реализуют интерфейс `ResponseSchema<T>` через zod.

**Три класса ошибок (различение сеть / HTTP / схема).**

- `ApiNetworkError(path, cause)` — `fetch()` отклонился до получения ответа
  (нет связи с бэкендом, прерванное соединение). `fetch` обёрнут в try/catch;
  raw `TypeError` наружу не выходит. Это корень различения «нет связи» от HTTP-ошибки.
- `ApiError(status, message, body)` — получен не-2xx HTTP-ответ. Сообщение берётся
  из тела: поле `detail`, `title` или `message`; иначе `"HTTP {status}"`. Тело
  409 Conflict доступно через `readConflictBody(error)` (`ConflictBody.code + detail`);
  тело 400 ValidationProblem (`errors`: dict поле→сообщения) разбирает `applyFieldErrors`.
- `ApiSchemaError(path, issues)` — 2xx прошёл, но не прошёл Zod-границу (дрейф контракта BE↔FE).

### 3.2 `lib/queryClient.ts` — политики и классификация ошибок

Один глобальный `QueryClient` с политиками:
- `staleTime: 30_000` мс по умолчанию;
- `refetchOnWindowFocus: true` — освежает данные при возврате на вкладку; лечит застой
  админ-списков без поллинга (инфобазы/клиенты/пользователи/аудит/отчёты). Статичные
  запросы опт-аутятся явно (`useHealth` → `false`; `useAuth` защищён `staleTime` 5 мин);
  «живые» данные и так задают свои интервалы;
- retry: ≤ 2 попытки кроме 401/403 (0 попыток); мутации — без retry.

На `QueryCache.onError` и `MutationCache.onError` навешен единый классификатор
`classifyError` (диагностика идёт через консоль — удалённой телеметрии нет):
- `ApiNetworkError` → `markOffline()` поднимает глобальный баннер «нет связи»
  (`errors.network`); снимается `markOnline()` при следующем успешном запросе
  (`onSuccess` обоих кэшей). Фактический успех первичнее `navigator.onLine`.
- `ApiSchemaError` → `console.error("[ApiSchemaError]", path, issues)` с distinct
  greppable-префиксом — расхождения BE↔FE отделены от сетевых; пользователю
  показывается `errors.generic` (его действий не требуется).
- `ApiError` 4xx/5xx → ни баннера, ни schema-лога: обрабатывается на месте показа
  (тост или inline-ошибка поля формы).

### 3.3 `lib/connectionStatus.ts` — индикатор соединения

Минимальный module-level store без провайдера: `markOffline()` / `markOnline()`
переключают флаг, подписка из React — через `useIsOffline()` (`useSyncExternalStore`).
Первичный сигнал «нет связи» — фактический `ApiNetworkError` (не `navigator.onLine`).
Баннер `ConnectionBanner` (в `AppShell`, над топбаром) виден, только пока флаг взведён.
Рядом с ним в `AppShell` — `UpdateBanner` (фича `updates`, ADR-50): «Доступна версия X.Y.Z»,
виден всем ролям, скрыт при недоступной проверке или отсутствии обновления.

---

## 4. TanStack Query: конвенции

### 4.1 Query keys

Каждая фича экспортирует именованный константный ключ:

```ts
// tenants
export const tenantsQueryKey = ["tenants"] as const;

// infobases
export const infobasesQueryKey = ["infobases"] as const;

// sessions
export const sessionsSnapshotQueryKey = ["sessions", "snapshot"] as const;

// dashboard
export const dashboardSummaryQueryKey = ["dashboard", "summary"] as const;

// performance
export const hostMetricsQueryKey = ["performance", "host"] as const;

// backups (параметризован инфобазой)
export const backupsQueryKey = (infobaseId: string | null) =>
  ["backups", infobaseId ?? "__none__"] as const;

// auth
export const ME_KEY = ["auth", "me"] as const;

// discovery (по ресурсу)
// ["discovery", "cluster-infobases"], ["discovery", "databases"], …

// users
export const usersQueryKey = ["users"] as const;
```

Пагинированные хуки расширяют префикс параметрами фильтров и страницы:
```ts
queryKey: [...tenantsQueryKey, { page, pageSize, search }]
queryKey: [...infobasesQueryKey, { tenantId, publishStatus, page, pageSize }]
```
Мутации инвалидируют по корневому префиксу (`["tenants"]`, `["infobases"]`) —
это покрывает все страницы и фильтры разом.

**Серверная пагинация списков (BE-17, MLC-130).** Кроме клиентов/инфобаз/аудита
пагинированы на сервере (`page`/`pageSize`, ответ-конверт `{ items, total, page, pageSize }`)
ещё `GET /api/v1/backups` и `GET /api/v1/performance/recordings` — эндпоинты больше не
материализуют всю таблицу. Их потребители (`BackupsDialog` — бэкапы одной инфобазы,
ограничены keep-latest retention; `RecordingSection` — расследования) запрашивают одну
страницу с запасом (`pageSize=100`) и читают `.items`; отдельной UI-листалки в них нет
(объёмы малые). **Поиск клиентов (UX-05):** список `/tenants` имеет поле поиска по имени —
`useTenants(page, pageSize, search)` шлёт `search` (debounce, сброс на 1-ю страницу);
бэкенд фильтрует `Tenant.Name` обычным `Contains`→`LIKE` (регистр — за collation БД).
С MLC-144a терм поиска живёт в URL-фильтре колонки `name` (`?f_name=…`, через
`useUrlTableFilters`) — отфильтрованный список шарится ссылкой; страница использует
`DataTable` с серверной пагинацией (`manualPagination`).

**Базы (`/infobases`, MLC-144b).** Таблица баз (`InfobasesPage` + билдер
`infobaseColumns`) тоже на `DataTable` с серверной пагинацией (`manualPagination` +
`pageCount`, без сортировки — сортировка серверная не применяется). URL-фильтры клиента
(`?tenantId=`), статуса публикации (`?publishStatus=`) и «не найдена в кластере»
(`?notInCluster=true`, MLC-150) сохранены как есть (через `useSearchParams`, вне tanstack
`columnFilters`) и размещены в слоте тулбара рядом с меню видимости колонок и density.
Колонка «Размер» (MLC-185d) показывает текущий размер базы — сумму data+log из последнего
снимка телеметрии (`currentDataBytes`/`currentLogBytes`, `omittable`; форматтер `formatBytes`
КБ/МБ/ГБ, тултип с разбивкой); «—» пока снимка нет (ночная джоба не отработала / база была
недоступна). Та же колонка — в таблице инфобаз на карточке клиента (`infobaseDetailColumns`).

Фильтр «Только не найденные в кластере» (чекбокс, **только админ**) фильтруется **серверно**
(`GET /infobases?notInCluster=true`): BE отбирает записи панели, чьего `ClusterInfobaseId`
нет в снапшоте кластера 1С, по тому же TTL-кэшу снапшота, что и `/infobases/unassigned`
(без второго спавна rac.exe). Развилка недоступности RAS: ответ несёт `clusterAvailable`
(заполняется только при этом фильтре). При `clusterAvailable: false` фильтрация не выполнена —
страница показывает честный жёлтый баннер «не удалось проверить кластер» с кнопкой «Обновить»,
а не вводящий в заблуждение пустой список «0 найдено» (нельзя отличить «нет пропавших» от
«не знаем»). Пустой результат при доступном кластере — «Все базы найдены в кластере 1С». Множественный выбор для bulk-операций живёт во внешнем `Map<id публикации, строка>` (а не в
tanstack row-selection), поэтому переживает листание страниц и смену фильтра; чекбокс-колонка
видна только админу. Панель массовых действий (`PublicationsBulkBar`, MLC-184b) делит экран на
**зону выбора** (счётчик + «выбрать все N по фильтру» через `GET /infobases/ids` (MLC-181c) +
«снять») и **зону действий**: частые кнопками (Опубликовать, Сменить платформу) + меню «Ещё»
(Проверить, Снять с публикации, Удалить базу — последние два destructive). Движок — `useBulkOperation`
(N идемпотентных per-id вызовов, частичный успех, прогресс); удаление из бара бьёт по инфобазе
(`DELETE /infobases/{id}`, общий чекбокс «снять из IIS»), остальные — по публикации. Статус публикации и метка «не найдена в кластере»
(MLC-096) — через `StatusBadge`. Карточка клиента (`/tenants/:id`) показывает инфобазы
клиента на том же `DataTable` через `buildInfobaseDetailColumns` (`infobaseDetailColumns.tsx`,
MLC-144c); прежний общий компонент `InfobaseRow` удалён (MLC-146). Набор колонок по умолчанию
(MLC-206) — ☐·База·Клиент·Статус(+«не найдена в кластере»)·Публикация·Размер·⋯; **«Версия
платформы» и «Проверено» скрыты по умолчанию** (`initialState.columnVisibility`) и доступны
через меню «Колонки» — как Создан/Обновлён у «Клиентов» (MLC-200).

Страница несёт две вкладки (`Tabs`, **контролируемые URL `?tab=`**, MLC-196b): **«Базы»**
(дефолт, ключ не пишется — чистый URL) · **«Размер баз»** (`InfobasesSizeTab`). Управление IIS
вкладкой здесь больше нет — детальное управление IIS переехало в раздел «Сервер» (MLC-215,
ADR-54). Смена вкладки пишет `?tab=` **слиянием** в существующие параметры (прочие фильтры
`tenantId`/`publishStatus`/`search` сохраняются), поэтому вкладка шарится ссылкой; устаревший
`?tab=iis` нормализуется в дефолт «Базы». «Размер баз» — size-часть растворённых «Отчётов»:
фильтр периода (`ReportsFiltersBar`), сводка `DatabaseSizeSummary` и drill-down по клиенту
`DatabaseSizeDetail` (`useDatabaseSize`/`useDatabaseSizeByTenant`, эндпоинты
`/reports/database-size[/:tenantId]` те же — переехало только FE-потребление). Период/клиент
отчёта держатся в URL (`from`/`to`/`tenant`) и пишутся слиянием (`useReportFilters`);
отчётный ключ `?tenant=` ≠ басовый фильтр `?tenantId=` (разные ключи). Компонент
самодостаточен и рендерится внутри `TabsContent value="size"` — Radix монтирует его только
когда вкладка активна, поэтому отчётные хуки не стреляют на вкладке «Базы».

**Клиентская сортировка и пагинация live-снапшотов (UX-14/15, MLC-131).** Сеансы
(`/sessions`) и список «не найденных в кластере» баз (`MissingInfobasesDialog`) приходят
целым массивом — серверной пагинации нет. Сортировка и разбивка по страницам выполняются
над уже полученным снапшотом без дополнительных запросов:

- **Сеансы** (`/sessions`) — **дом темы лицензий с тремя видами** (MLC-196a/196b, Фаза 1
  редизайн-трека). `useSessionsPage` оркеструет live-виды; активный вид — в URL (`?view=`,
  дефолт `byTenant`, чистый URL не пишет ключ; `parseView` различает `byTenant`/`live`/`usage`,
  неизвестное → дефолт). Переключатель — `Tabs` (`@/components/ui/tabs`).
  Шапка, баннер ошибки и баннер «license-fact unavailable» (`licenseFactAvailable === false`,
  ADR-48/MLC-166) — общие для всех видов.

  - **Проекция «По клиентам»** (`view=byTenant`, ПО УМОЛЧАНИЮ) — агрегат «кто сколько
    потребляет» (`ByTenantTable`). Колонки: **Клиент · Потребляет · Лимит · Загрузка**
    (полоса `Progress` + %). Источник — **клиентская склейка БЕЗ нового BE-эндпоинта**:
    все клиенты (имя + `maxConcurrentLicenses`) из `useAllTenants`, потребление —
    `buildConsumedByTenant` над тем же снапшотом сеансов, что уже грузит страница (второго
    параллельного запроса нет). Клиенты с 0 сеансов показываются (`consumed = 0`). Чистая
    сборка/сортировка строк — `buildByTenantRows`/`sortByTenantRows` (`byTenantRows.ts`,
    тест): **превышения (`consumed > limit`) — сверху**, затем по потреблению ↓, при равенстве —
    имя клиента (`localeCompare("ru")`). Цвет/ярлык/полоса — строго через `lib/quota.ts`
    (`quotaDisplay`); статусный ярлык квоты — через `StatusBadge` (`common.quota.*`). **Клик
    по строке клиента** → переключает на «Живые сеансы» с фильтром по имени клиента
    (`view=live` + `q=<tenantName>`).
  - **Вид «Использование за период»** (`view=usage`, MLC-196b) — license-часть растворённых
    «Отчётов» (`SessionsUsageView`): фильтр периода (`ReportsFiltersBar`), сводка
    `LicenseUsageSummary` (по всем клиентам) и drill-down по клиенту `ReportsDetail`
    (`useLicenseUsage`/`useLicenseUsageByTenant`, эндпоинты `/reports/license-usage[/:tenantId]`
    те же — переехало только FE-потребление). Период/клиент держатся в URL (`from`/`to`/`tenant`)
    и пишутся **слиянием** (`useReportFilters` → `mergeReportFiltersIntoParams`): хост-ключ
    `view=usage` и прочие параметры сохраняются (без полного replace URL, который сбросил бы
    вид). Компонент самодостаточен и рендерится внутри `TabsContent value="usage"` — Radix
    монтирует его только когда вид активен, поэтому отчётные хуки не стреляют на «По
    клиентам»/«Живые сеансы».
  - **Проекция «Живые сеансы»** (`view=live`) — текущий live-снимок (поведение без изменений).
    Сверху — **лицензионный банд** (`SessionsLicenseBand`): тихая плотная строка KPI host-уровня
    «Использовано / лимит · Свободно · Активных» из `useDashboardSummary` (`/dashboard/summary`,
    без нового контракта: `licensesConsumedTotal`/`licensesAvailableTotal`/`sessionsActiveTotal`,
    лимит = использовано + свободно). Ниже — таблица сеансов: `useSessionsPage` строит экземпляр
    `DataTable` (`useReactTable`, ADR-46) с клиентской сортировкой (`getSortedRowModel`,
    компараторы повторяют прежнюю семантику UX-14: `localeCompare("ru")` для строк, числовое
    сравнение длительности, сортировка статуса лицензии `licenseStatus`) и клиентской пагинацией
    (`getPaginationRowModel`, 25 записей). Канонический компаратор `sortRows` сохранён (тест).
    Заголовки сортируемых колонок — `DataTableColumnHeader` с иконками ↑↓↕. При рефетче снапшота
    (каждые 5 с) `pageIndex` clamp'ится в `[0, pageCount-1]` — экран не «прыгает»; смена
    сортировки/фильтра сбрасывает на первую страницу. Колонки без сортировки (ID сеанса, App ID,
    Действие) остаются статичными. URL-фильтры `q`/`infobaseId`/`appIds` (фильтр инфобазы —
    `SearchableSelect`, UX-38; фильтр **типа сеанса** — `SearchableMultiSelect`, мультивыбор
    `app-id` с человеческими именами `sessions.appTypes`, MLC-165) — вне tanstack `columnFilters`
    (кросс-колоночные); размещены в тулбаре `DataTable` рядом с видимостью колонок и density.
    **Тумблер «Только лицензионные»** (`consuming`, MLC-167) — **ВКЛ по умолчанию** (чистый URL
    → показываются только сеансы `licenseStatus === "Consuming"`, фактически потребляющие лицензию
    по факту rac); `consuming=0` выключает. При ВКЛ фильтр типов **перекрыт** (приглушён, значение
    сохраняется). При ВЫКЛ действует мультивыбор типов: опции — **полный каталог** `KNOWN_APP_IDS`
    ∪ присутствующие незнакомые типы (можно отметить тип заранее, до его появления), с действием
    «Выбрать все / Снять все»; дефолт выбора (без `appIds`) — интерактивные, явный пустой =
    «показать пусто» (`appTypes.ts`/`resolveAppIds`).

- **«Не найдены в кластере»** — `MissingInfobasesDialog` сортирует список по
  `tenantName + name` (стабильно, по-русски) и показывает по 20 записей через
  `PaginationBar`. Страница также clamp'ируется при изменении входного массива.

- **Пользователи** — `UsersPage` (`features/users`) использует `DataTable` (MLC-144e)
  с клиентской сортировкой (`getSortedRowModel`); пагинация не нужна — список учёток
  небольшой (весь в памяти). Колонки: Логин, Роль, Статус, Последний вход, Действия.
  Колонка «Логин» отображает круглую монограмму-аватар с инициалами (`initialsOf`,
  инлайн в ячейке) слева от имени — монохром (`bg-muted text-muted-foreground`), без внешних
  зависимостей. Колонка «Роль» показывает иконку слева от подписи роли: Admin →
  `ShieldCheckIcon`, Viewer → `EyeIcon` (оба `text-muted-foreground`, `size-4`); хелпер
  `roleIconFor` выбирает иконку по массиву ролей (приоритет Admin > Viewer).
  Статус — только через `StatusBadge` (инвариант). Столбец «Действия» (`enableHiding: false`)
  содержит меню смены роли, сброса пароля, отключения/включения учётки. Тулбар `DataTable`
  предоставляет видимость колонок и density-toggle.

### 4.2 Инвалидация через `useInvalidatingMutation`

`lib/useInvalidatingMutation.ts` — единая обёртка над `useMutation` с политикой
инвалидации. Принимает `invalidate`: один ключ, массив ключей или функцию от
переменных мутации. Примеры:

**Простая инвалидация одного ресурса:**
```ts
// useKillSession — убивает сеанс, инвалидирует снимок
invalidate: sessionsSnapshotQueryKey
```

**Кросс-фичевая инвалидация** — `useReassignInfobase` (переназначение инфобазы
меняет счётчик баз у обоих клиентов):
```ts
invalidate: [infobasesQueryKey, tenantsQueryKey]
```

**Публикации** — мутации `usePublish`, `useUnpublish`, `useChangePlatform`
инвалидируют `["infobases"]`, потому что публикация встроена во вложенный объект
строки списка инфобаз:
```ts
invalidate: () => [infobasesQueryKey]
```

**Бэкапы** — ключ параметризован инфобазой, инвалидация через функцию:
```ts
invalidate: (infobaseId) => backupsQueryKey(infobaseId)
```

После успешного логина `useLogin` записывает результат напрямую в кэш через
`qc.setQueryData(ME_KEY, user)` — без лишнего сетевого запроса. При logout
`qc.clear()` удаляет весь кэш.

### 4.3 Refetch-интервалы «живых» данных

| Запрос | Интервал | Обоснование |
|---|---|---|
| `useSessionsSnapshot` | 5 с | near-realtime: согласован с hot-каденцией enforce (~4 с) |
| `useDashboardSummary` | 5 с | KPI дашборда отражает near-realtime |
| `useDashboardAlerts` | 30 с | сигналы «Требует внимания», не live-KPI; бэкенд кеширует агрегат 30 с |
| `useHostMetrics` (performance) | 5 с | live-метрики хоста |
| `useDashboardHostHealth` | 45 с | «есть ли проблема», не «какая» |
| `useBackups` | 5 с | poll прогресса Queued→Running→Succeeded |
| `useMe` | нет (`false`) | меняется только при логине/выходе |
| discovery-запросы | нет | `staleTime: 5 мин`, кэш до перефетча |

Все «живые» запросы используют `placeholderData: (prev) => prev` — экран не
мигает скелетоном между poll-ами.

**Пауза и ручное обновление (MLC-156).** «Сеансы» и «Быстродействие» несут компонент
`LiveControls` (см. `06_UI_GUIDE`): **Пауза** переключает `refetchInterval` опрашиваемых
хуков в `false` (страница владеет одним `isPaused`; на «Быстродействии» он общий для host/1С/SQL).
**«Обновить сейчас»** на «Сеансах» — живой форс: `POST /api/v1/sessions/refresh` (без тела,
204) запускает cold-прогон на бэкенде и ждёт его завершения, затем `refetch()` снимка; на
«Быстродействии» — `refetchQueries` трёх live-ключей (perf уже live на каждый poll).

### 4.4 Live-оверлей потребления лицензий на клиента

Список клиентов (`GET /api/v1/tenants`) и DTO клиента несут только лимит
(`maxConcurrentLicenses`) — текущего потребления в контракте клиента нет. Чтобы
акцентировать «нарушителя квоты» на `/tenants` и карточке `/tenants/:id`,
потребление берётся live-оверлеем из снапшота сеансов: хук
`useTenantConsumption()` (`features/tenants`) поверх `useSessionsSnapshot()`
строит `Map<tenantId, consumed>` чистой функцией `buildConsumedByTenant(items)`
(считает записи со `licenseStatus === "Consuming"`, группирует по `tenantId`). Это
намеренное небольшое дублирование канонического backend-метода
`LicenseConsumption.CountByTenant`: на FE — визуальный оверлей, не контракт и не
parity-правило (тот же снапшот питает серверный расчёт дашборда — значения
совпадают). Клиент без сеансов → `consumed = 0`; первая загрузка снапшота → скелетон.

Визуальный язык акцента (пороги 75/90, severity → `StatusBadge`-вариант) —
единый источник `lib/quota.ts`; раскладка по экранам — `06_UI_GUIDE.md` §7.

**Список клиентов (`tenantColumns.tsx`, MLC-200).** Колонки по умолчанию:
**Клиент · Лицензии · Базы (`infobaseCount`) · Статус · ⋯ (actions)**. Колонка
**«Лицензии»** — единая (слиты прежние «Лимит» и «Квота»): `consumed / limit (percent%)`
+ **полоса заполнения** (`Progress`, цвет через `quotaDisplay().progressClass`) +
`StatusBadge` ярлыка квоты (`common.quota.*`). Тот же визуальный язык, что вид
«Сеансы → По клиентам» (`ByTenantTable`). Безлимит (`maxConcurrentLicenses ≤ 0`) →
«—», без полосы. Процент > 100% показывается числом, полоса cap'ится на 100. Колонки
**«Создан»/«Обновлён» скрыты по умолчанию** (`initialState.columnVisibility`), но
остаются доступны через меню «Колонки» `DataTable` (`enableHiding`).

**Карточка клиента (`TenantDetailPage.tsx`, MLC-200).** Шапка — имя + статус-`StatusBadge`;
ниже **лицензионная панель** (`Card`): лимит, полоса потребления (`Progress` +
`quotaDisplay`), `consumed / limit (percent%)` + `StatusBadge`, при `consumed > limit` —
явный текст **«превышение на N»** (`tenants.detail.overBy`, плюрализован по N), ссылка
**«Сеансы клиента →»** на `/sessions?view=live&q=<имя клиента>` (паттерн перехода
`goToLiveWithTenant`: вид «Живые сеансы» + фильтр `q` по имени). Безлимит → «Лимит не
задан», без полосы. Ниже — таблица инфобаз клиента (`buildInfobaseDetailColumns`).

### 4.5 Инвалидация отчётов при смене лимита (FE-03)

`useUpdateTenant` инвалидирует `[tenantsQueryKey, reportsQueryKey]`: смена лимита
влияет на проценты в отчёте использования лицензий, поэтому кэш отчётов сбрасывается
вместе с клиентами. `reportsQueryKey` вынесен в `features/reports/reportsQueryKeys.ts`
(а не в `useLicenseUsage.ts`), чтобы `useTenants` импортировал только константу
без циклической зависимости через потребителей отчётов.

---

## 5. Zod и `omittable` (ADR-10.1)

### 5.1 Ключевая гоча: backend опускает null-поля

Backend сериализует JSON с `JsonIgnoreCondition.WhenWritingNull`: nullable-поле с
`null` не приходит как `"field": null` — ключ **отсутствует** в ответе. Поэтому
zod-правило `.nullable()` ломает валидацию на таких полях (требует ключ).

**Решение — `omittable(schema)` из `lib/apiSchema.ts`:**
```ts
export function omittable<T extends z.ZodTypeAny>(schema: T) {
  return schema.nullish().transform((value): z.infer<T> | null => value ?? null);
}
```
`.nullish()` принимает и `undefined` (ключ отсутствует), и `null`; `.transform`
нормализует оба варианта в `null`. Выводимый тип — `T | null`, потребители не
различают «null» и «нет ключа».

**Пример из `features/backups/types.ts`** — поля, которые у `Queued`-бэкапа
отсутствуют:
```ts
startedAtUtc:    omittable(z.string()),
completedAtUtc:  omittable(z.string()),
filePath:        omittable(z.string()),
fileSizeBytes:   omittable(z.number()),
errorMessage:    omittable(z.string()),
```

**Пример из `features/infobases/types.ts`** — поля публикации:
```ts
physicalPathOverride: omittable(z.string()),
lastCheckAt:          omittable(z.string()),
lastCheckDetails:     omittable(z.string()),
```

**Пример из `features/tenants/types.ts`:**
```ts
updatedAt: omittable(z.string()),
rowVersion: omittable(z.string()),  // MLC-136 — токен оптимистической блокировки (base64)
```

`tenantSchema.rowVersion` (R12c) — токен оптимистической блокировки, приходящий
base64-строкой; `omittable`, т.к. под отсутствием токена API опускает поле. Форма
редактирования (`TenantFormDialog`) кладёт прочитанный `tenant.rowVersion` в `TenantInput`
и шлёт обратно при `PUT`. Конкурентный апдейт (устаревший токен) сервер отклоняет 409
`TENANT_CONCURRENCY_CONFLICT`; форма ловит его через `matchConflictCode` ДО маппинга дубля
имени и показывает тост `tenants.errors.concurrencyConflict` («обновите страницу и повторите»),
а не ошибку поля. Закреплено `__tests__/tenantSchema.test.ts` (приём ответа с токеном и
omit-null без него).

**Инфобазы/публикации (MLC-151)** зеркалят тот же паттерн. `infobaseSchema.rowVersion` и
`publicationSchema.rowVersion` — `omittable(z.string())`. Форма инфобазы (`useInfobaseForm`)
кладёт прочитанный `infobase.rowVersion` в `UpdateInfobaseInput` и
`infobase.publication.rowVersion` во вложенный `publication`, шлёт оба обратно при `PUT
/infobases/{id}`. 409 `INFOBASE_CONCURRENCY_CONFLICT` (и `PUBLICATION_CONCURRENCY_CONFLICT` на
случай дрейфа контракта) ловится через `matchConflictCode` ДО маппинга дубля имени / занятости
кластер-базы → тост `infobases.errors.concurrencyConflict`. Закреплено
`__tests__/infobaseSchema.test.ts` (приём токена и omit-null для обеих схем).

### 5.2 Parity с BE-валидацией

`features/infobases/validation.ts` — единый источник правил формы инфобазы на
фронте; закреплён parity-тестом `validation.test.ts`, парным бэкенд-тестам
(`InfobasesValidationTests.cs`). Правила: regex версии платформы
`/^\d+\.\d+\.\d+\.\d+$/`, max-длины полей (200/200/200/200/50/260),
виртуальный путь начинается с `/`, без пробелов.

Типы на FE выводятся из zod-схем (`z.infer`): `types.ts` не дублирует
интерфейсы вручную.

### 5.3 Критичные границы (runtime-валидация схемой)

Runtime-валидация через `api(..., { schema })` включена на всех значимых read-границах
(MLC-132, FE-09). Схемы живут в `features/<feature>/types.ts`; типы выводятся `z.infer`.

**Исходные критичные границы (ADR-10.1 / MLC-016):**
`currentUserSchema` (роли), `sessionsSnapshotResponseSchema`,
`infobaseListResponseSchema`, `tenantListResponseSchema`, `hostMetricsSnapshotSchema`,
`backupsPagedSchema`, `recordingsPagedSchema` (paged-конверты после BE-17/MLC-130).

**Расширено в MLC-132 (read-границы всех основных фич):**

| Фича | Схема | Хук |
|---|---|---|
| `server/iis` | `iisServerStatusSchema`, `iisAppPoolsResponseSchema`, `iisSitesResponseSchema` (translation-ключи остались `publications.iis.*` — историческое размещение, MLC-215) | `useIisServerStatus`, `useIisAppPools`, `useIisSites` |
| `server` (статус + обслуживание) | `serverStatusSchema`, `serverOperationSchema`, `backupFreshnessSchema` (свежесть бэкапов SQL, вкладка «Обслуживание», MLC-216) | `useServerStatus`, `useOneCServerOperation`, `useMaintenanceBackups` |
| `discovery` | `clusterInfobasesResponseSchema`, `databasesResponseSchema`, `iisSitesDiscoveryResponseSchema`, `racPathsResponseSchema`, `platformVersionsResponseSchema`, `sqlInstancesResponseSchema` | все хуки `useDiscovery.ts` |
| `publications` | `publicationStatusResponseSchema` | `useCheckStatus`, `usePublish`, `useUnpublish`, `useChangePlatform` |
| `infobases` | `clusterIdAvailabilitySchema`, `infobaseDetailSchema` | `useClusterIdAvailability`, `useCreateInfobase`, `useUpdateInfobase`, `useReassignInfobase` |
| `settings` | `settingsListSchema`, `rasServiceStatusSchema`, `rasServiceOperationSchema` | `useSettings`, `useRasServiceStatus`, `useRasServiceOperation` |
| `reports` | `licenseUsageSeriesResponseSchema`, `databaseSizeSeriesResponseSchema`, `databaseSizeTenantSeriesResponseSchema` | `useLicenseUsage`, `useLicenseUsageByTenant`, `useDatabaseSize`, `useDatabaseSizeByTenant` |
| `audit` | `auditPagedResponseSchema`, `auditRetentionResponseSchema` | `useAuditLog`, `useAuditRetention` |

**Read-границы, валидируемые с более ранних задач (учтены FE-09, MLC-133):** эти схемы
подключены до появления трека FE-09 и потому не входили в свип MLC-132 — но read-граница
у обеих фич полная:

| Фича | Схема | Хук | Откуда |
|---|---|---|---|
| `users` | `userListResponseSchema` | `useUsers` | MLC-060 |
| `infobases/unassigned` | `unassignedInfobasesResponseSchema` | `useUnassignedInfobases` | MLC-093 |

**Пропущены (мутации с тривиальным/echo/`null` телом):** мутации IIS
(recycle/start/stop/restart/iisreset — echo `IisOperationResponse`, реальное состояние
приходит фоновым refetch discovery), `useDeleteInfobase` (204 No Content),
`useDisableUser`/`useEnableUser`/`useChangeUserRole` (null body),
`useHideUnassignedInfobase`/`useUnhideUnassignedInfobase` (null body).
Мутации создания/сброса пользователей (`useCreateUser`, `useResetUserPassword`) несут
одноразовый пароль — схема малоценна (пароль показывается в UI один раз, не сохраняется).

---

## 6. Формы: react-hook-form + zod

### 6.1 Паттерн диалога

Все диалоги CRUD следуют одной схеме (пример — `TenantFormDialog`):
1. zod-схема строится функцией `buildSchema(t)`, принимающей `t` из `useTranslation` —
   тексты ошибок локализованы в момент построения.
2. `useForm<FormValues>({ resolver: zodResolver(buildSchema(t)), defaultValues })`.
3. `onSubmit = form.handleSubmit(async (values) => { ... })`.
4. Единый порядок обработки ошибки submit (UX-04): **409-code → 400-field → generic-тост**:
   1. `matchConflictCode(error, { CODE: { field, messageKey } })` →
      `form.setError(field, { type: "server", message: t(key) })`.
   2. `applyFieldErrors(error, form.setError, fieldMap?)` — на 400 ValidationProblem
      разбирает dict `errors` (ключи полей бэка в **PascalCase**, для вложенной
      публикации с префиксом `Publication.`), маппит на имена полей формы (явная
      карта или нормализация first-letter-lowercase по сегментам:
      `Publication.SiteName → publication.siteName`) и ставит **первое** сообщение
      из массива через `setError(..., { type: "server" })`. Возвращает `true`, если
      проставлено хоть одно поле (тогда дальше не идём).
   3. `toastFormSubmitError(error, t)` — fallback (400 → серверное сообщение, иначе generic).

   Источник 400-ошибок поля — рантайм-барьер `InfobaseValidationRules` (MLC-118):
   длина/символы приходят как 400 ValidationProblem, а не 500. Inline-ошибки
   применяют: `TenantFormDialog` (`Name`), `UserFormDialog` (`UserName`),
   `useInfobaseForm` (инфобаза + вложенная публикация; поля блока «Дополнительно»
   раскрывают его). `SettingField` разбирает `errors.Value` своей inline-веткой;
   `ChangePlatformDialog` (версия — `Select`, без текстового поля) показывает
   конкретное серверное 400-сообщение тостом. **LoginPage** имеет form-level
   inline-канал (`role="alert"`): 401 → `auth.invalidCredentials`,
   `ApiNetworkError` → `errors.network`, прочее → `errors.generic` (тост вторичен).

### 6.2 Сброс состояния при повторном открытии

Два паттерна — выбор зависит от того, где живёт `useForm`:

**Форма внутри DialogContent (Radix размонтирует при закрытии).**
`ChangePasswordForm` (`features/profile`) держит `useForm` внутри себя и рендерится
в `DialogContent`. Radix размонтирует `DialogContent` при `open=false` → компонент
пересоздаётся при следующем открытии, `defaultValues` применяются заново.
Никакого дополнительного кода не требуется.

**Форма во внешнем always-mounted компоненте.**
Если компонент с `useForm` монтируется постоянно (call-site не размонтирует его
при закрытии диалога), значения «призрачно» остаются между открытиями.
Пример: `UserFormDialog` — `UsersPage` держит его смонтированным всегда,
переключая только `open`. Решение — `useEffect` на `open → true`:
```ts
useEffect(() => {
  if (open) form.reset({ userName: "", role: "Admin" });
}, [open, form]);
```
Альтернатива — call-site key-паттерн `key={editing?.id ?? "create"}` (как у
`TenantFormDialog`/`TenantsPage`): он перемонтирует диалог при переключении между
разными целями редактирования, но у create-only пути ключ константен (`"create"`)
и при повторных открытиях создания НЕ перемонтирует — поэтому для сброса create-формы
нужен `useEffect(open → reset)`, а не этот ключ.

В `useInfobaseForm` сброс управляется через `useEffect` на `open`:
touched-рефы и `advancedOpen` сбрасываются при каждом открытии.

### 6.3 Discovery-поля (автоподстановка)

`DiscoveryField` (`features/discovery/DiscoveryField.tsx`) — контролируемый
компонент (`value/onChange` из `react-hook-form field`). Работает в двух режимах:
- **Список** — `Select` из Radix с обнаруженными вариантами (`/api/v1/discovery/*`).
- **Ручной ввод** — `Input` с fallback при `available=false` или по кнопке
  «Ввести вручную».

Если discovery-источник недоступен, поле автоматически переходит в ручной режим.
`toDiscoveryState(query)` нормализует состояние react-query в `{ available, loading, error }`.

Используется в форме инфобазы для полей: база кластера, имя БД, сайт IIS,
версия платформы; в параметрах системы — для пути к `rac.exe` и SQL-инстанса.

Discovery-запросы кешируются 5 минут (`staleTime`), чтобы не перезапрашивать
кластер 1С/IIS при каждом открытии формы.

---

## 7. Маршрутизация и доступ

### 7.1 Структура маршрутов (`routes/router.tsx`)

Все страницы грузятся лениво (`React.lazy`). Корень SPA делится на две ветки:

```
/login            — LoginPage (без защиты)
/                 — ProtectedRoute → AppShell
  /               — DashboardPage
  /tenants        — TenantsPage
  /tenants/:id    — TenantDetailPage
  /infobases      — InfobasesPage
  /sessions       — SessionsPage
  /performance    — PerformancePage
  /server         — ServerPage; три вкладки «Службы»/«IIS»/«Обслуживание» (свежесть бэкапов SQL, read-only, MLC-216), «IIS»/«Обслуживание» монтируются лениво. Viewer наблюдает; управление сервером 1С — Admin, гейт на действиях, не на маршруте
  /audit          — AuditPage
  /design         — DesignSystemPage (эталон дизайн-системы; вне навигации, обе роли)
  /settings       — ProtectedRoute (requireAdmin) → SettingsPage
  /users          — ProtectedRoute (requireAdmin) → UsersPage
  /*              — Navigate to /
```

### 7.2 `ProtectedRoute`

`ProtectedRoute` опрашивает `useMe()` (`GET /api/v1/auth/me`):
- Загрузка → спиннер.
- `isError` из-за `ApiNetworkError` / `ApiSchemaError` → экран «нет связи» с кнопкой
  «Повторить» (`refetch`); сессия **не** сбрасывается (UX-03/FE-05). Сетевой сбой
  показывает `errors.network`, схемный — `errors.generic`.
- Нет данных без ошибки (неаутентифицирован: `fetchMe` поймал 401 → `null`, а реальный
  401 уже увёл на `/login` через `onUnauthorized`) → `<Navigate to="/login" replace />`.
- `data.mustChangePassword === true` → блокирующий экран `ForcePasswordChange`
  (сайдбар и контент не рендерятся вовсе). После успешной смены пароля
  инвалидируется `ME_KEY` — флаг снимается.
- `requireAdmin && !data.roles.includes("Admin")` → `<Navigate to="/" replace />`
  (залогиненный Viewer перенаправляется на дашборд, не на `/login`).

### 7.3 Проверка роли в компонентах

Роль проверяется через `useMe()`:
```ts
const isAdmin = me?.roles.includes("Admin") ?? false;
```
Кнопки create/edit/delete рендерятся условно по `isAdmin`. Страницы «Параметры»
и «Пользователи» скрыты из навигации и защищены маршрутным `requireAdmin`.

### 7.4 Фильтры и навигация журнала аудита

`AuditPage` (`features/audit`) держит фильтры и страницу в URL-state
(`useSearchParams` → `auditUrlState.ts`), поэтому вид шарится ссылкой и переживает
перезагрузку.

`AuditTable` использует `DataTable` (ADR-46, MLC-144d) с серверной пагинацией
(`manualPagination: true`, `pageCount`). Колонки: Время, Действие, Инициатор, Клиент,
Описание — все скрываемые через меню видимости; density-toggle сохраняется в
`localStorage`. Бейджи типа действия реализованы через `Badge` с семантическими
цветами (emerald/sky/rose/neutral по суффиксу `*Created`/`*Updated`/`*Deleted`/Auth).

`AuditFiltersBar` (фильтры) передаётся в слот `toolbarChildren` `DataTable` и предлагает:

- **Тип действия** и **Клиент** — searchable-списки (`components/ui/SearchableSelect`):
  поповер с полем фильтра по подстроке (case-insensitive) поверх Popover + Input +
  списка опций, без отдельной combobox-зависимости. Фильтр действий ищет по
  локализованному названию (`audit.actions.*`). Первая строка списка — «любое»,
  сбрасывает выбор в `null`.
- **Поиск** — единственное свободное текстовое поле; подстрока по описанию И инициатору
  (debounce ~300 мс перед коммитом фильтра, чтобы не дёргать запрос на каждый символ).
- **С даты / По дату** и **размер страницы** — как прежде.

Любое изменение фильтра возвращает страницу на первую. Все термы передаются на
backend (`GET /api/v1/audit`: `search`, `initiator` дополняют `actionType`/`tenantId`/
`from`/`to`); сервер валидирует длину свободного текста и фильтрует выборку.
URL-параметры фильтров (`actionType`, `tenantId`, `from`, `to`, `search`, `initiator`,
`page`, `pageSize`) сохранены без изменений — shareable-ссылки остаются совместимы.

`AuditPagination` поверх скользящего окна `pageLinkRange` добавляет переход на
произвольную страницу: кнопки «первая»/«последняя» и поле ввода номера страницы
(Enter/blur, clamp в диапазон `1..totalPages`) — на больших объёмах окна из 7 ссылок
недостаточно.

### 7.5 RAS-карточка дашборда

`RasHealthCard` (`features/dashboard`) показывает здоровье связи с кластером 1С из
снапшота `summary.ras` (`healthy`, `lastCheckedAtUtc`, `lastErrorMessage`,
`consecutiveFailures`). Три состояния: «Проверка…» (neutral, до первой пробы) /
«OK» (success) / «Сбой» (danger). При `healthy === false` под бейджем —
**видимая** actionable-подсказка (`dashboard.ras.hint`) «нет связи с кластером 1С,
проверьте адрес RAS в «Параметрах»» + ссылка-переход в `/settings` + счётчик
`consecutiveFailures` (plural-ключи). Сырой `lastErrorMessage` остаётся во вторичном
тултипе — не как основной текст (UX-17).

Сигнал недоступности — звено модели ADR-47 «дашборд замечает → Настройки лечат»:
дашборд только сигналит проблему и уводит оператора в «Параметры», где `RasServiceCard`
ставит полный диагноз (4 состояния) и чинит службу RAS. Источник сигнала —
**дешёвый** in-memory health-снимок `summary.ras` (`RasHealthProbingService`); дорогой
`GET /ras-service/status` (перебор служб Windows) с дашборда **не** вызывается.

### 7.6 Композиция «Обзора» (MLC-186, MLC-198)

`DashboardPage` сверху вниз (порядок Фазы 2 редизайна, MLC-198): заголовок (+ время
обновления) → виджет **«Требует внимания»** (поднят под шапку: actionable-сигналы важнее
тихих KPI) → **KPI-ряд** (5 кликабельных карточек, MLC-085: Клиенты · Инфобазы · Сеансы
(live-точка) · Использовано (мини-спарклайн лицензий 7д) · Свободно) → **один тренд**
(использование лицензий 7д, на всю ширину) → строка здоровья (RAS + хост, две равные карты
**на всю ширину**) → **лента свежей активности** (на всю ширину).

Блоки **топ-клиенты по нагрузке** и **тренд роста размера баз** убраны с «Обзора» (MLC-198,
решение владельца): размер баз живёт в «Базы → Размер баз» (MLC-196b), потребление по клиентам —
в «Сеансы → По клиентам» (MLC-196a, богаче: все клиенты, сортировка, превышения). Контракт
`/dashboard/summary` не меняется — поле `topTenantsByConsumption` остаётся в толерантной Zod-схеме
(больше не рендерится; кандидат на BE-чистку отдельной задачей).

- **`AttentionWidget`** (`features/dashboard`) — единый список actionable-сигналов из
  серверного агрегата `GET /dashboard/alerts` (`useDashboardAlerts`, MLC-186a) ПЛЮС RAS-health и
  факт лицензий из `summary` (передаётся пропсом — без второго запроса). Строки показываются
  только при активном сигнале (danger перед warning), severity-иконки/цвета — палитра
  `StatusBadge`/`lib/quota.ts`. Квота лицензий — **три ФАКТИЧЕСКИХ бакета** по `consumed` vs
  `limit` (MLC-193, зеркало `quotaLabel` из `lib/quota.ts`), а не severity-цвет: «превысили квоту»
  (`consumed > limit`, danger), «достигли лимита» (`consumed == limit`, danger) и «близки к
  лимиту» (ниже лимита, но процент ≥ 75 %, warning) — в этом порядке; счётчики приходят из
  агрегата как `quotaExceeded`/`quotaAtLimit`/`quotaNearLimit`. Переход даётся ссылкой только если цель доступна роли
  (`/settings` — Admin-only, гейт через `useMe()`). Дрейф панель↔кластер приходит `null` для
  не-Admin (Admin-only на бэкенде) → строк нет. Пусто → success-строка «Всё в порядке».
  Отдельный амбер-баннер «факт лицензий недоступен» убран — теперь это строка виджета.
- **Тренд** (`LicenseTrendCard`) — компактный recharts на хуке отчётов `useLicenseUsage` с
  **фиксированным 7-дневным** диапазоном (`lastNDaysRange(7)`, мемоизирован один раз — стабильный
  query-key; локально-суточные границы общие с building-blocks отчётов, MLC-177). Один
  license-запрос на страницу делят трендовая карточка и KPI-спарклайн.
- **`RecentActivityCard`** — последние 5 записей журнала через `useAuditLog` (мемоизированный
  фильтр), лейблы действий `t('audit.actions.*')` и `RelativeTime` как на `/audit`; «Показать
  всё» ведёт в `/audit` (Viewer-доступен).

---

## 8. i18n

SPA поддерживает только русский язык.

`i18n/index.ts` инициализирует i18next с одним ресурсом:
`resources: { ru: { translation: ru } }`, `lng: "ru"`, `fallbackLng: "ru"`.
Ключ `returnNull: false` гарантирует, что пропущенный ключ вернёт строку,
а не `null`.

UI-тексты разнесены по per-feature файлам `i18n/ru/<topLevelKey>.json` (по одному
файлу на каждый top-level ключ: `common.json`, `nav.json`, `auth.json`, …; каждый
файл оборачивает свой ключ). `i18n/index.ts` собирает их обратно в ОДИН объект
`ru` (через spread) и регистрирует единственный namespace `"translation"` —
полноценные i18next-namespaces не вводятся (RU-only, выгоды нет). Структура
итогового словаря идентична прежнему плоскому `ru.json`. Все строки интерфейса
берутся через `useTranslation()` и хук `t(key)` без аргумента-namespace.
Целостность сборки (наличие всех срезов и ключевых вложенных ключей) страхует
тест `i18n/__tests__/resources.test.ts`.

### Конвенция ключей

Ключи организованы по доменным пространствам имён:

```
common.*          — общие действия: save, cancel, edit, loading, …
nav.*             — навигация (nav.dashboard = "Обзор", nav.sessions = "Сеансы", …)
nav.groups.*      — группы меню (monitoring, management, system)
auth.*            — вход, выход, смена пароля, force-change
tenants.*         — клиенты: fields.*, form.*, errors.*, toasts.*
infobases.*       — инфобазы: fields.*, errors.*, statuses.*
publications.*    — публикации: fields.*, errors.*, statuses.*
sessions.*        — сеансы
reports.*         — отчёты
performance.*     — быстродействие
server.*          — раздел «Сервер»: tabs.*, health.*, onec.*, summary.*, dialog.*, toasts.*, dashboard.*, maintenance.* (вкладка «Обслуживание», MLC-216)
audit.*           — аудит
users.*           — пользователи, users.roles.Admin / users.roles.Viewer
settings.*        — параметры
backups.*         — бэкапы
discovery.*       — загрузка / retry / ручной ввод
errors.generic    — общий текст ошибки
```

Ключи соответствуют терминологии глоссария `01_OVERVIEW.md` §5: `nav.tenants` =
«Клиенты», `nav.infobases` = «Базы», `users.roles.Admin` = «Роль Admin» и т. д.

---

## 9. Тесты

Инструменты: **Vitest** ^4.1.7, **@testing-library/react** ^16.3.2,
**@testing-library/user-event** ^14.6.1, **@testing-library/jest-dom** ^6.9.1,
среда jsdom ^27.4.0.

Глобальный `test/setup.ts` добавляет заглушки для `ResizeObserver`, Pointer Capture
API и `matchMedia` (jsdom их не реализует, Radix UI требует).

По данным A0-BASELINE: **355 тестов в 69 файлах**, все проходят; `type-check`
(tsc) и `lint` (eslint) — без ошибок.

### Что покрыто тестами

- **Критичные zod-границы** (`lib/__tests__/apiSchema.test.ts`) — `pagedResponseSchema`,
  `currentUserSchema`, `sessionsSnapshotResponseSchema`: проверяют, что
  расхождение контракта бросает ошибку, а не пропускает неверный тип.
- **Валидация форм** (`infobases/__tests__/validation.test.ts`) — parity-тест правил
  версии платформы, max-длин полей, виртуального пути; golden-таблица парная BE.
- **Диалоги CRUD** — `TenantFormDialog`, `DeleteTenantDialog`, `InfobaseFormDialog`,
  `DeleteInfobaseDialog`, `ReassignInfobaseDialog`, `useInfobaseForm`.
- **Маршрутная защита** (`auth/__tests__/ProtectedRoute.test.tsx`,
  `ForcePasswordChange.test.tsx`) — поведение при незалогиненном, viewer, force-change.
- **Вспомогательные функции** — `lib/__tests__/api.test.ts`, `apiErrors.test.ts`,
  `pagination.test.ts`; `infobases/__tests__/paths.test.ts`, `mapConflictToField.test.ts`.
- **Страничные сценарии** — `InfobasesPage.unassigned.test.tsx`,
  `InfobasesPage.missing.test.tsx`.

### Запуск

```
pnpm test           # vitest run — однократный прогон
pnpm test:watch     # vitest — watch-режим
pnpm type-check     # tsc -b --noEmit
pnpm lint           # eslint .
```

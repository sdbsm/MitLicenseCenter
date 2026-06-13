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
  features/               — 14 фич (см. ниже)
  components/
    layout/               — AppShell, Sidebar, Topbar, ThemeToggle
    ui/                   — shadcn/radix примитивы (button, dialog, form, …)
    PageFallback.tsx
    PaginationBar.tsx
  lib/
    api.ts                — fetch-обёртка, ApiError, ApiSchemaError
    apiErrors.ts          — matchConflictCode, toastFormSubmitError
    apiSchema.ts          — omittable(), pagedResponseSchema()
    queryClient.ts        — единственный QueryClient со стандартной политикой
    useInvalidatingMutation.ts — фабрика мутаций с инвалидацией
    pagination.ts         — pageLinkRange()
    utils.ts              — cn() (clsx + tailwind-merge)
  i18n/
    index.ts              — инициализация i18next (lng: "ru")
    ru.json               — единственный файл локализации
  test/setup.ts           — jsdom-заглушки (ResizeObserver, matchMedia, …)
```

### 14 фич (`features/`)

| Фича | Страница / роль |
|---|---|
| `audit` | `/audit` — журнал операций |
| `auth` | `/login`, `ProtectedRoute`, `ForcePasswordChange` |
| `backups` | диалог бэкапов на карточке инфобазы |
| `dashboard` | `/` — KPI-карточки, здоровье хоста |
| `discovery` | `DiscoveryField` — общий компонент автоподстановки |
| `infobases` | `/infobases` — CRUD инфобаз и публикаций |
| `performance` | `/performance` — метрики хоста, 1С, SQL |
| `profile` | форма смены пароля (в ForcePasswordChange и профиле) |
| `publications` | мутации публикации/смены платформы/проверки IIS |
| `reports` | `/reports` — график потребления лицензий |
| `sessions` | `/sessions` — live-снимок сеансов, kill |
| `settings` | `/settings` (Admin) — параметры системы |
| `tenants` | `/tenants` — CRUD клиентов |
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

**Ошибки.** Не-2xx → `ApiError(status, message, body)`. Сообщение берётся из тела:
поле `detail`, `title` или `message`; иначе `"HTTP {status}"`. Тело 409 Conflict
доступно через `readConflictBody(error)` (`ConflictBody.code + detail`) для маппинга
в ошибку поля формы.

### 3.2 `lib/queryClient.ts`

Один глобальный `QueryClient` с политиками:
- `staleTime: 30_000` мс по умолчанию;
- `refetchOnWindowFocus: false` (live-данные задают свои интервалы явно);
- retry: ≤ 2 попытки кроме 401/403 (0 попыток); мутации — без retry.

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
queryKey: [...tenantsQueryKey, { page, pageSize }]
queryKey: [...infobasesQueryKey, { tenantId, publishStatus, page, pageSize }]
```
Мутации инвалидируют по корневому префиксу (`["tenants"]`, `["infobases"]`) —
это покрывает все страницы и фильтры разом.

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
| `useHostMetrics` (performance) | 5 с | live-метрики хоста |
| `useDashboardHostHealth` | 45 с | «есть ли проблема», не «какая» |
| `useBackups` | 5 с | poll прогресса Queued→Running→Succeeded |
| `useMe` | нет (`false`) | меняется только при логине/выходе |
| discovery-запросы | нет | `staleTime: 5 мин`, кэш до перефетча |

Все «живые» запросы используют `placeholderData: (prev) => prev` — экран не
мигает скелетоном между poll-ами.

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
```

### 5.2 Parity с BE-валидацией

`features/infobases/validation.ts` — единый источник правил формы инфобазы на
фронте; закреплён parity-тестом `validation.test.ts`, парным бэкенд-тестам
(`InfobasesValidationTests.cs`). Правила: regex версии платформы
`/^\d+\.\d+\.\d+\.\d+$/`, max-длины полей (200/200/200/200/50/260),
виртуальный путь начинается с `/`, без пробелов.

Типы на FE выводятся из zod-схем (`z.infer`): `types.ts` не дублирует
интерфейсы вручную.

### 5.3 Критичные границы (runtime-валидация схемой)

Runtime-валидация через `api(..., { schema })` включена только там, где расхождение
контракта означало бы ошибку авторизации или потерю операционных данных:
`currentUserSchema` (роли), `sessionsSnapshotResponseSchema`,
`infobaseListResponseSchema`, `tenantListResponseSchema`, `hostMetricsSnapshotSchema`,
`backupListSchema`. Остальные запросы используют `payload as T`.

---

## 6. Формы: react-hook-form + zod

### 6.1 Паттерн диалога

Все диалоги CRUD следуют одной схеме (пример — `TenantFormDialog`):
1. zod-схема строится функцией `buildSchema(t)`, принимающей `t` из `useTranslation` —
   тексты ошибок локализованы в момент построения.
2. `useForm<FormValues>({ resolver: zodResolver(buildSchema(t)), defaultValues })`.
3. `onSubmit = form.handleSubmit(async (values) => { ... })`.
4. При 409 Conflict — `matchConflictCode(error, { CODE: { field, messageKey } })` →
   `form.setError(field, { type: "server", message: t(key) })`.
5. Прочие ошибки — `toastFormSubmitError(error, t)` (400 → серверное сообщение, иначе generic).

### 6.2 Сброс состояния при повторном открытии

Для сброса состояния формы при каждом открытии диалога применяется key-паттерн:
диалог монтируется с `key={dialogOpenFlag ? "open" : "closed"}` или аналогичным
уникальным ключом, что заставляет React пересоздать компонент и вызвать
`defaultValues` заново.

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
  /reports        — ReportsPage
  /performance    — PerformancePage
  /audit          — AuditPage
  /settings       — ProtectedRoute (requireAdmin) → SettingsPage
  /users          — ProtectedRoute (requireAdmin) → UsersPage
  /*              — Navigate to /
```

### 7.2 `ProtectedRoute`

`ProtectedRoute` опрашивает `useMe()` (`GET /api/v1/auth/me`):
- Загрузка → спиннер.
- Ошибка / нет данных → `<Navigate to="/login" replace />`.
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

---

## 8. i18n

SPA поддерживает только русский язык.

`i18n/index.ts` инициализирует i18next с одним ресурсом:
`resources: { ru: { translation: ru } }`, `lng: "ru"`, `fallbackLng: "ru"`.
Ключ `returnNull: false` гарантирует, что пропущенный ключ вернёт строку,
а не `null`.

`i18n/ru.json` — единственный файл UI-текстов; все строки интерфейса берутся
оттуда через `useTranslation()` и хук `t(key)`.

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

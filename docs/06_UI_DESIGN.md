# UI Design

This document defines the visual language and interaction patterns for the Control Panel. It is operational, not aspirational — every rule here exists because something concrete in this product depends on it.

The component library is locked to **shadcn/ui** (see ADR-11). This document fills the gaps shadcn doesn't dictate: status semantics specific to our domain, table patterns, destructive-action UX, polling freshness, Russian microcopy.

## 1. Design Philosophy

- **Admin tool, not consumer product.** Density first, decoration second. Operators spend hours per day here — wasted vertical space is a productivity tax.
- **Read at a glance.** Status of every object (Tenant, Infobase, Publication, Session) must be readable without clicking through. Color + icon + label, in that order of redundancy.
- **Destructive actions are loud.** Kill Session, Reconcile, Delete, Disable Admin — these are red, confirmed in a modal, and audited. Never wired to a single-click button.
- **Real-time is honest.** Data refreshes via polling (~15s on the Sessions Monitor). The UI never lies about freshness — every live view shows when it last updated and visibly indicates a stale state.
- **Desktop-first.** Target resolution baseline `1366×768` (lowest realistic admin workstation); design works up to `2560×1440`. Mobile is not a target in v1; layouts may break gracefully on narrow screens but no effort is spent.

## 2. Stack (locked alongside Shadcn)

| Concern | Choice |
| --- | --- |
| Component primitives | **shadcn/ui** (copy-paste, owned in repo) on top of **Radix UI** |
| Styling | **Tailwind CSS** |
| Icons | **lucide-react** (shadcn default) |
| Data tables | shadcn `Table` rendered directly (hand-rolled `.map()` + a shared `PaginationBar`). `@tanstack/react-table` is a declared dependency but **not used in v1** — there is no headless sort/filter/visibility state yet. |
| Forms | **react-hook-form** + **zod** for validation |
| Toasts / notifications | **sonner** (shadcn default) |
| Charts | `recharts` is a declared dependency but **not wired in v1** — the Dashboard renders metrics with `Card` + `Progress`, no chart components yet. |
| Date formatting | **date-fns** + `date-fns/locale/ru` |
| i18n | **react-i18next**, single `ru.json` (see `01_PROJECT_CONTEXT.md`) |

No additional component libraries (no MUI, Ant, etc. mixed in). If shadcn doesn't provide it, we build it on top of Radix + Tailwind in the same style.

## 3. Color & Semantics

We use shadcn's CSS-variable theme system unchanged for **structural** colors (`background`, `foreground`, `border`, `muted`, `accent`, etc.) and define a small **semantic palette** for status. The dark-mode CSS variables are defined, but **v1 ships no theme switcher** — there is no `ThemeProvider` or toggle, and the app follows the system preference only (`next-themes` is pulled in transitively by the sonner toaster, never driven by a UI control). A user-facing light/dark toggle is a future addition, not a v1 feature.

Semantic colors map 1:1 to states across the entire app. **Same status, same color, every screen.**

| Semantic | Tailwind base | Used for |
| --- | --- | --- |
| `success` | `emerald-600` / dark `emerald-500` | `InSync`, `Active`, RAS `Healthy` |
| `warning` | `amber-600` / dark `amber-500` | `Maintenance`, license consumption 75–90% |
| `danger` | `red-600` / dark `red-500` | `Drift`, RAS `Unhealthy`, `Suspended`, `Error`, license consumption ≥ 90%, all destructive buttons |
| `info` | `sky-600` / dark `sky-500` | `Missing`, `Unknown`, informational badges |
| `neutral` | `zinc-500` | `Viewer` role chip, disabled, "no data" |

**Status badge component** (`<StatusBadge variant="...">`) is the single way to render any status anywhere — never inline `<span className="bg-green...">`. This is a convention upheld by review; there is **no ESLint rule enforcing it** in v1.

## 4. Typography

- **Font family:** Inter (variable). Fallback stack: `Inter, ui-sans-serif, system-ui, -apple-system, "Segoe UI", Roboto, sans-serif`. Cyrillic glyphs in Inter are excellent — no fallback to system fonts needed.
- **Monospace** for IDs, session IDs, cluster IDs, file paths: `JetBrains Mono` or `ui-monospace` fallback. Used in tables for `SessionId`, `ClusterInfobaseId`, `wsisapi.dll` paths.
- **Hierarchy:** shadcn's defaults are fine — `text-4xl` (page title), `text-2xl` (section), `text-base` (body), `text-sm` (table body, default in admin density), `text-xs` (captions, timestamps).
- **Russian copy is 20–40% longer than English.** Always test labels with full Russian strings before committing to a button width. Reserve space for "Согласовать состояние" (16 chars), not "Reconcile" (9). Never truncate action button labels.

## 5. Layout

```
┌────────────────────────────────────────────────────┐
│ Topbar: product name • env tag • user menu          │
├──────────────┬─────────────────────────────────────┤
│              │                                     │
│  Sidebar     │           Content area              │
│  (nav)       │                                     │
│              │           max-width: none           │
│              │           (use full width)          │
│              │                                     │
└──────────────┴─────────────────────────────────────┘
```

- **Sidebar:** shadcn `Sidebar` component, collapsible to icons. Sections grouped by domain — `Operations` (Dashboard, Sessions, Publications), `Configuration` (Tenants, Infobases), `System` (Audit). There is **no Administrators entry in v1** (see §3.6 of `05_UI_REQUIREMENTS.md` — admin self-management is not built; the single seeded admin manages everything).
- **Content area uses full available width.** Admin tables benefit from horizontal real estate; do not centre-cap to `max-w-7xl` like marketing sites do.
- **Page header** in every content view: title (h1), subtitle/description (muted), and primary action button(s) top-right.
- **No breadcrumbs in v1.** Two-level nav (sidebar group → page) is shallow enough.

## 6. Tables (the dominant pattern)

Sessions, Audit, Infobases, Publications — all tables (and the future Administrators screen will follow the same pattern). Single canonical pattern:

- **Built on:** shadcn `Table` rendered directly. Sort/filter/pagination are hand-managed in component state; `@tanstack/react-table` is **not used in v1**.
- **Density:** compact (`text-sm`, `py-2` rows), fixed. There is **no density toggle** in v1 (no localStorage preference).
- **Filter bar above the table:** free-text search left, segmented status filter centre, date-range right. Filter state lives in component state only — it is **not serialized to URL query params** in v1, so filtered views are not shareable by link.
- **Column visibility:** columns are fixed; there is **no column-visibility menu** in v1.
- **Pagination:** server-side for every list backed by a paged endpoint — Audit, Clients, Infobases (and the per-client infobase list on the tenant drill-down). Each fetches one page via `?page=&pageSize=` and renders the `{ items, total, page, pageSize }` envelope; a shared `PaginationBar` (range summary `«from–to из total»` + page-number links) shows only when `total > pageSize`, and a previous page stays on screen while the next loads (no skeleton flash). Audit offers a `25 / 50 / 100` page-size selector; the Clients/Infobases lists use a fixed page size of 25. The «По клиенту» grouping toggle groups the **current page** of infobases.
- **Sticky header** when scrolling.
- **Row hover** highlights the row; click does NOT navigate (avoids accidents). Navigation is via an explicit `<ChevronRight>` icon column or an action menu.
- **Action menu per row** (`...` button → shadcn `DropdownMenu`) for row-level actions. Bulk actions (rare in this product) sit above the table only when at least one row is selected.
- **Empty state** inline within the table: icon, one-line headline ("Нет активных сеансов"), one-line hint ("Сеансы появятся здесь, когда пользователи подключатся к опубликованным базам.").

## 7. Destructive Actions

Anything that kills a session, reconciles a publication, disables an admin, or deletes a record:

- **Always** triggers a shadcn `AlertDialog` (not `Dialog` — the variant is important — focus traps and the destructive default).
- **Default focused button is "Отмена".** Never the destructive option. This is the one rule that prevents the "I hit Enter by reflex and killed prod" class of incident.
- **The destructive button uses `variant="destructive"`** (red), labelled with the verb in imperative ("Завершить сеанс", "Согласовать", "Отключить администратора"). Never just "ОК".
- **Modal copy is concrete, not generic.** Bad: "Вы уверены?". Good: "Сеанс пользователя ИВАНОВ И.И. в базе УТ-Демо будет немедленно завершён. Несохранённые данные пользователя будут потеряны."
- **For high-impact actions** (Reconcile that recycles an app pool, Delete Tenant) — require typing a confirmation token (the object's name) before the destructive button enables. shadcn doesn't ship this; we build it as a small wrapper around `AlertDialog`.

## 8. Live Data & Freshness

The Sessions Monitor and Dashboard poll. The UI must communicate freshness honestly:

- **Per-view "last updated" indicator:** small muted text top-right of the content area — "Данные обновлены 3 сек назад" via a `<RelativeTime>` component that re-renders on a 1-second tick. Tooltip shows the exact timestamp.
- **Refetch spinner** integrated into the indicator, not a giant page overlay. Polling refetches must never flash the entire screen.
- **Stale state:** if React Query reports `isStale` and the last successful fetch is > 60 seconds old, the indicator goes amber ("Данные устарели") and a subtle banner appears with a retry button.
- **Error state:** if N consecutive polls fail, the indicator goes red ("Не удалось получить данные") with the last successful timestamp still visible. The previously fetched data stays on screen — never blank the table on a transient backend hiccup.

## 9. States: Loading / Empty / Error / Forbidden

| State | Pattern |
| --- | --- |
| **Loading (first visit)** | shadcn `Skeleton` blocks shaped like the eventual content. No spinners on full pages. |
| **Loading (refetch)** | Existing data stays visible; small spinner in the freshness indicator only. |
| **Empty** | Centered icon + headline + one-line hint + optional primary CTA. See table empty-state pattern in §6. |
| **Error (page-level)** | Inline error card: icon (`AlertTriangle`), what failed in user terms, retry button. Never a stack trace; never a raw HTTP code. |
| **Forbidden (Viewer)** | Destructive controls don't render at all (not greyed). The view itself remains available. If a Viewer reaches a forbidden URL directly, show a polite page: "У вас нет прав на этот раздел. Обратитесь к администратору." |

## 10. Icons

- **Library:** lucide-react only.
- **Conventional icons used across the app** — establish these once and reuse:
  - Tenant → `Building2`
  - Infobase → `Database`
  - Publication → `Globe`
  - Session → `MonitorPlay`
  - Audit → `ScrollText`
  - Drift / warning → `AlertTriangle`
  - Healthy / synced → `CircleCheck`
  - Error / RAS unhealthy → `CircleX`
  - Maintenance → `Wrench`
  - Reconcile → `RefreshCcw`
  - Kill / terminate → `Power`
  - Administrator → `ShieldCheck`
- Icon size in tables: `h-4 w-4`. In page headers: `h-5 w-5`. In empty states: `h-12 w-12 text-muted-foreground`.

## 11. Accessibility & Density

- **Contrast:** WCAG AA minimum on all text (4.5:1 normal, 3:1 large). Status colors must pass against both light and dark backgrounds — the Tailwind 600/dark-500 split above is chosen for exactly this reason.
- **Keyboard:** every interactive element reachable by Tab; `Esc` closes modals; `Enter` confirms the *focused* button (which for destructive dialogs is "Отмена"); arrow keys navigate menus and tables.
- **Focus rings:** shadcn's default (two-tone ring), never disabled.
- **Hit targets:** minimum 32×32 px for clickable elements in tables (the density tradeoff has a floor).

## 12. Russian Microcopy Dictionary

Use these exact phrasings throughout the UI for consistency. **Do not invent synonyms** ("Удалить" vs "Стереть" vs "Убрать" — pick one and stick with it).

### Common actions
| Action | Russian |
| --- | --- |
| Save | Сохранить |
| Cancel | Отмена |
| Apply | Применить |
| Delete | Удалить |
| Edit | Изменить |
| Add | Добавить |
| Create | Создать |
| Confirm | Подтвердить |
| Close | Закрыть |
| Refresh | Обновить |
| Export | Экспорт |
| Import | Импорт |
| Filter | Фильтр |
| Search | Поиск |
| Reset | Сбросить |
| Details | Подробнее |
| Back | Назад |

### Domain actions
| Action | Russian |
| --- | --- |
| Terminate session | Завершить сеанс |
| Reconcile publication | Согласовать состояние |
| Check drift now | Проверить сейчас |
| Disable administrator | Отключить администратора |
| Reset password | Сбросить пароль |
| Assign infobase | Назначить базу |
| Suspend tenant | Приостановить клиента |

### Statuses
| English | Russian |
| --- | --- |
| Active | Активна / Активен |
| Maintenance | Обслуживание |
| Suspended | Приостановлен |
| InSync | Соответствует |
| Drift | Расхождение |
| Missing | Отсутствует |
| Error | Ошибка |
| Healthy | В норме |
| RAS Healthy | OK |
| RAS Unhealthy | Сбой |
| RAS Probing | Проверка… |

### Frequent phrases
- "Данные обновлены N сек назад" — freshness indicator.
- "Данные устарели" — stale state.
- "Не удалось получить данные" — fetch error.
- "Нет данных" — empty table generic.
- "У вас нет прав на это действие" — forbidden inline.
- "Действие необратимо." — destructive modal warning line.

### Error message tone
- Address the user formally (`Вы`, не `ты`).
- Describe what failed, not what we tried. Bad: "Ошибка при выполнении запроса POST /api/v1/sessions/.../kill". Good: "Не удалось завершить сеанс — сервер 1С недоступен. Попробуйте через минуту."
- Never apologize ("К сожалению..."). Concise, neutral.

## 13. Out of Scope (v1)

- Customizable dashboards / draggable widgets.
- User-pinnable views or favorites.
- In-app guided tours.
- Print-friendly layouts.
- Mobile-optimized layouts.
- Multiple themes beyond light + dark.

When any of these is requested, it becomes its own ADR with a real cost/benefit analysis — not snuck in.

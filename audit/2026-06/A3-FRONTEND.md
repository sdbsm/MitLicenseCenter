# A3 — FRONTEND: независимый аудит

Этап A3 пред-релизного аудита. Дата: **2026-06-12**, репозиторий `F:\dev\MitLicense Center`,
HEAD `e6b1317` (та же база, что A0). Режим — read-only; первоисточник — код, не доки.
Объём: `frontend\src` (271 файл .ts/.tsx, 14 фич) + сверка с контрактами
`backend\src\MitLicenseCenter.Web\Endpoints`. Входная фактура — `audit/2026-06/A0-BASELINE.md`
(71 эндпоинт, тесты FE 355/355 зелёные).

Метод: 6 независимых субагентов по зонам (контракты — opus; кэш-инвалидация — opus;
формы, типобезопасность/гарды, качество тестов — sonnet; i18n-инвентаризация — haiku),
кандидаты Blocker/High прошли отдельного субагента-скептика. Выборочные факты
(i18n-ключи, JSON-политика) перепроверены куратором по коду.

---

## Резюме

**Blocker: 0 · High: 1 · Medium: 10 · Low: ~10.**

Фронтенд в целом зрелый и единообразный: централизованная инвалидация
(`lib/useInvalidatingMutation.ts`), все мутации через `mutateAsync` в `try/catch`
(fire-and-forget `mutate()` не найден вовсе), на каждой странице-списке есть
error-ветка с повтором, 0 `any`/`@ts-ignore` в прод-коде, auth-гарды корректны,
i18n без пользовательских багов. Исторический урок «API опускает null-поля →
`.nullish()`» внедрён **системно** на схемных границах (хелпер `omittable()` +
регрессионные тесты на ответах без ключей; голого `.nullable()` в прод-схемах нет
вообще), но держится на конвенции, а не на машинном гейте, и не покрывает
эндпоинты без схемы.

---

## Ключевые проверенные факты

- **Null-omission подтверждена:** `backend\src\MitLicenseCenter.Web\Program.cs:32` —
  `DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull` (глобально);
  `Program.cs:35` — `JsonStringEnumConverter`, все enum'ы сериализуются строками.
  Все FE-union/`z.enum` сверены с BE-enum'ами — расхождений нет.
- **`omittable()`** (`frontend\src\lib\apiSchema.ts:11`) = `.nullish().transform(v => v ?? null)` —
  используется во всех прод-схемах с nullable-полями; grep `.nullable()` по прод-схемам — 0
  (только предостерегающие комментарии). Wire-fixture-тесты «ответ без null-ключей» есть для
  backups, performance (×4), dashboard host-health, lib/apiSchema.
- **Schema-валидация ответов** включена только на «критичных границах» (MLC-016):
  auth/me+login, sessions/snapshot, списки infobases/tenants/users/backups/unassigned,
  performance host/onec/sql/recordings. Остальные ~35 вызовов — слепой `payload as T`
  (`lib/api.ts:121`).
- **Auth-гарды:** `/settings` и `/users` — `<ProtectedRoute requireAdmin>` (router.tsx:56–65) +
  скрытие пунктов меню (Sidebar.tsx:66–72); прямой URL у Viewer → редирект на `/`.
  `mustChangePassword`-гейт в внешнем ProtectedRoute оборачивает весь AppShell — обход
  навигацией невозможен (ProtectedRoute.tsx:32–34). В localStorage — только тема
  (`mlc-theme`); токенов/паролей в storage нет, auth — HttpOnly cookie.
- **Retry-политика** (`lib/queryClient.ts`): до 2 ретраев, 401/403 не ретраятся, мутации без
  retry, staleTime 30с, refetchOnWindowFocus off. Polling: dashboard/sessions/host — 5с,
  корректно гейтится `enabled` и размонтированием; утечек не найдено.
- **Parity-валидация инфобаз** (`InfobaseValidationRules.cs` ↔ `validation.ts`): 10 из 11
  правил совпадают построчно (regex платформы, 6 длин, virtualPath-правила); parity-тесты —
  честные golden-таблицы (12 кейсов regex, включая одноцифровой build 8.5), каждая сторона
  гоняет свою реальную реализацию, не заглушки. Одно расхождение — FE-06 ниже.

---

## Findings

### High

**[FE-01] Фича profile (смена пароля + ForcePasswordChange) — 0% тестового покрытия** ·
**High** · Evidence: `frontend\src\features\profile\` — тест-файлов нет (grep
`ChangePasswordForm|changePassword` по `**/*.test.*` — 0); `ProtectedRoute.test.tsx:11–14`
мокает `ForcePasswordChange` заглушкой; BE-тест `AuthEndpointsTests.cs:77–94` покрывает
только успешный путь · Суть: `ChangePasswordForm.tsx` — единственный путь смены пароля,
работает в двух контекстах (диалог Topbar и блокирующий экран после сброса пароля админом).
Не покрыты: маппинг 400-ошибок на поля (`FIELD_BY_BACKEND_KEY`, ключи в двух регистрах,
`ChangePasswordForm.tsx:21–26`), cross-field `superRefine`, и главное — цепочка
`ForcePasswordChange.tsx:38–40` `onSuccess → invalidateQueries(ME_KEY)`, единственный
механизм снятия блокирующего экрана · Риск: регресс в этой цепочке оставит пользователя
запертым на экране форс-смены после успешной смены пароля — и не будет пойман ни одним
тестом (ложная уверенность от 355 зелёных) · Рекомендация: тест `ChangePasswordForm.test.tsx`:
успех → инвалидация `/me` + закрытие; 400 → ошибки на полях (оба регистра ключей); 400 без
маппинга → generic toast; cross-field mismatch; сценарий ForcePasswordChange ·
Confidence: high. **Подтверждено скептиком.**

### Medium

**[FE-02] Создание/удаление инфобазы не инвалидирует счётчик баз у клиента** · **Medium**
(понижено скептиком с High) · Evidence: `features\infobases\useInfobases.ts:73–79` (create),
`:101–106` (delete) — инвалидируют только `infobasesQueryKey`; `infobaseCount` — серверное
поле схемы tenants (`features\tenants\types.ts:11`), рендерится в колонке «Баз»
(`TenantsPage.tsx:178`); reassign при этом инвалидирует оба ключа (`useInfobases.ts:89–99`) ·
Суть: непоследовательность — reassign делает правильно, create/delete нет · Риск: оператор
создал/удалил базу → перешёл на «Клиенты» в пределах 30с staleTime → видит старый счётчик;
tenants не поллится, refetchOnWindowFocus выключен · Рекомендация: добавить
`tenantsQueryKey` в `invalidate` обеих мутаций (по образцу reassign) · Confidence: high.

**[FE-03] Смена лимита лицензий клиента не освежает «Отчёты»** · **Medium** · Evidence:
`features\tenants\useTenants.ts:41–47` — `useUpdateTenant` инвалидирует только
`tenantsQueryKey`; `useLicenseUsage.ts:22` — reports без polling · Суть:
`maxConcurrentLicenses` влияет на проценты использования в reports; дашборд самовыправится
(polling 5с), reports — нет · Риск: оператор правит лимит и сразу сверяет квоты в отчётах —
видит старые проценты до 30с + ре-навигации · Рекомендация: добавить `reportsQueryKey` в
`invalidate` · Confidence: medium (зависит от того, считает ли BE проценты от текущего лимита).

**[FE-04] Ошибка drill-down в «Отчётах» маскируется под «данные накапливаются»** ·
**Medium** · Evidence: `features\reports\useReportsPage.ts:24` возвращает `detail`, но
`ReportsPage.tsx:50–57` не читает `detail.isError`; `ReportsDetail.tsx:82–91` при `!data`
рендерит `ReportsEmptyState` · Суть: упавший запрос ряда по клиенту неотличим от пустого
200; сводка (`summary.isError`) при этом обработана правильно · Риск: при сбое (500/таймаут/
schema-drift) оператор делает ложный вывод «по клиенту данных нет» · Рекомендация:
пробросить `detail.isError` и показать error-ветку с повтором, как у summary ·
Confidence: high.

**[FE-05] `ApiSchemaError` нигде не обрабатывается и не логируется** · **Medium** ·
Evidence: бросается в `lib\api.ts:117`; grep `ApiSchemaError` по `features` — 0
обработчиков; в `lib\queryClient.ts` нет `QueryCache`/`MutationCache` с `onError` · Суть:
управляемая по замыслу ошибка «контракт разошёлся» (ADR-10.1/MLC-016) деградирует до
неотличимого generic «не удалось загрузить»; диагностика (`path`, `issues`) теряется ·
Риск: при дрейфе версий BE↔FE на проде владелец (не программист) не получает сигнала
«несовместимость версий» — худший путь диагностики · Рекомендация: минимум —
`console.error` с `path` через глобальный `QueryCache({onError})`; лучше — отдельный текст
ошибки для этого класса · Confidence: high.

**[FE-06] Parity-расхождение: проверка абсолютности `physicalPathOverride` есть только на
бэке** · **Medium** · Evidence: BE `Endpoints\Shared\InfobaseValidationRules.cs:70–75`
(`Path.IsPathFullyQualified`); FE `features\infobases\validation.ts:65–68` — только max 260,
refine на абсолютность отсутствует; ни один parity-тест это правило не покрывает · Суть:
единственное правило из набора, нарушающее задекларированную в CLAUDE.md парность; дрейф
невидим для CI · Риск: относительный путь проходит клиентскую валидацию и отбивается
сервером — UX хуже остальной формы; прецедент молчаливого рассинхрона parity-набора ·
Рекомендация: добавить `.refine()` на FE + golden-строку в оба parity-теста; если
асимметрия намеренная — оформить `[Doc divergence]` · Confidence: high.

**[FE-07] UserFormDialog не сбрасывается при повторном открытии** · **Medium** · Evidence:
`features\users\UsersPage.tsx:201` — нет `key`; `UserFormDialog.tsx:52–55` — `useForm`
инициализируется однажды; для сравнения `TenantFormDialog` и `InfobaseFormDialog` имеют
`key={editing?.id ?? "create"}` · Суть: key-паттерн применён ко всем диалогам, кроме этого ·
Риск: «призрак» прошлого ввода (логин, роль) при повторном открытии → возможное создание
пользователя с именем из прошлой попытки · Рекомендация: добавить `key` либо
`form.reset()` по `open` · Confidence: high.

**[FE-08] ChangePasswordForm (Topbar) не сбрасывается при повторном открытии** · **Medium** ·
Evidence: `components\layout\Topbar.tsx:103` — Dialog без key; `ChangePasswordForm.tsx:83` ·
Суть: после неудачной попытки и закрытия диалога повторное открытие показывает прошлый
ввод всех трёх полей пароля и старые ошибки `setError` · Риск: UX + заполненные
password-поля прошлой сессии · Рекомендация: key по факту открытия или `form.reset()` при
закрытии · Confidence: high.

**[FE-09] ~35 эндпоинтов без runtime-схемы: TS-типы «врут» про nullable-поля** · **Medium**
(совокупно) · Evidence: `lib\api.ts:121` (`payload as T`); 11 эндпоинтов с nullable
BE-полями под слепым кастом: audit (`AuditContracts.cs:9,12`), dashboard/summary
(`DashboardContracts.cs:24–26`), settings (`SettingsContracts.cs:9–10`), iis
(`IisContracts.cs:15`), discovery (`DiscoveryEndpoints.cs:172–178`), reports
(`ReportsContracts.cs:14`), publications-статусы (`PublicationsContracts.cs:23–24`) · Суть:
поля задекларированы `| null`, а на проводе ключ отсутствует → в рантайме `undefined`;
сегодня все потребители защищены (`??`/`?.` — проверено трассировкой), краш-класса нет ·
Риск: латентный — новый потребитель с проверкой `=== null` молча уйдёт в неверную ветку;
конвенция `omittable()` не закрыта lint-правилом · Рекомендация: поднять
settings/dashboard-summary до схемных границ или честно типизировать поля как
`?: string | null`; рассмотреть eslint-запрет голого `.nullable()` · Confidence: high.

**[FE-10] Discovery-запросы формы инфобаз не гейтятся ролью** · **Medium** · Evidence:
`features\infobases\useInfobaseForm.ts:134–137` — 4 discovery-запроса без `enabled: isAdmin`;
все `/discovery/*` на BE — Admin-only (группа, `DiscoveryEndpoints.cs:28`) · Суть: сейчас
форму открывает только Admin (кнопки скрыты при `!isAdmin`, `InfobasesPage.tsx:294,440`),
но сам компонент изнутри не защищён · Риск: хрупкость — при будущем переиспользовании
формы Viewer получит пачку 403 и деградацию discovery без объяснения · Рекомендация:
`enabled: open && isAdmin` · Confidence: high.

**[FE-11] Пробелы покрытия за пределами profile: kill-сессий, LoginPage, IIS-подсекция;
в тестах хуков схемы не прогоняются** · **Medium** · Evidence: `KillSessionDialog.tsx`,
`LoginPage.tsx`, `features\publications\iis\` (7 файлов) — тестов нет;
`DashboardPage.test.tsx:11` и ряд тестов хуков мокают `api` целиком
(`vi.mock("@/lib/api")`) — zod-схемы в этих тестах не выполняются · Суть: операционно
важные флоу (завершение сеанса с 409-логикой, вход с веткой 401, IIS-действия) без
покрытия; мок `api()` обходит схемную валидацию (частично митигировано отдельными
wire-fixture schema-тестами) · Риск: регрессии в этих флоу не ловятся · Рекомендация:
тесты по образцу `DisableUserDialog`; для критичных хуков — wire-fixture-паттерн как в
`useDashboardHostHealth.test.tsx` · Confidence: high.

### Low (сводно)

**[FE-12] Двойной клик «Начать бэкап»** — `BackupsDialog.tsx:88–95`: окно гонки до
`isPending`; сервер прикрыт 409 `BACKUP_ACTIVE`, последствие — ложный error-тост после
успешного запуска · Confidence: medium.

**[FE-13] Confirmation-поля не сбрасываются при reopen** — `PublishPublicationDialog.tsx:42`
(токен подтверждения остаётся введённым — кнопка публикации сразу активна, подтверждение
теряет смысл; key не меняется при повторном открытии той же публикации) и
`KillSessionDialog.tsx:29–30` (старый reason) · Рекомендация: сброс в `onOpenChange(false)`.

**[FE-14] Settings-поля без inline-ошибки 400** — `DatabaseServerField.tsx:35–47`,
`PlatformPicker.tsx:51–62` (+ `VersionEscapeField`): generic-тост вместо serverError под
полем (у `SettingField`/`RasPortField` сделано правильно); в `handlePick` две
последовательные мутации — при падении второй путь сохранён, версия нет.

**[FE-15] `toastFormSubmitError` при 400 показывает технический title** —
`lib\apiErrors.ts:27–33`: для ValidationProblem это «One or more validation errors
occurred.» без конкретики (актуально для длин, которые ловит только nvarchar БД).

**[FE-16] BE не проверяет max-длины в runtime** — `InfobaseValidationRules.cs:31–76` длины
не режет, DataAnnotations в minimal API не исполняются; практический гейт — FE-zod + nvarchar.
Соответствует задокументированной гоче CLAUDE.md; для прямых API-клиентов — сырое
поведение БД вместо чистого 400.

**[FE-17] Мёртвые i18n-ключи (~12 подтверждённых)** — группа `publications.source.*`
(ru.json:300–302; используется только дубль `infobases.source.*` — `InfobaseRow.tsx:215`),
`sessions.errors.killFailed`, `sessions.filters.infobase`, `infobases.form.section*`,
`infobases.form.clusterIdHint`, `performance.sql.{io,waits}.heading`, часть `common.*`,
`errors.{network,forbidden}`. Первичный список субагента «51 неиспользуемый» куратором
опровергнут выборочно: большинство — ложные срабатывания (ключи-литералы в таблицах
conflict-кодов). Отсутствующих ключей — 0; захардкоженных видимых строк — 0 (одна
служебная в `main.tsx:8` + логотип).

**[FE-18] Мёртвый код / гигиена** — `components\layout\ComingSoonPage.tsx` (сирота),
`features\performance\sqlLoad.ts:82` `isBlocked` (используется только тестами), 8
экспортированных threshold-констант без внешних потребителей; eslint: `src/components/ui/**`
исключён из линтинга (оправдано для shadcn, но кастомные правки там не линтуются),
`no-explicit-any` только наследуется из recommended.

**[FE-19] Хрупкие/недостающие мелочи в тестах** — `StatusBadge.test.tsx:11–14` ассертит
конкретные Tailwind-классы (ложно-красный при дизайн-рефакторе); нет wire-fixture-теста
для `dashboard/summary` (nullable `ras.*`-поля).

---

## Таблица: мутации → инвалидация (сводно)

| Мутация | Инвалидирует | Должна дополнительно | Вердикт |
|---|---|---|---|
| create/update/delete infobase | `infobases` | create/delete: + `tenants` (FE-02) | ⚠ |
| reassign infobase | `infobases`, `tenants` | — | ✓ образец |
| hide/unhide unassigned | `infobases.unassigned` | — | ✓ |
| create/delete tenant | `tenants` | dashboard — самовыправится polling'ом 5с | ✓/инфо |
| update tenant (лимит) | `tenants` | + `reports` (FE-03) | ⚠ |
| publications check/publish/change-platform (+bulk) | `infobases` | — | ✓ |
| IIS recycle/start/stop/restart/reset (9 шт.) | `iis.*` ×3 + `infobases` | — | ✓ отлично |
| kill session | `sessions.snapshot` | dashboard — polling 5с | ✓/инфо |
| start/delete backup | `backups(infobaseId)` | — | ✓ (урок MLC-071 учтён) |
| recordings start/stop/delete | `performance.recordings` | — | ✓ |
| update setting | `settings` | — | ✓ |
| users create/reset/disable/enable/role | `users` | — | ✓ |
| login/logout | `setQueryData(ME_KEY)` / `qc.clear()` | — | ✓ |

Согласованность ключей проверена: рассинхрона «инвалидируем ключ, которым никто не
пользуется» не найдено. Дашборд и сессии держатся на polling 5с, а не на инвалидации —
осознанно, но это стоит помнить при снижении частоты polling.

---

## (а) Топ-5 рисков

1. **[FE-01]** Непокрытая тестами цепочка ForcePasswordChange: регресс молча запирает
   пользователя на блокирующем экране — единственный High.
2. **[FE-05]** `ApiSchemaError` неразличима и не логируется: при дрейфе версий BE↔FE на
   проде диагностика теряется — критично при владельце-непрограммисте.
3. **[FE-02] + [FE-03]** Пропуски кросс-фичевой инвалидации (счётчик баз, проценты в
   отчётах): операторы видят устаревшие цифры сразу после своих действий — подрыв доверия
   к данным панели.
4. **[FE-09]** ~35 эндпоинтов на слепом `as T` с «врущими» nullable-типами; защита —
   конвенция и дисциплина ревью, без машинного гейта — риск растёт с каждым новым
   контрактом.
5. **[FE-04]** Ошибка drill-down в отчётах неотличима от «данных нет» — ложные выводы по
   конкретному клиенту.

## (б) Вердикт

**Готово с оговорками.** Blocker'ов нет; единственный High — пробел тестового покрытия, а
не дефект поведения. Архитектурная дисциплина (инвалидация, обработка ошибок мутаций,
parity-валидация, omit-null-схемы, auth-гарды) — на уровне выше среднего и подтверждена
кодом, а не доками. Оговорки: до релиза желательно закрыть FE-01 (тест на смену пароля)
и точечные FE-02/FE-04/FE-07/FE-08 (каждый — правка в несколько строк); FE-05 и FE-09 —
кандидаты в первый пострелизный спринт.

## (в) Что не покрыто этим этапом

- **Динамическая проверка**: аудит статический (код), приложение не запускалось — реальное
  поведение форм/инвалидации в браузере, e2e-сценарии, визуальные регрессии не проверялись.
- **Производительность FE**: размер бандла (3610 модулей по A0), code-splitting,
  ре-рендеры, поведение таблиц на больших объёмах — вне объёма.
- **Доступность (a11y)** и клавиатурная навигация — не проверялись.
- **Внутренности `components/ui/**`** (shadcn-генерат, исключён из eslint) — выборочно.
- **Экспорт xlsx/jspdf** — проверен только на уровне типов, корректность генерируемых
  файлов не проверялась.
- Список «неиспользуемых i18n-ключей» проверен выборочно (подтверждена ~дюжина), полная
  ручная верификация всех кандидатов не проводилась.
- Сверка контрактов опиралась на чтение кода обеих сторон субагентом; машинной генерации
  типов из Swagger (которая дала бы 100%-гарантию) в проекте нет — это же отмечено как
  системный риск FE-09.

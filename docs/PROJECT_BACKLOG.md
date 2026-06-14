# MitLicense Center — Project Backlog

Единый активный реестр задач. Этот файл **постоянно поддерживается в актуальном состоянии**
и читается первым при каждом следующем запуске работы над проектом.

Закрытые задачи перечислены **компактным индексом** в `docs/PROJECT_BACKLOG_ARCHIVE.md`
(одна строка на задачу: суть + дата + якорь-коммит); полные постановки и отчёты несёт
git-история (коммиты `MLC-NNN:` и PR) — детали достаются по инструкции в шапке архива.
Этот файл и архив держим тонкими, чтобы не жечь контекст при каждом старте. Канон проекта
(`docs/01..06 + DECISIONS.md + ROADMAP.md + OPERATIONS.md`) —
источник правды по архитектуре v1. Бэклог не дублирует канон, а фиксирует улучшения поверх него.

## Как пользоваться

1. Прочитать этот файл.
2. Найти задачу, помеченную `NEXT TASK`.
3. Выполнить **только её**.
4. После выполнения задачи: статус `Done`, **полный отчёт — в описании закрывающего PR
   и в теле коммита** (не в архив-файл — он остаётся тонким индексом); в секции трека здесь
   остаётся **сжатая Done-строка** (1–3 строки: что построено + гочи, нужные *следующим
   задачам этого трека*) — передаточная записка, не летопись.
5. После закрытия трека: куратор добавляет в `PROJECT_BACKLOG_ARCHIVE.md` по **одной
   строке-индексу на каждую задачу трека** (суть + дата + якорь-коммит, как в шапке архива)
   и удаляет секцию трека из реестра; в индексе «Завершённые треки» остаётся одна строка,
   актуализируется счётчик в «Закрыто». Развёрнутые отчёты живут в git, не в архив-файле.
6. Не выполнять больше одной задачи за сессию без отдельного указания.

Цель схемы: реестр читается при старте каждой сессии и должен оставаться ~150–200 строк
независимо от возраста проекта — растёт во время трека, схлопывается при его закрытии.

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

**Рефакторинг-трек (2026-06-03)** — план долгосрочного улучшения поддерживаемости, полные спеки
в `C:\Users\andre\.claude\plans\distributed-orbiting-snail.md` (ID `REF-01..REF-13`). Phase 1–2
(`MLC-029..035`) **закрыт полностью**; Phase 3–4 (REF-08..13 → `MLC-025/026/027/011(a)/028/036`)
остаётся gated на триггеры — см. «Открытые опции».

---

## Завершённые треки — индекс

Компактный индекс закрытых задач (по треку, с якорями-коммитами) — в
`PROJECT_BACKLOG_ARCHIVE.md`; развёрнутые отчёты, методика и развилки треков — в git-истории
(коммиты `MLC-NNN:`/PR, поиск по `MLC-NNN` или названию трека); спеки — в план-файлах
`.claude/plans/`. Здесь — только указатели.

- **Аудит безопасности и дефектов** — `MLC-001..019` (2026-06-01..03).
- **Техдолг и поддерживаемость** — `MLC-020..024` (2026-06-03); `MLC-025..028` → «Открытые опции».
- **Рефакторинг-трек Phase 1–2** — `MLC-029..035` (2026-06-03..04); спека `distributed-orbiting-snail.md`.
- **Перф-трек Phase 1–2 (PERF-01..07)** — `MLC-037..043` (2026-06-04..05); PERF-08+ gated.
- **Вне треков:** `MLC-044` hot-enforce ≤5с (2026-06-05); `MLC-045`/`MLC-046` webinst-публикации +
  bulk (2026-06-05); `MLC-047` управление IIS, ADR-24 (2026-06-06); `MLC-053` reset-admin (2026-06-07);
  `MLC-072` диагностика метрик «Быстродействия» — числа корректны (2026-06-09); `MLC-074`
  retention-джобы под execution strategy (2026-06-09).
- **Трек «Отчёты — использование лицензий»** — `MLC-048..050` 3/3 (2026-06-06), раздел `/reports`,
  ADR-25; спека `concurrent-purring-kahn.md`; + `MLC-054` полировка (2026-06-07).
- **Трек «Экспорт отчётов»** — `MLC-051..052` 2/2 (2026-06-07), CSV/XLSX/HTML/PDF;
  спека `adaptive-scribbling-quiche.md`.
- **Трек «Полировка /settings»** — `MLC-055..056` 2/2 (2026-06-07..08); спека `1-2-rippling-zephyr.md`.
- **Трек «Полировка панели v1.1»** — `MLC-057..059` 3/3 (2026-06-08), тема + «Администраторы» +
  форс-смена пароля; спека `eventual-hopping-pebble.md`.
- **Мини-трек «Раздел Пользователи»** — `MLC-060..061` 2/2 (2026-06-08), переименование + смена роли;
  спека `users-section-rename-roles.md`.
- **Трек «Анализ быстродействия 1С»** — `MLC-063..071` 9/9 (2026-06-08..09), раздел `/performance`:
  host-снимок → 1С-сеансы → SQL live + запись по требованию (ADR-26);
  спека `spicy-discovering-torvalds.md`; UX-полировка `MLC-073` → «Открытые опции».
- **Трек «Резервное копирование баз SQL»** — `MLC-075..078` 4/4 (2026-06-09..10), on-demand
  COPY_ONLY-бэкапы + keep-latest (ADR-15 изменён, ADR-27); спека `lazy-cuddling-finch.md`;
  хвосты `MLC-079`/`MLC-080` влиты в UX-трек.
- **Трек «UX-пересборка панели под single-host», этап 1** — `MLC-081..086` 6/6 (2026-06-10),
  frontend-only: «Базы» = Инфобазы + Публикации (вкладка «IIS», `/publications` удалена), форма
  без выбора сервера, /settings без «Значений по умолчанию» (секция «SQL Server»), профиль в
  топбаре + сайдбар 8 пунктов, дашборд-«Обзор» (кликабельные KPI + здоровье хоста), канон 05/06
  переписан; +влитые `MLC-079`/`MLC-080`; аудит-спека `.claude/plans/ux-audit-single-host.md`;
  тег отката `v1-pre-ux-redesign`.
- **Трек «UX-пересборка, этап 2: single-host бек-чистка»** — `MLC-087..091` 5/5 (2026-06-10),
  по подтверждению single-host пользователем: ключ `Sql.Server` — единственный источник
  SQL-инстанса (discovery без `server=`), колонка `Infobase.DatabaseServer` и ключ
  `OneC.Cluster.Server` удалены (миграции), фильтр `publishStatus` на «Базах», **ADR-28
  «Single-host topology»** + канон 01/03/04 present-tense; хвост-тест полноты §7 пройден;
  тег `v1-pre-singlehost-stage2`, бэкапы стендовой БД — `F:\MlcStage2Backups`.
- **Трек «Нераспределённые базы: discovery-first добавление»** — `MLC-092..094` 3/3
  (2026-06-11), закрытие слепой зоны (а) дрейфа панель↔кластер: `GET /infobases/unassigned`
  (diff RAS − заведённые − скрытые, TTL-кэш 60 с), игнор-лист `HiddenClusterInfobases` +
  hide/unhide + аудит 14/15, баннер-счётчик и диалог разбора на «Базах», «Добавить базу» =
  discovery-first, **ADR-29** + канон 03/04/05/06; live e2e на стенде (обе роли), бэкап БД
  `MitLicenseCenter_20260611_pre-mlc092.bak`; спека — план-файл
  `glittery-floating-turtle.md` (локальный, у куратора).
- **Трек «Обратный дрейф панель↔кластер»** — `MLC-095..097` 3/3 (2026-06-11), закрытие
  слепой зоны (б): `MissingItems[]` в ответе `/infobases/unassigned` (обратный diff из
  того же снапшота RAS, только при `Available:true`), красный баннер + метка `danger`
  «Не найдена в кластере» на строке + диалог с удалением существующим флоу; без таблиц/
  миграций/джобов/аудит-кодов; ADR-29 Update-нота + канон 04/05/06. **Пункт ROADMAP
  «Дрейф панель↔кластер» закрыт полностью** (оба направления, вместе с `MLC-092..094`);
  спека — часть 2 того же план-файла.
- **Трек «GUI-установщик (Inno Setup)»** — `MLC-098..104` (2026-06-11): бэкенд сам отдаёт SPA
  same-origin (**ADR-30**, IIS не нужен для хостинга панели) → self-contained single-file publish
  (`publish-release.ps1`) → GUI-установщик Inno Setup (`installer/MitLicenseCenter.iss` +
  `build-installer.ps1`, **ADR-31**): одна Windows-служба (service-aware `UseWindowsService`,
  `MLC-104`), мастер SQL/учётные данные (Windows-аккаунт или SQL-логин, тест подключения),
  оператор задаёт пароль admin, апгрейд поверх, деинсталл с keep-data prompt, ярлык «Пуск»;
  ADR-14 update-ноты (packaging ≠ CD). Спеки — план-файлы `.claude/plans/installer-track.md` +
  `mlc-098..104`; полные отчёты — `PROJECT_BACKLOG_ARCHIVE.md`.
- **Вне трека (follow-up к «GUI-установщик»):** `MLC-105` Done (2026-06-11) — мастер
  распознаёт уже-инициализированную целевую БД (fail-open проба `auth.Users` тем же приёмом,
  что тест подключения; флаг `DbAlreadyInitialized` — один раз при уходе со страницы «Сеть»):
  предупреждает + пропускает страницу пароля admin (как на апгрейде), `.secret` не пишет;
  «чистая установка» ≠ «пустая БД». Канон — **ADR-31** + `OPERATIONS`. Только `.iss`;
  план `.claude/plans/mlc-105-installer-existing-db.md`; отчёт — `PROJECT_BACKLOG_ARCHIVE.md`.
- **Вне трека (follow-up к «GUI-установщик»):** `MLC-106` Done (2026-06-11) — bootstrap создаёт БД
  на пустом инстансе. **Гоча:** EF `MigrateAsync` под `EnableRetryOnFailure` НЕ создаёт несуществующую
  БД (4060 ретраится как транзиентная → краш) — поэтому ранний сырой `CREATE DATABASE` к `master`
  (`DatabaseBootstrapper.EnsureDatabaseCreatedAsync`, `IF DB_ID IS NULL`, имя — параметром +
  экранирование `]`→`]]`) в `Program.cs` сразу после `builder.Build()`, ДО Hangfire-регистрации/Migrate/
  сидера; гейт — непустая `ConnectionStrings:Default` (InMemory-тесты пропускают). Канон — **ADR-18** +
  `OPERATIONS`; план `.claude/plans/mlc-106-bootstrap-create-db.md`; отчёт — `PROJECT_BACKLOG_ARCHIVE.md`.
- **Вне трека (follow-up к «GUI-установщик»):** `MLC-107` Done (2026-06-11) — установщик: надёжное
  создание службы + чистый конфиг. Создание/конфиг/старт службы и firewall перенесены из `[Run]` в
  `[Code]` (`ConfigureService` в ssPostInstall) с проверкой `rc` `sc.exe`: на провале `sc create` —
  внятный `MsgBox` (особо **1057** — в Windows-режиме введён SQL-логин → подсказка выбрать
  SQL-аутентификацию) + `RaiseException` (откат; больше нет «успеха без службы»); `sc start` —
  предупреждение со ссылкой на Event Log. `appsettings.Production.json` сносится `[UninstallDelete]` и
  на **чистой** установке перезаписывается (skip-if-exists — только апгрейд, `ServiceExists`). Подписи
  режимов A/B — яснее. Только `.iss`. Канон — **ADR-31** + `OPERATIONS`; план
  `.claude/plans/mlc-107-installer-service-robustness.md`; отчёт — `PROJECT_BACKLOG_ARCHIVE.md`.
- **Трек «Пред-релизные фиксы по итогам аудита»** — `MLC-108..115` 8/8 (2026-06-12..13):
  закрытие findings пред-релизного аудита А0–С1. Детерминированный состав артефакта (REL-01),
  немедленный отзыв доступа через SecurityStampValidator (SEC-01), NTFS-ACL секретов +
  честный ADR-8 «key ring plaintext» (KEYRING-01в/SEC-02/04), явный выбор клиента в форме
  привязки (UX-42), страховка апгрейда — экран бэкапа + провал старта службы = ошибка (REL-02),
  полное снятие IIS-публикации (`webinst -delete` за ADR-20, эндпоинт + UI + аудит
  `PublicationUnpublished=23`, UX-43), пакет валидации/наблюдаемости (BE-03/05/10 +
  аудит `LoginFailed=108` + UX-01-остаток), `THIRD_PARTY_LICENSES.txt` в поставку + тесты
  profile/ForcePasswordChange (REL-10 + FE-01). Спека — `.claude/plans/audit-fix-prerelease.md`;
  индекс задач — `PROJECT_BACKLOG_ARCHIVE.md`; вердикт/findings — `audit/2026-06/MASTER-REPORT.md`.
  **После трека (вне задач, ведёт куратор):** ручной прогон Setup.exe на чистой ВМ (слепая зона
  аудита) → этап D1 «Документация v2» (промт `.claude/plans/audit-prerelease-prompts.md`;
  выровнять security-слой и 02, терминологию DPAPI→plaintext в ADR-21/CLAUDE.md) → перенос
  roadmap R1–R12 из `TECH-DEBT.md` в `ROADMAP.md`.
- **Вне трека (follow-up ВМ-проверки к установщику):** `MLC-116` Done (2026-06-13) — апгрейд:
  служба реально останавливается перед заменой файлов; ложная ошибка 1056 устранена.
  **Первопричина:** `StopServiceAndWait` передавал в `ControlService` access right
  `SERVICE_STOP=$0020` вместо control-кода `SERVICE_CONTROL_STOP=$0001` → `ERROR_INVALID_PARAMETER`,
  служба не останавливалась → restart-manager «файлы заняты» + после RM-перезапуска `sc start`=1056.
  Фикс: верный control-код; `sc start` rc=1056 (ALREADY_RUNNING)=успех (не предупреждение, не
  `ServiceStartFailed`); тексты «не запустилась» вместо ложного «создана» на апгрейде; чистая
  установка (create/1057/RaiseException/ACL/.secret) не тронута. Только `.iss`; ISCC-компиляция
  `[Code]` зелёная, живой апгрейд-прогон на ВМ — за владельцем. Отчёт — PR #138 / коммиты `MLC-116:`.
- **Вне трека (follow-up ВМ-проверки к установщику):** `MLC-117` Done (2026-06-13) — публикация видит
  `OneC.RAS.Endpoint` сразу после установки (раньше падала «Не задан адрес 1С-кластера» до пересохранения).
  **Первопричина:** у ключа не было сидового дефолта → на свежей установке `ValueText=null`, а FE
  (`RasPortField`/`parseRasPort`) рисовал фантомный «1545» при неактивной «Сохранить» → значение
  не персистилось; discovery работал (rac.exe молча → localhost при пустом endpoint),
  публикация падала (`WebinstArgs.ResolveClusterServer` бросает на пустом). Оба пути делят
  один TTL-снапшот — расхождения чтения нет. **Фикс:** `DefaultValue: "localhost:1545"` в
  `SettingDefinitions` (свежая БД) + `SettingsSeeder.HealRasEndpointAsync` — одноразовый heal
  существующей пустой строки на апгрейде (дефолт из каталога, только при пустом `ValueText`+null
  `Value`, идемпотентно, чужое не трогает). `ResolveClusterServer` не менялся (ошибка остаётся для
  реально пустого). Без миграций/FE/parity. Отчёт — PR #140 / коммиты `MLC-117:`.

---

## Активный трек: «Пострелизный рефакторинг» (R2–R12)

Цель — закрыть весь остаточный техдолг аудита по итерациям `docs/ROADMAP.md` «Backlog /
deferred». Каждая итерация R-N = одна задача = один PR. Первоисточник детализации находок —
`audit/2026-06/TECH-DEBT.md`. Порядок — по ROADMAP (зависимости учтены).

Уже закрыто: **R1–R10** полностью (`MLC-118..128`; R9 — кластеры `MLC-125..127`; R10-остаток —
`MLC-128`). Остаётся **R11** (масштабируемость списков — в работе, разбита на кластеры) и **R12**
(полировка UX). Отмечено в ROADMAP значком ✅.
Номера задачам присваиваются по мере выдачи (бэклог тонкий — одна `NEXT TASK` за раз).

**Done-строки трека (передаточные записки):**
- **`MLC-118` — R2: Валидационный барьер (BE+FE, parity) ✅** (2026-06-13). Рантайм-хелперы
  валидации (`InfobaseValidationRules.AppendInfobaseFieldErrors`/`AppendPublicationFieldErrors`)
  теперь режут длину и опасные символы (DataAnnotations `[StringLength]` в minimal API в рантайме
  не срабатывают — гоча CLAUDE.md). Единые предикаты символов BE↔FE: `Name` — connstr-safe (`; = "`),
  `DatabaseName` — без path/служебных метасимволов и `..`, `VirtualPath` — без `\`/`..`,
  `PhysicalPathOverride` — абс. путь + без `..`/`; = "`. Defense-in-depth в `WebinstArgs.BuildConnStr`.
  Невалидный ввод → 400 с ошибкой поля (не 500). Канон — 03 §3.5; parity golden-таблицы расширены
  синхронно. Гоча для R3+: предикаты живут в `InfobaseValidationRules` (BE) и `validation.ts` (FE) —
  новые правила полей добавлять там же, обе стороны + golden-таблицы. **R2 закрыт полностью.**
- **`MLC-119` — R3: Аудит-целостность (BE-01/BE-11/BE-25) ✅** (2026-06-13). Введён
  **enlist-примитив**: `IAuditLogger.Enlist(...)` / `EndpointHelpers.EnlistAudit(...)` кладёт
  аудит-запись в общий tracked-`AppDbContext` **без своего `SaveChanges`** — запись коммитится
  тем же `SaveChangesAsync`, что и действие (атомарно: оба или ничего). `LogAsync` = `Enlist` +
  `SaveChanges` (DRY). Опора — DI-факт: `AppDbContext`+`IAuditLogger` оба Scoped → внутри одного
  scope (web-запрос / Hangfire-джоба) контекст логгера и эндпоинта/джобы — один инстанс. **BE-01:**
  свип всех мутационных call-site'ов; инвертированный порядок «аудит ДО действия» был у **двух**
  `DeleteAsync` — `Tenants` и `Infobases` (оба переведены на enlist + единый финальный
  `SaveChanges`); остальные — action-first (без риска ложной записи), не тронуты;
  `PublicationUnpublished` в Infobase-delete оставлен action-first (аудитит необратимый внешний
  webinst-side-effect). **BE-11:** оживлён `LimitChanged = 201` (int заморожен, не переназначался) —
  `TenantsEndpoints.UpdateAsync` дописывает доп. событие при фактической смене `MaxConcurrentLicenses`
  (старое→новое в описании), `TenantUpdated` остаётся. **BE-25:** `KillEnforcer` — `Enlist` в цикле +
  один `SaveChanges` за цикл (короче держит замок). Гоча для следующих задач: новый аудит, парный
  с мутацией в **том же** `AppDbContext`, писать через `EnlistAudit`/`Enlist` (атомарно); standalone
  и внешне-side-effect аудит (Auth/Settings/Sessions/webinst) — через `LogAsync`/`AuditAsync`
  (action-first). **R3 закрыт полностью.**
- **`MLC-120` — R4: Тестовые слепые зоны (safety-net, только тесты) ✅** (2026-06-13).
  Закрыт остаток R4 без правок продакшн-поведения. **BE-09:** поведенческие тесты
  `PUT /publications/{id}` (`PublicationsUpdateValidationTests`, Stage-2) — длина/метасимволы
  → 400 ValidationProblem с ошибкой поля (не 500), happy-path → 200 + аудит. **BE-12:**
  юнит-тест OEM/CP866-декода `SystemProcessRacRunner` (round-trip kill-маркера + контрпример
  mojibake как UTF-8); декод не менялся. **BE-14:** freeze `AuditActionType` расширен до **всех
  43 членов** + reflection-инвариант полноты (новый член без `[InlineData]` роняет тест).
  **BE-24:** `EnforcementGateTests` переведены с `Task.Delay` на `TaskCompletionSource`
  (timezone-assert/smoke-пустышка не обнаружены). **FE-11:** тесты kill-сессий, LoginPage,
  IIS-подсекции с реальным прогоном Zod-схем. **FE-19:** `StatusBadge` — поведенческий ассерт
  (`data-variant`) вместо Tailwind-классов; **добавлена толерантная Zod-граница
  `dashboardSummarySchema`** (omit-null `ras.*`, урок `api-omits-null-fields`) — заведена в
  `useDashboardSummary` (слепой `api<T>()`-каст заменён; поведение для валидных ответов
  сохранено). Минимальные поведение-сохраняющие правки продакшна (видимость private→internal,
  `data-variant`, dashboard-граница) санкционированы куратором. **R4 закрыт полностью.**
  Гоча/хвост: **BE-15/DOC-32** (стейл-комментарий «UTF-8» в шапке `SystemProcessRacRunner` при
  фактическом OEM/CP866-декоде) найден по ходу, **не чинился** — тест BE-12 защищает от действий
  по нему; правка комментария отдельной задачей (несёт R10-остаток / куратор).
- **`MLC-121` — R5: Офлайн-UX и обратная связь ✅** (2026-06-13). Внятная обратная связь при
  сетевых сбоях/недоступности и единый паттерн ошибок. **UX-03/FE-05:** класс `ApiNetworkError`
  (обёртка `fetch`-reject) — корень различения «нет связи» vs HTTP-ошибка; глобальная
  классификация на `QueryCache`/`MutationCache.onError` (`classifyError` в `lib/queryClient.ts`):
  `ApiNetworkError` → module-store `connectionStatus` → глобальный `ConnectionBanner` (живой
  `errors.network`, снимается первым успехом), `ApiSchemaError` → обособленный
  `console.error("[ApiSchemaError]", path, issues)` (диагностика FE↔BE-дрейфа без телеметрии);
  `ProtectedRoute` больше не разлогинивает молча при нет-связи/схеме — экран «Повторить» (refetch),
  реальный 401 ведёт на /login прежним onUnauthorized-путём. **UX-04:** `applyFieldErrors(error,
  setError, fieldMap?)` в `lib/apiErrors.ts` — разбор 400 ValidationProblem (ключи бэка PascalCase,
  публикация с префиксом `Publication.`, нормализация first-letter-lowercase по сегментам) в
  inline-ошибки полей; порядок в формах **409-code → 400-field → generic-тост**; подключено в
  TenantFormDialog/UserFormDialog/useInfobaseForm (раскрывает блок «Дополнительно» при ошибке его
  поля); экран входа получил form-level inline (`role="alert"`). **UX-17:** RAS-карточка дашборда —
  видимая actionable-подсказка + ссылка в «Параметры» при `!healthy` (завязана на
  `ras.healthy`/`consecutiveFailures`), `lastErrorMessage` остаётся во вторичном тултипе. **UX-44:**
  текст ошибки публикации (`WebinstArgs.ResolveClusterServer`) без технического ключа
  `OneC.RAS.Endpoint` — направляет в раздел «Параметры» (флоу публикации не тронут; корень устранён
  в `MLC-117`). Канон 05/06 обновлён; правил валидации/parity-таблиц/enum аудита не трогали.
  **Гоча для R6+:** новый паттерн обратной связи на FE — сетевые сбои уже идут через
  `ApiNetworkError`+баннер, схемные через `[ApiSchemaError]`-лог; формам 400-валидацию связывать
  через `applyFieldErrors` (не плодить generic-тосты). **R5 закрыт полностью.**
- **`MLC-122` — R6: Видимость лимитов ✅** (2026-06-14). Единый визуальный язык «нарушитель
  квоты» за пределами дашборда; frontend-only (backend/DTO/parity/enum не тронуты). **UX-02:**
  `frontend/src/lib/quota.ts` — **единственный источник** порогов (`QUOTA_WARNING_THRESHOLD=75`,
  `QUOTA_DANGER_THRESHOLD=90`) + маппинги `severity → StatusBadge`-вариант / класс прогресс-бара;
  акцент на `/tenants` (колонка «Лицензии»: `consumed/limit (percent%)` + badge), карточке
  `/tenants/:id` и `/reports` (ReportsStats); дашборд `progressColorClass` переведён на тот же
  хелпер (пороги больше не дублируются). Бейдж-лейблы — `common.quota.exceeded/nearLimit` (общий
  текст вместо `reports.stats.peak*`). **Источник потребления на клиента — live-оверлей на FE:**
  `useTenantConsumption()` поверх `useSessionsSnapshot()` агрегирует `Map<tenantId, consumed>`
  чистой `buildConsumedByTenant` (`consumesLicense===true`, группировка по `tenantId`) — намеренный
  небольшой дубль канонического backend `LicenseConsumption.CountByTenant`; тот же снапшот питает
  дашборд, значения совпадают; контракт клиента не расширялся. **FE-03:** `useUpdateTenant`
  инвалидирует `[tenantsQueryKey, reportsQueryKey]`; `reportsQueryKey` вынесен в
  `features/reports/reportsQueryKeys.ts` против циклического импорта `useTenants → useReportsPage`.
  Гоча: `Limit`/`peakLimit` в отчётах исторические (пишутся на момент снапшота) — инвалидация даёт
  когерентность кэша, ретроактивного пересчёта истории нет. **UX-46:** ссылка «Открыть карточку
  клиента» из drill-down `/reports`; список `/tenants` и топ-клиенты дашборда уже ведут на
  `/tenants/:id`; sidebar остался статичным. Канон 05 §4.4/4.5 + 06 §7. **R6 закрыт полностью.**
  Гоча для R6-потребителей: новые экраны с акцентом квоты берут пороги/маппинг только из
  `lib/quota.ts`; потребление на клиента на FE — через `useTenantConsumption` (не дублировать
  агрегацию снапшота).
- **`MLC-123` — R7: Устойчивость джобов и службы ✅** (2026-06-14). **BE-19:** TTL-reaper
  зависших `Running`-бэкапов в насосе (`BackupOrchestrator.ReapStuckRunningAsync`, каждый
  5-сек тик `BackupPumpService`): `Running` старше `StuckRunningTimeout=6ч` →
  `Failed`/`TimedOut` (новый `BackupFailureReason.TimedOut=6`, **append-only**; FE-паритет
  массива + i18n) **И снятие in-memory замка-на-базу** (иначе база выпадала из бэкапов до
  рестарта); race-guard в `CompleteAsync` (`is { Status: Running }`) против позднего возврата
  зависшего `Task.Run`; аудит `BackupFailed`/`System` только при count≥1. **BE-20:**
  **мисконцепция аудита** — `CancellationToken.None` в `RecurringJob.AddOrUpdate` это
  идиоматический плейсхолдер Hangfire (1.8 подменяет его реальным shutdown-токеном),
  регистрации НЕ трогали; зафиксировано комментарием в `Program.cs` + характеризующим тестом
  (тело джобы уважает `ct`). **BE-21:** осознанный `[AutomaticRetry]` на методах интерфейсов
  джоб (3+`Fail` для суточного housekeeping audit/license/backup-retention; `0` для
  `publication-status-refresh` и `cold-snapshot`). **REL-22:** `JobRetentionStateFilter`
  истекает `Failed` за 30д (раньше — никогда; `Succeeded`/`Deleted` остались 2д). **REL-03:**
  установщик задаёт recovery-политику (`sc failure …`, основной механизм, не зависит от
  размещения SQL) + производную **локальную** SQL-зависимость (`DeriveLocalSqlServiceName`:
  `.`/`localhost`/`(local)`→`MSSQLSERVER`, `.\NAME`→`MSSQL$NAME`, `SERVER\NAME`/удалённый→
  пропуск); жёсткий `depend=MSSQLSERVER` отвергнут (ломает именованные/удалённые инстансы).
  Канон — **ADR-40** (операц. модель устойчивости службы) + **ADR-35** расширен
  (retry-политика / 30д `Failed` / graceful shutdown) + `OPERATIONS` §1 и §7. **Гоча для
  R8+:** `CancellationToken.None` в рекуррентных джобах НЕ «чинить» (идиоматический
  Hangfire-плейсхолдер, подменяется реальным shutdown-токеном); `Failed`-джобы Hangfire теперь самоочищаются
  за 30д; `BackupFailureReason` ints заморожены — новые причины только в конец. **R7 закрыт
  полностью.**
- **`MLC-124` — R8: Релизный конвейер ✅** (2026-06-14). Воспроизводимая, автоматически
  проверяемая сборка релизного артефакта; только скрипты/`.iss`/CI/шаблон/канон (продуктовый код
  не тронут). **DOC-08:** в шаблон `backend/src/MitLicenseCenter.Web/appsettings.Production.json`
  (ручной деплой) добавлен `"Urls": "http://+:8080"` — без него Kestrel слушал бы дефолтный
  `localhost:5000`; путь GUI-инсталлятора (`WriteProductionConfig` в `.iss` пишет `Urls` сам) не
  затронут. **REL-13:** секция `[InstallDelete]` в `.iss` чистит `{app}\wwwroot\assets\*` ДО
  раскладки `[Files]` — старые хэшированные SPA-ассеты не накапливаются (риск «белого экрана»
  снят); key ring/БД (`{commonappdata}`) и `appsettings.Production.json` (корень `{app}`) вне
  `wwwroot` — не затронуты; на чистой установке секция безвредна. **REL-20:** личный путь к ISCC
  убран из `build-installer.ps1` — резолв `-IsccPath` → `$env:ISCC_PATH` → PATH → стандартные
  каталоги Inno Setup 6. **REL-12:** новый `release.yml` (`workflow_dispatch` + push тега `v*`):
  job на `windows-latest` ставит Inno Setup (`choco`), гонит `build-installer.ps1` (publish→ISCC),
  загружает Setup.exe-артефакт. **REL-14/REL-21:** `dependabot.yml` (NuGet/npm/Actions, weekly);
  `ci.yml` получил `pnpm format:check` фронта, информационные аудиты `dotnet list package
  --vulnerable` / `pnpm audit` (`continue-on-error`), `paths-ignore` для пропуска чисто-doc
  изменений; SDK-чеклист перед релизом — `DEVELOPMENT` §6. Канон — **ADR-14** update-нота +
  `DEVELOPMENT` §5/§6 + `OPERATIONS` §2. **Гоча (биллинг):** Actions красный — YAML авторинг,
  вступает в силу при включении биллинга; гейт остаётся локальный `build.ps1` (зелёный, 798 BE +
  486 FE тестов) + ручной прогон релизного пути. Живая ISCC-компиляция `.iss` — за владельцем
  (ISCC на сборочной машине не установлен). **Хвост (не блокер):** per-job paths-split
  (backend-job не гонять на чисто-frontend и наоборот) — только doc-skip; полный per-job фильтр
  требует `dorny/paths-filter` (отложено — Actions по биллингу не работает). **R8 закрыт
  полностью.** Гоча для R9: установщик-hardening меняет тот же `.iss` — учитывать новую
  `[InstallDelete]` и резолв ISCC в `build-installer.ps1`.
- **`MLC-125` — R9a: Security-барьер бэкенда (SEC-07 + SEC-08)** (2026-06-14). Первый кластер
  R9 (крупная задача разбита куратором на кластеры). Только backend + тесты + канон; продуктовый
  контракт/DTO/parity/enum аудита не тронуты. **SEC-07:** middleware `SecurityHeaders.UseSecurityHeaders`
  (новый класс `Web/Security/SecurityHeaders.cs`) ставит `X-Content-Type-Options: nosniff`,
  `Referrer-Policy: no-referrer` на **все** ответы, а `X-Frame-Options: DENY` + `Content-Security-Policy`
  — на все, **кроме** `/api/docs*` (Swagger UI на inline-скриптах). Middleware — рано в пайплайне:
  после `UseExceptionHandler`, ДО `UseStaticFiles` (заголовки на SPA/ассеты/fallback). CSP жёстко
  зашит (single-host): `script-src 'self'` (без unsafe-inline — Vite-бандл без inline-script),
  `style-src 'self' 'unsafe-inline'` (React CSS-in-JS). **SEC-08:** `AddRateLimiter` per-IP fixed
  window (политика `"login"`, PermitLimit=10/мин, partition по RemoteIp) + `UseRateLimiter` (после
  авторизации, до эндпоинтов) + `.RequireRateLimiting("login")` только на `POST /auth/login`;
  превышение → **429** ДО тела хендлера (аудит `LoginFailed` НЕ пишется — reject до SignInManager).
  Поверх Identity-lockout (ADR-36): lockout защищает учётку, rate-limit троттлит источник.
  **Тест-инфра (впервые в проекте):** `WebApplicationFactory<Program>` (пакет
  `Microsoft.AspNetCore.Mvc.Testing`, `FrameworkReference Microsoft.AspNetCore.App`) — фабрика
  `MlcWebApplicationFactory` поднимает реальный хост под средой **"Test"** с EF InMemory; Program.cs
  пропускает регистрацию рекуррентных Hangfire-джоб и сидеров под `IsEnvironment("Test")` (оба
  требуют реального SQL: `JobStorage.Current`/`MigrateAsync`) — `AddInfrastructure`/`AddHangfire`
  жёстко требуют непустую строку подключения, поэтому DB-less загрузка невозможна, фабрика даёт
  фейковую непустую строку. Канон — **ADR-41** (security headers) + **ADR-42** (login rate limiting)
  + `SECURITY.md` §2/§8 + `02_ARCHITECTURE` (пайплайн) + `OPERATIONS` (Transport hardening). `build.ps1`
  зелёный (822 BE + 486 FE). **Гоча для R9-остатка:** заголовки/лимит зашиты в коде (не tuneable);
  CSP — единый источник в `SecurityHeaders.cs`, при правке синхронить ADR-41/SECURITY/OPERATIONS
  (три текстовые копии); новые интеграционные тесты пайплайна — через `MlcWebApplicationFactory`
  (среда "Test"). **R9 — кластер 1/3 закрыт; остаётся аккаунт службы и чистка поставки/firewall.**
- **`MLC-126` — R9b: Чистка поставки + firewall hardening (REL-08 + SEC-06 + REL-17) ✅** (2026-06-14).
  Второй кластер R9. Только скрипты/`.iss`/канон; продуктовый код/parity/enum не тронуты. **REL-08:**
  `publish-release.ps1` подавляет артефакты information-disclosure общими publish-аргументами
  (`-p:DebugType=none -p:DebugSymbols=false` → нет `*.pdb`; `-p:IsTransformWebConfigDisabled=true` →
  нет `web.config`, мёртвого при ADR-30 SPA-хостинге) в обоих режимах, дефензивно удаляет
  `appsettings.Development.json` (чистого publish-аргумента нет) и имеет собственный sanity-чек состава
  (throw до упаковки); `build-installer.ps1` — второй рубеж: forbidden-чек расширен на
  `*.pdb`/`appsettings.Development.json`/`web.config` (ловит и stale-каталог при `-SkipPublish`);
  `.iss` `[Files] Excludes` — defense-in-depth те же три маски. **SEC-06:** `GetFirewallAddParams` —
  `profile=domain,private` (порт закрыт на Public; Domain/Private = штатный LAN, не регрессит);
  `remoteip=` намеренно не задаётся (localsubnet сломал бы multi-subnet LAN — документирован как
  опция). **SEC-09 (решение куратора):** localhost-bind по умолчанию в мастер **НЕ** вносился (потребовал
  бы новый wizard-контрол, не ISCC-проверяемый локально, риск LAN-флоу) — зафиксирован как
  **документированная ручная опция** в `OPERATIONS` (bind `http://localhost:<порт>` за реверс-прокси +
  TLS); укладывается в Критерии готовности (firewall сужен по профилю; bind/LAN не регрессит; описано в
  каноне). **REL-17:** **ADR-43** — Inno Setup свободна для коммерческого использования (баннер
  «non-commercial» — ошибочная посылка finding'а; ключ не нужен); REL-17 закрыт как «нет проблемы».
  Канон — ADR-43 + `OPERATIONS` (firewall/bind) + `INSTALL` (правило брандмауэра). `build.ps1` зелёный
  (822 BE + FE; изменённые файлы вне поверхности сборки). **Гоча:** живая ISCC-компиляция `.iss` — за
  владельцем (ISCC на сборочной машине отсутствует), Pascal/Excludes вычитаны глазами. **R9 — кластеры
  1–2/3 закрыты; остаётся аккаунт службы (MLC-127).**
- **`MLC-127` — R9c: Аккаунт службы (REL-06 + SEC-05) ✅** (2026-06-14). Завершающий кластер R9.
  Только текст подписи `.iss` + канон; привилегии службы/режимы не менялись (SEC-05 — фиксация риска,
  не код). **REL-06:** mode-A подпись `PageCreds.SubCaptionLabel` (`CurPageChanged`) расширена явными
  требованиями к сервис-аккаунту: (1) SQL-доступ + предупреждение, что «Проверить подключение» тестит
  под админом-установщиком, не под службой (зелёное ≠ служба стартует); (2) IIS — членство в
  Administrators ИЛИ read на `%windir%\system32\inetsrv\config`, иначе публикации в «Ошибка проверки»;
  (3) право «Вход в качестве службы». Пост-установочный чек-лист режима A — в `INSTALL` §4. **SEC-05:**
  **ADR-44** — привилегии повышены осознанно (функции IIS/SQL/DMV требуют по природе); узкий ACE на
  `inetsrv\config` требует runtime-трассировки `ServerManager` на стенде → переход на low-priv аккаунт
  **отложен** (триггер: проведена трасса). **Хвост MLC-126 устранён:** `SECURITY.md` §7 и §9 п.4
  приведены в соответствие с кодом (firewall `profile=domain,private`, не Public; принятым риском
  остаётся только отсутствие `remoteip=`-сужения). Канон — ADR-44 + `SECURITY.md` §6/§7/§9 + `INSTALL` §4.
  `build.ps1` зелёный (822 BE + 486 FE). **Гоча:** ISCC за владельцем; визуальную вместимость
  расширенной mode-A подписи проверить в живом мастере (та же инфа продублирована в INSTALL/SECURITY).
  **R9 закрыт полностью (3/3 кластера).**
- **`MLC-128` — R10-остаток: верификация (DOC-10 / BE-15 / FE-17-18 / REL-11) ✅** (2026-06-14).
  Сборная зачистка хвостов R10. **DOC-10:** `SettingsSeeder` EventId `1002`→`1100` (диапазон по
  компоненту: Identity `100x`, Settings `110x`; коммент-конвенция в обоих seeder'ах) — коллизия с
  `IdentitySeeder.1002` устранена; таблица EventId в `OPERATIONS` §3 синхронизирована, примечание о
  коллизии убрано (doc-driven, тот же PR). **BE-15/DOC-32:** шапка `SystemProcessRacRunner.cs`
  приведена к реальности (OEM/CP866-декод с откатом UTF-8 по ADR-3.3) вместо стейл-«UTF-8»; декод не
  менялся (тест BE-12 сторожит); ADR-3.3 уже корректен. **FE-17/18:** удалена сирота `ComingSoonPage.tsx`
  + мёртвый ключ `common.comingSoon`. **REL-11:** §3/§4 `OPERATIONS` верифицированы (полны после D1) —
  правка только таблицы EventId. `build.ps1` зелёный. Гоча: EventId логов — НЕ аудит-enum (не заморожены).
  **R10 закрыт полностью.** Исполнитель оборвался на пункте 1 — куратор доделал механический остаток
  (BE-15/FE-17-18) в том же worktree + прогнал гейт.
- **`MLC-129` — R11a: Аудит — масштабируемые фильтры и навигация (UX-20/35/37/38) ✅** (2026-06-14).
  Первый кластер R11. BE+FE+parity+канон. **UX-20:** query-параметры `search` (подстрочный по
  `Description`+`Initiator`) и `initiator` (точное совпадение) в `AuditEndpoints.ListAsync`, валидация
  длины ≤200 → ValidationProblem; FE — поле поиска (debounce 300мс) + поле инициатора в `AuditFiltersBar`,
  URL-state (`auditUrlState.ts`), сериализация в `useAuditLog.buildQuery`. **UX-35:** в `AuditPagination`
  — кнопки первая/последняя + ввод номера страницы с clamp в [1, totalPages]. **UX-37/38:** новый
  переиспользуемый `components/ui/SearchableSelect.tsx` (Popover+Input+фильтр, ARIA combobox/listbox; БЕЗ
  новой зависимости) — применён к спискам действий и клиентов фильтра аудита (sessions-фильтр получит его
  в R11c). Канон 05 §7.4. **Гоча/урок (память `efcore-stringcomparison-not-translated`):** исполнитель
  применил `Contains(term, StringComparison.OrdinalIgnoreCase)` — это **рантайм-краш** на SQL Server (EF
  Core не транслирует StringComparison-перегрузку; InMemory-тесты маскировали). Куратор поймал на ревью по
  доке MS → переведено на plain `Contains`→`LIKE`, регистр за CI-collation БД, CA1862 подавлён; CI-тест
  переписан в фиксацию границы collation. `build.ps1` зелёный (BE 831 + FE 491). **Гоча для R11b/R11d:**
  любой EF-поиск (LIKE) — только plain `Contains`, НЕ StringComparison-перегрузка.

## NEXT TASK

> **`MLC-130` — R11b: Пагинация `/backups` + `/performance/recordings` (BE-17) и поиск клиентов на `/tenants` (UX-05).**
> Backend+Frontend · P3 · Medium. Второй кластер R11 (R11a закрыт `MLC-129`). Точный маппинг находок
> (исправлено куратором — A1·BE-17 ≠ A4·UX-05):
> - **BE-17** (списки без пагинации): `BackupsEndpoints.ListAsync` (`Ok<IReadOnlyList<BackupSummary>>`,
>   фильтр `infobaseId`, `OrderByDescending(RequestedAtUtc)`) и `PerformanceEndpoints.ListRecordingsAsync`
>   (`Ok<IReadOnlyList<RecordingSummary>>`, без фильтров, `OrderByDescending(StartedAtUtc)`) материализуют
>   **всю** таблицу без `Skip/Take`. Перевести оба на **серверную пагинацию** по принятому в проекте
>   шаблону (`AuditEndpoints`/`InfobasesEndpoints`: query `page`/`pageSize`, `AllowedPageSizes`/`MaxPageSize`,
>   `CountAsync` + `Skip/Take`, paged-record `{ items, total, page, pageSize }`). Контракты
>   `BackupsContracts`/`PerformanceContracts` дополнить paged-record (образец `AuditPagedResponse`).
>   ВАЖНО: `RecordingSummary` — host-уровневая запись, **с клиентом НЕ связана** → поиска по клиенту у
>   recordings нет (только пагинация). У backups при желании можно добавить подстрочный поиск по
>   `DatabaseName` (опционально, plain `Contains`).
> - **UX-05** (High, locus — `/tenants`): на странице клиентов **нет поиска**. `TenantsEndpoints.ListAsync`
>   уже пагинирован (page/pageSize), но без `search`. Добавить query-параметр `search` (подстрочный по
>   `Tenant.Name`, **plain `Contains`→`LIKE`, НЕ StringComparison** — память
>   `efcore-stringcomparison-not-translated`; регистр за CI-collation; валидация длины) + поле поиска на
>   `TenantsPage` (debounce, сброс page→1). Дашборд/иные потребители `useTenants` не ломать.
> - **FE:** `BackupsPage`/recordings-страница — на пагинацию (переиспользовать `PaginationBar`/
>   `AuditPagination`-паттерн); `useBackups`/`useRecordings` — page/pageSize в query + paged-тип;
>   `TenantsPage` — поле поиска.
> Parity BE↔FE: типы/Zod ответов и query-сериализация — синхронно; **API опускает null-поля** →
> nullable как `.nullish()` (память `api-omits-null-fields`). Замороженные enum (`PerfRecordingStatus`/
> `PerfRecordingStopReason`/`BackupFailureReason`) НЕ трогать. Канон 05 (бэкапы/быстродействие/клиенты) +
> 04 (контракты эндпоинтов) — в том же PR.
> Критерии готовности: `/backups` и `/performance/recordings` пагинированы сервер-сайд; `/tenants` имеет
> рабочий поиск по имени (plain Contains); FE-таблицы листаются/ищут; parity-тесты зелёные; канон обновлён;
> `build.ps1` зелёный. Исполнитель — субагент (worktree); модель выбирает куратор.
>
> **План кластеров R11** (R11a ✅ `MLC-129`):
> - **R11b `MLC-130`** — пагинация /backups+/recordings (BE-17) + поиск клиентов на /tenants (UX-05). ← _текущий_
> - **R11c `MLC-131`** — таблица сеансов + диалог «не найдены»: пагинация и сортировка (UX-14/15);
>   sessions-фильтр клиентов получает `SearchableSelect` (хвост UX-38).
> - **R11d `MLC-132`** — FE-09: Zod-схемная валидация ~35 эндпоинтов вместо `payload as T`
>   (raw `api<T>`-касты в ~20 хуках); расширяет 3 текущие Zod-границы (ADR-10.1). Учесть omit-null.
> _Источник детализации — `ROADMAP.md` R11; первоисточник находок — `TECH-DEBT.md`._

**Отложенные опции** — см. «Открытые опции» ниже (`MLC-025/026/027`, `MLC-062`, `MLC-073`);
инфраструктурные отложенности (RAS Strategy B / `MLC-036`, multi-node, UI-долги канона 06,
PERF-08+, серверный фильтр таблицы «Базы» по «не найдена в кластере») несёт `ROADMAP.md`
«Backlog / deferred».

---

## Открытые опции (deferred — не активные задачи)

Объём = новая работа; брать по появлению **триггера**, не по умолчанию. Триаж 2026-06-10:
снятые опции и обоснование — в архиве («Триаж отложенных опций»).

- `MLC-025` — **OpenAPI-codegen / расширение Zod-границ** (поглощает `MLC-006(a)`). Сейчас типы
  рукописные, Zod на 3 критичных границах (ADR-10.1, MLC-016). Триггер: рост API-поверхности /
  частые расхождения FE↔BE. Даёт инфраструктуру для `MLC-026`. Объём M.
- `MLC-026` — **генерация FE-полей настроек из `SettingDefinitions`**. Сейчас новый ключ касается
  enum + каталога + захардкоженного FE-рендера (`SettingsPage.tsx` `SECTIONS`/`FIELD_META`) + i18n
  (подтверждено на MLC-024). Зависит от `MLC-025`. Триггер: каталог > ~25–30 ключей (сейчас 17).
  Объём S.
- `MLC-027` — **разбить `i18n/ru.json` по фичам** (namespaces i18next). Сейчас один плоский файл,
  RU-only (locked). Триггер: файл > ~1000 строк или появление 2-го локаля. Объём S.
- `MLC-073` — **UX-полировка отображения раздела «Быстродействие»** (P3, из находок диагностики `MLC-072`;
  корректности **не** касается — числа верны, подтверждено сверкой с эталонами). Три пункта: (1) **выровнять
  базу времени** host-гейджа CPU (сейчас мгновенный WMI) и атрибуции по семьям (дельта-среднее за 5с) —
  главный источник визуального «не сходится» (host 38% / семьи 16%); вариант — сгладить host коротким
  скользящим средним или явно подписать гейдж «мгновенно»; (2) добавить `SOS_WORK_DISPATCHER` в benign-wait
  список (сейчас ложно лидирует в топ-ожиданиях SQL как фоновое idle планировщика); (3) подпись/тултип, что
  процессный CPU усреднён за интервал поллинга (снимает алиасинг vs мгновенный ДЗ). Триггер: недоверие к
  цифрам реально мешает в работе. Относится к Фазе 5 / UI-холистик-треку. Объём S.
  Решение куратора 2026-06-10: в UX-трек single-host **не** входит.
- `MLC-062` — **движок таблиц `@tanstack/react-table`** вместо рукописных таблиц (единые
  сортировка/пагинация/выбор строк). Триггер: будущий UI-холистик-трек. Решение куратора
  2026-06-10: в UX-трек single-host **не** входит (умножает риск UX-A). Объём M; полная
  постановка — в архиве.

---

## Закрыто

`MLC-001..129` закрыты (кроме отложенных `MLC-025/026/027`, `062`, `073` — см. «Открытые
опции», и активной `MLC-130` — см. «NEXT TASK»; `MLC-080` закрыта в составе `MLC-081`, `MLC-079` —
в составе `MLC-084`; `MLC-011(a)`, `MLC-028`, `MLC-006(a)` сняты с реестра триажем 2026-06-10 —
их несут ADR-20 / DECISIONS «Deployment topology» + ROADMAP / запись `MLC-025`; `MLC-036` —
ROADMAP «RAS Strategy B»). Индекс закрытых задач — **`docs/PROJECT_BACKLOG_ARCHIVE.md`**
(поиск по `MLC-NNN`); полные отчёты — в git-истории (коммиты `MLC-NNN:`/PR).

**Следующий свободный номер: `MLC-131`.** Куратор инкрементирует при выдаче; выдавать занятые
номера нельзя (прецедент коллизии `MLC-053`/`054`).

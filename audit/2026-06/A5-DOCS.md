# A5 — DOCS: аудит документации против кода

Этап А5 пред-релизного аудита. Дата: **2026-06-12**, репозиторий `F:\dev\MitLicense Center`,
ветка `main`, HEAD `e6b1317` (тот же, что в A0). Режим: read-only, код — первоисточник факта
(но не обязательно эталон замысла: ADR фиксирует решение, и код может его нарушать — класс «б»).

**Метод.** Работа выполнена 12 субагентами: 8 × sonnet (фактическая сверка по документам:
README+00_INDEX; 01+02; 03; 04; 05+06; OPERATIONS; CLAUDE.md; SECURITY+структурная оценка
бэклога/роадмапа), 3 × opus (реестр и реализация всех ADR + теневые решения; противоречия
доков между собой; двусторонняя полнота канона против инвентаря A0), 1 × opus-скептик
(адверсариальная перепроверка всех High-кандидатов, включая проверку поведения ASP.NET Core
Data Protection по официальной документации Microsoft). Входная фактура — `audit/2026-06/A0-BASELINE.md`
(71 эндпоинт, 5 Hangfire-джобов, 19 миграций, 14 FE-фич, версии). Blocker-находок нет;
все четыре High/бывших-High кандидата прошли скептика — подтверждены, две понижены до Medium.

---

## 1. Сводная таблица расхождений

Классификация: **(а)** док устарел/неверен · **(б)** код не соответствует зафиксированному
решению · **(в)** неясно — требуется решение владельца.

### 1.1 High

| ID | Заголовок | Класс | Conf |
|---|---|---|---|
| DOC-01 | Key ring Data Protection НЕ зашифрован at-rest — заявленная «DPAPI-backed / machine-scoped» защита отсутствует | **(б)** | high |

**DOC-01 — подробно (подтверждено скептиком, перепроверено по Microsoft Learn).**
- **Док:** `docs/DECISIONS.md:52` (ADR-8: «DPAPI-backed on Windows … scoped to the service account»), `docs/04_INFRASTRUCTURE.md:92` («DPAPI-backed … machine-scoped»), `SECURITY.md:11` («Секреты в покое — DPAPI»), `docs/03_DOMAIN_MODEL.md:122` («DPAPI-encrypted bytes»), `CLAUDE.md` («DPAPI key ring»), OPERATIONS (модель «key ring + БД = единый бэкап-юнит» опирается на это).
- **Код:** `backend/src/MitLicenseCenter.Infrastructure/DependencyInjection.cs:270–273` — `AddDataProtection().SetApplicationName("MitLicenseCenter").PersistKeysToFileSystem(...)` **без** `.ProtectKeysWithDpapi()`. Grep `ProtectKeysWith|DpapiNG|X509` по `backend/src` = 0; других мест конфигурации Data Protection нет.
- **Факт платформы:** по официальной документации Microsoft (key-storage-providers / key-encryption-at-rest, aspnetcore-10.0) явный `PersistKeysToFileSystem` **дерегистрирует** механизм шифрования ключей at-rest — мастер-ключи лежат в XML открытым текстом, защита только NTFS ACL каталога.
- **Точная формулировка:** секреты-значения в `dbo.Settings` зашифрованы (protector работает) — открытым текстом лежит **сам key ring**. Утечка каталога `keys\` + дампа БД (например, через бэкап) раскрывает все секреты без какой-либо привязки к машине/аккаунту — именно ту привязку и обещает ADR-8. Заявленное оператору security-свойство не существует.
- **Класс (б):** похоже на тихий пропуск `.ProtectKeysWithDpapi()`, а не осознанный отход — ни ADR-апдейта, ни комментария в коде. Решение владельца: чинить код (добавить вызов, Windows-only; учесть миграцию существующих незашифрованных ключей на стендах) или править ADR-8/SECURITY/04/CLAUDE на честное «защита NTFS ACL».

### 1.2 Medium

| ID | Заголовок | Док | Код/факт | Класс | Conf |
|---|---|---|---|---|---|
| DOC-02 | 04 §3 неверно перечисляет секреты: «MSSQL connection strings, RAS credentials» якобы шифруются в `dbo.Settings` — фактически единственный секрет `OneC.Cluster.AdminPassword`; conn string живёт в `appsettings*.json` и через Settings не проходит | `docs/04_INFRASTRUCTURE.md` §3 | `SettingDefinitions.cs` (только `OneCClusterAdminPassword` IsSecret:true) | (а) | high |
| DOC-03 | 02 описывает отменённую механику: «parses and safely updates `default.vrd` (XML) without overwriting custom nodes» — surgical-VRD-патч отозван (ADR-4.1 REVOKED, MLC-045); код правит только сегмент пути `wsisapi.dll` (web.config primary / vrd fallback), полную публикацию делает `webinst` с перезаписью целиком. 01/04/ADR-4 описывают корректно — рассинхрон локализован в 02. Подтверждено скептиком | `docs/02_ARCHITECTURE_REQUIREMENTS.md:27` | `WsisapiVersionRewriter.cs:13–36`, `OneCWebinstPublisher.cs:14`, ADR-4 (`DECISIONS.md:21`) | (а) | high |
| DOC-04 | ADR-3.3 утверждает, что kill сессии шлёт `--error-message="<reason>"` («shown to the kicked user») — код флаг не эмитит, 04 §1 прямо говорит «no `--error-message` flag is emitted». Внутреннее противоречие канона; прав 04. Подтверждено скептиком (grep `error-message` по backend/src = 0) | `docs/DECISIONS.md:162` vs `docs/04_INFRASTRUCTURE.md:12` | `RacExecutableRasClusterClient.cs:108–111` | (а) | high |
| DOC-05 | ADR-7: «first admin is seeded **by migration**» — фактически сидит `IdentitySeeder` в startup-пайплайне (runtime, после миграций); ADR-18, 03 §6 и OPERATIONS говорят правильно. Второе внутреннее противоречие DECISIONS.md | `docs/DECISIONS.md` ADR-7 | `IdentitySeeder.cs`, ADR-18 | (а) | high |
| DOC-06 | Таблица замороженных int-слотов в 03 («Enum int-stability», стр. ~111) неполная: отсутствуют целые группы IIS `220–228` (ADR-24) и Backup `510–514` (ADR-27), а также 1–3, 10–12, 20–22, 100–102. Подана как исчерпывающий перечень — риск выбора занятого числа по доку. Скептик понизил с High: enum-файл в коде сам прокомментирован как frozen и реально смотрят туда | `docs/03_DOMAIN_MODEL.md:111` | `Domain/Audit/AuditActionType.cs` (полный список) | (а) | high |
| DOC-07 | 00_INDEX: «каталог **17** настроек» (дважды) — фактически в `SettingDefinitions.All` и таблице 04 §4 их ~24 (пересчёт двух независимых агентов: 24; один упомянул 26 — точное число зафиксировать при правке). Цифра не обновлялась после MLC-064/070/076+ | `docs/00_INDEX.md:17,33` | `Application/Settings/SettingDefinitions.cs` | (а) | high |
| DOC-08 | OPERATIONS, ручной деплой: трекаемый шаблон `appsettings.Production.json` **не содержит ключа `Urls`** — его добавляет только инсталлятор. Оператор по инструкции ручного деплоя получит приложение не на том порту (без предупреждения в доке) | OPERATIONS §Deployment | `appsettings.Production.json` (нет Urls), `installer/MitLicenseCenter.iss:379` | (а) | high |
| DOC-09 | OPERATIONS не содержит процедуры **restore**: предписывает «бэкапить key ring + БД как единый юнит», но нет ни одного шага восстановления (RESTORE DATABASE, перенос key ring, проверка расшифровки секретов). Для отказа диска инструкция обрывается | OPERATIONS §Backup | — (пробел) | (а) | high |
| DOC-10 | Коллизия EventId **1002**: `IdentitySeeder` (Warning, «админ создан с заданным паролем») и `SettingsSeeder` (Information, «засеяно N параметров») пишут под одним EventId в один источник — диагностика по доке (фильтр по 1002) даёт шум/пропуск сигнала | OPERATIONS §Diagnosing | `IdentitySeeder.cs:28`, `SettingsSeeder.cs:17` | (б) | high |
| DOC-11 | Решение по CSRF нигде не зафиксировано: antiforgery в backend отсутствует полностью (grep = 0), фактическая защита — cookie `SameSite=Strict`. Для same-origin SPA это валидный барьер, но выбор «SameSite вместо antiforgery» не записан ни в SECURITY.md, ни в ADR-7 | SECURITY.md, ADR-7 (молчат) | `Program.cs:77–84` | (в) | high |
| DOC-12 | Reconcile/drift-хвосты пережили ADR-4.1 (REVOKED): 06 §12 словарь («Reconcile publication → Согласовать состояние», «Check drift now»), 06 §3/§7/§10 (Drift-семантика, Reconcile как destructive), 03 §6 (роль Admin «…and publication reconcile»), OPERATIONS («Проверить сейчас»). Таких действий в v1 нет; строки в FE отсутствуют (grep = 0) | `docs/06_UI_DESIGN.md` §§3,7,10,12; `03` §6; OPERATIONS | ADR-4.1 REVOKED; frontend/src (0 совпадений) | (а) | high |
| DOC-13 | 05 §3.5 заявляет фильтр аудита по **Initiator** — ни в FE (`AuditFilters`: actionType/tenantId/from/to), ни в API параметра нет. Спека описывает нереализованное; пометки «deferred» нет | `docs/05_UI_REQUIREMENTS.md` §3.5 | `features/audit/AuditFiltersBar.tsx`, `types.ts`; A0 §2.2 | (в) | high |
| DOC-14 | SECURITY.md неполон для security-документа: не описаны параметры cookie (HttpOnly, SameSite=Strict, Secure=Always в prod, 8 ч sliding), защита Hangfire (Admin-фильтр), Swagger **безусловно включён в Development**, `Security:EnforceHttps=false` по умолчанию в Production-шаблоне, механизм `initial-admin.secret` (ADR-31), `Encrypt=False` в дев-конфиге | `SECURITY.md` | `Program.cs:77–84`, `TransportSecurity.cs:22–23`, `appsettings.Production.json:7` | (а) | high |

### 1.3 Low

| ID | Заголовок | Класс | Conf |
|---|---|---|---|
| DOC-15 | 02: рудименты стадии выбора — «Hangfire **or Quartz.NET**» (Quartz отсутствует в зависимостях; тот же док ниже уже ссылается на Hangfire), «React/Vue/Blazor» при зафиксированном React; смешение present-tense и «must/should» | (а) | high |
| DOC-16 | 02: IIS-адаптер описан монолитом — фактически три интерфейса (`IIisPublishingService`, `IIisLifecycleService`, `IIisResetConcurrencyGate`, ADR-24); допустимое исключение ADR-20 (Web → Infrastructure.Persistence/Identity) в 02 не оговорено | (а) | med |
| DOC-17 | 01/02: интервалы «15–30 с» / «20–30 с cold, 3–5 с hot» — фактически конфигурируемые настройки (cold default 25, диапазон 10–300; hot default 4, диапазон 2–60); дефолты входят в заявленные диапазоны, но формат вводит в заблуждение | (а) | high |
| DOC-18 | 00_INDEX: архив указан как «MLC-001..043» (фактически ..108); дата «последней построчной сверки 2026-06-05» устарела на ~65 коммитов; перечень джобов «cold/hot/status-refresh/retention» — hot не Hangfire-джоба (BackgroundService без cron), `backup-retention` пропущен | (а) | high |
| DOC-19 | README: структура репо без `installer/` и `backend/tools/`; `scripts/` описан как «build, dev, db-reset» (фактически 9 скриптов, включая релизные `publish-release`/`build-installer`); пример connection string без `Encrypt=False` (на части конфигураций SQL Server даст ошибку TLS) | (а) | high |
| DOC-20 | CLAUDE.md: в синопсисе `publish-release.ps1` пропущен реальный параметр `-Configuration`; `reset-admin.ps1` не упомянут вовсе (важен при инцидентах); гоча CP866 атрибутирует декодирование «парсеру» — декодирует runner (`SystemProcessRacRunner`), причём динамически системной OEM-страницей, не хардкодом 866; список «build.ps1/db-reset.ps1 глушат ErrorActionPreference» устарел (паттерн во всех скриптах) | (а) | high |
| DOC-21 | Гоча «.ps1 — UTF-8 с BOM» не подкреплена инструментально: `.editorconfig` не задаёт `charset = utf-8-bom` для `*.ps1` (глобальный `[*]` — utf-8 без BOM); фактически все скрипты с BOM, но новый файл может быть сохранён без него | (в) | high |
| DOC-22 | 03: `Tenant.UpdatedAt` не описан; опечатка `AppID` (в коде `AppId`); §7 Setting описывает одно поле `Value (String)` при фактических двух столбцах (`ValueText nvarchar` / `Value varbinary` — контрактная секция ниже точна); сущности телеметрии (`LicenseUsageSnapshot`, `PerfRecording`, `PerfRecordingSample`, `DatabaseBackup`) и их enum'ы (`PerfRecordingStatus/StopReason`, `BackupStatus`, `BackupFailureReason` — все HasConversion<int>) не описаны как сущности | (а) | high |
| DOC-23 | 04: третья регулярная rac-команда `infobase summary list` (`ListInfobasesAsync`, discovery/unassigned) не описана в §1; конкретные таймауты не названы (webinst 60 с; BACKUP/VERIFYONLY 4 ч — в доке «hours-scale»); «OEM/CP866» подано как константа (код берёт системную OEM-страницу); `UnassignedInfobasesCache` назван «the IClusterUuidCache shape» — интерфейс не реализует, живёт в Web; причина raw-SQL в `AuditRetentionJob` (vs `ExecuteDelete` у двух других retention) не объяснена | (а) | high |
| DOC-24 | 04 «Discovery error contract» перечисляет 4 из 6 discovery-эндпоинтов (нет `cluster-infobases`, `rac-paths`; оба покрыты «по месту» в других разделах) | (а) | high |
| DOC-25 | OPERATIONS и ADR-24 используют имя страницы «Публикации» — по 05/06 отдельной страницы нет, всё на странице «Базы» | (а) | high |
| DOC-26 | OPERATIONS, прочие пробелы: нет команд управления службой при ручном деплое (`sc stop MitLicenseCenter`); нет команды чтения Event Log; `Backup.MaxParallel` не упомянут в разделе бэкапов; нет сценария «обновили платформу 1С на хосте»; dev-путь key ring (`%LOCALAPPDATA%`) не назван; бэкап SQL-баз самих инфобаз не упомянут как ответственность оператора | (а) | med-high |
| DOC-27 | 06/05 устаревания: «Eight items» в меню (фактически 9 Admin / 7 Viewer — само перечисление в доке верное); recharts заявлен «только /reports» (используется и в performance); таблица Sessions — 6 колонок в доке vs 8–9 в коде (добавлены user, startedAt) | (а) | high |
| DOC-28 | Не реализованные UI-требования канона: 06 §9 — для Viewer на запретном URL требуется «polite page», код делает молчаливый redirect на `/`; 06 §8 — баннер «Данные усталели» при stale>60 c не реализован (строки нет в ru.json); 05 §3.5 — колонка Reason в таблице аудита не рендерится (данные в типе есть) | (б) | high |
| DOC-29 | ADR-14: исходный перечень `scripts/` в теле ADR не вычищен (дополнен changelog-«Update (MLC-099/100)»-абзацами вместо present-tense правки); такие же Update-хвосты в ADR-10.1, ADR-17, ADR-29 — против конвенции канона | (а) | med |
| DOC-30 | SECURITY.md относит `AllowedHosts` к семейству «ключи `Security:*`» — фактически это стандартный корневой ключ ASP.NET Core, отдельный механизм | (а) | high |
| DOC-31 | Терминологические неясности: 03:111 относит слоты 103–107 (user-management) к «100–102 login-session group»; 06 §3 «Missing» (info/sky) против «Не найдена в кластере» (danger/red) — похожие имена разных состояний без разведения | (в) | low |
| DOC-32 | Устаревший комментарий в коде: `SystemProcessRacRunner.cs:11–12` («rac.exe пишет UTF-8 без BOM, верифицировано в ADR-3.3») противоречит строкам 16–23 того же файла (OEM-декодирование); поведение кода корректно | (б, комментарий) | high |

---

## 2. Реализация ADR и теневые решения

**Реестр ADR:** в DECISIONS.md 31 нумерованный ADR (№ 9 — revoked-пропуск) + суб-ADR 3.3/4.1/6.1/6.2/10.1 + Locked Constraints + раздел Revoked. **Все активные ADR реализованы в коде**, кроме одного: **ADR-8 реализован частично** (DOC-01 — DPAPI-часть отсутствует). Revoke-дисциплина соблюдена (4.1, 9, 3.1, 3.2 помечены явно); противоречий между ADR, кроме DOC-04/DOC-05 (устаревшие формулировки внутри 3.3 и 7), не найдено.

**Теневые решения** — архитектурно значимые решения в коде без ADR (все — класс (в), владельцу решить, фиксировать ли):

1. **Lockout-политика логина: 5 попыток / 15 минут** (`DependencyInjection.cs:86–87`) — anti-bruteforce-порог, упомянут в SECURITY.md, но решением не оформлен.
2. **«Отключение пользователя» = Identity-lockout до `DateTimeOffset.MaxValue`** (`UsersEndpoints.cs:204,227,328`) — не отдельный флаг IsActive; влияет на семантику «активности».
3. **Force-password-change + LastLoginAt** (`AppUser.cs:10,14`, flow в AuthEndpoints, MLC-059) — расширение жизненного цикла учётки и контракта `/auth/login`; ADR-7 описывает только seed.
4. **Wire-контракт JSON: camelCase + `WhenWritingNull`** (`Program.cs:31–32`) — null-поля опускаются на проводе; следствия для FE известны (урок MLC-067/071), но самого решения нет (ADR-10.1 лишь упоминает).
5. **Hangfire job-retention 2 дня + `RemoveIfExists("drift-check")`** (`JobRetentionStateFilter.cs`, Program.cs) — операционное решение без фиксации.
6. **Паттерн in-process singleton-гейтов конкурентности** (`EnforcementGate`, `WebinstConcurrencyGate`, `IisResetConcurrencyGate`, `ClusterUuidCache`) — описан фрагментарно по нескольким ADR, единого решения нет («полу-теневое»).

---

## 3. Что сошлось (сжато)

Канон в основной массе **подтверждён кодом**: полная карта покрытия инвентаря A0 — все 71 эндпоинт, все 5 джобов (cron-расписания совпадают точно), все 14 FE-фич имеют спеку в 04/05; «тяжёлые» фичи (performance recordings, backups, discovery, unassigned/reverse-drift, полный IIS-lifecycle, SPA-fallback, health) заспечены полно. Вымышленной функциональности уровня «несуществующий экран» нет — худшие находки этого класса всего лишь словарные хвосты reconcile (DOC-12). Подтверждены: parity-валидация BE↔FE (включая regex PlatformVersion), все задокументированные frozen-int значения enum, 409-коды, FK-поведения и индексы, single-host (ADR-28), слои и anti-corruption граница (NetArchTest; rac.exe/ServerManager в Web — 0), роли и разбивка авторизации (3 anon / 3 auth / 23 Viewer / 42 Admin), lockout 5/15 и пароль 12+, Hangfire-фильтр Admin, схема `hangfire`, purpose `mlc.settings.v1` и пути key ring, бюджет spawn rac.exe, бэкап-цикл (COPY_ONLY/VERIFYONLY/keep-latest), 24-строчная таблица настроек в 04 §4 (сама таблица точна — устарел только счётчик в 00_INDEX), все скрипты CLAUDE.md существуют с заявленными параметрами, миграции UTF-8 без BOM + LF, pre-commit рабочий. Заявленные-но-не-построенные UI-вещи честно помечены в ROADMAP. Структурно PROJECT_BACKLOG (тонкий индекс), ARCHIVE (read-only архив) и ROADMAP роли выполняют, смешения нет.

---

## 4. Топ-5 самых опасных расхождений

1. **DOC-01 (High, б) — key ring не зашифрован вопреки ADR-8/SECURITY.** Единственная находка, где канон обещает security-свойство, которого нет. Усугубляется DOC-02 (раздел «секреты» в 04 §3 ещё и неверно перечисляет, что защищено): владелец, читая канон, получает ложное чувство защищённости бэкапов. Требует решения: чинить код или честно править канон.
2. **DOC-08 (Medium, а) — ручной деплой по OPERATIONS приводит к нерабочему порту.** Единственное место, где следование инструкции даёт неработающую систему (шаблон Production.json без `Urls`). GUI-инсталлятор не затронут.
3. **DOC-09 (Medium, а) — нет процедуры restore.** Бэкап-доктрина без восстановления: в ДР-сценарии оператор останется с `.bak` и каталогом keys без инструкции. В паре с DOC-01 риск компаундируется.
4. **DOC-11 (Medium, в) — решение по CSRF нигде не зафиксировано.** Фактическая защита (SameSite=Strict) валидна, но как незаписанное решение она хрупка: будущая правка cookie-политики или появление не-same-origin клиента молча снимет барьер.
5. **DOC-06 (Medium, а) — frozen-slots таблица enum в 03 неполная.** Прямой путь к коллизии «замороженных» int при добавлении нового AuditActionType по доку; смягчается комментариями в самом enum-файле.

---

## 5. Вердикт доверия канону

**Надёжен частично.** Ядро канона — доменная модель (03, кроме перечисленных пробелов), UI-спека (05), инфраструктурная спека (04, кроме §3 «Секреты»), OPERATIONS (кроме ручного деплоя и отсутствия restore) и карта покрытия фич — соответствует коду и пригодно как источник правды. Доверие подрывают точечно:

- **`docs/02_ARCHITECTURE_REQUIREMENTS.md` — самый устаревший документ канона** (DOC-03, DOC-15, DOC-16, DOC-17): несёт отменённые механики и рудименты стадии выбора; требует ревизии целиком.
- **Security-слой канона (ADR-8 + 04 §3 + SECURITY.md)** — содержит фактически неверное утверждение о DPAPI (DOC-01/DOC-02) и не фиксирует CSRF-решение (DOC-11); SECURITY.md как документ неполон (DOC-14).
- **DECISIONS.md** — решения реализованы, но три ADR содержат устаревшие формулировки (3.3 — DOC-04, 7 — DOC-05, 14 — DOC-29) и changelog-хвосты против собственной конвенции.
- **00_INDEX.md** — числовые утверждения (настройки, диапазон архива, перечень джобов, дата сверки) систематически отстают (DOC-07, DOC-18); как навигатор работоспособен.
- **06_UI_DESIGN.md** — reconcile-хвосты (DOC-12) и три нереализованных требования (DOC-28), не помеченные как deferred.

По докам систему **можно** развернуть (через GUI-инсталлятор — полностью; вручную — с ямой DOC-08), понять и сопровождать; **нельзя** — восстановить после сбоя (DOC-09) и корректно судить о защите секретов at-rest (DOC-01).

## 6. Что не успел покрыть / ограничения

- **PROJECT_BACKLOG_ARCHIVE.md (677 КБ)** — только структурная оценка (шапка, заголовки, 3 выборочные записи); построчная вычитка не выполнялась (вне объёма по постановке). Замечена мелкая рассинхронизация: заглавный индекс архива заканчивается на MLC-092, тело содержит записи до MLC-107.
- **Статическая проверка only:** ни одна операционная команда из OPERATIONS не исполнялась (бэкап/restore/инсталлятор/iisreset); корректность проверена по содержимому скриптов/кода. Поведение Data Protection (DOC-01) подтверждено документацией Microsoft, но фактические key-XML на работающем стенде не инспектировались — финальная верификация DOC-01 на стенде заняла бы минуты (посмотреть, есть ли `<encryptedSecret>` в файлах `%ProgramData%\MitLicenseCenter\keys\*.xml`).
- **DECISIONS.md** вычитан целиком ADR-агентом и cross-doc-агентом, но агент полноты читал его точечно (grep по якорям) — теоретически возможны пропуски «док-без-кода» внутри длинных ADR.
- **Мелкое неразрешённое:** точное число настроек в `SettingDefinitions.All` — два агента насчитали 24, один упомянул 26; на вывод DOC-07 («17» неверно) не влияет, пересчитать при правке.
- FE-поведение проверялось по коду, не в браузере (вне объёма А5; UI-этапы волны покрывают отдельно).
- Скан истории git на секреты и проход мастера инсталлятора — вне объёма А5 (частично покрыты A0 и этапами безопасности).

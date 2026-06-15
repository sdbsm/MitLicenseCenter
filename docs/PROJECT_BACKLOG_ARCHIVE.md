# MitLicense Center — Backlog Archive (закрытые задачи)

> **Это архив, read-only, справочный.** Активный реестр задач — `docs/PROJECT_BACKLOG.md`
> (его читают первым при каждом запуске работы). Здесь — **компактный индекс всех закрытых
> задач** `MLC-001..144`: одна строка на задачу (суть + дата + якорь в историю git).
> Развёрнутые постановки и отчёты здесь **не дублируются** — их полностью несёт git
> (см. «Как достать детали» ниже). Для выбора следующей работы этот файл читать не нужно —
> сюда заглядывают только за историей конкретной закрытой задачи по `MLC-NNN`.
>
> Снимок сжат 2026-06-13: до этой даты файл хранил полные прозаические отчёты по каждой
> задаче (~5000 строк). Они никуда не делись — лежат в истории этого же файла (см. ниже),
> а первоисточник всегда был в коммитах и PR. Канон проекта
> (`docs/01..06 + DECISIONS.md + ROADMAP.md + OPERATIONS.md`) — источник правды по
> архитектуре v1; бэклог его не дублирует.

## Как достать полную детализацию закрытой задачи

Индекс ниже — точка входа (номер + суть + дата). Глубину даёт git:

- **Реализация (код):** `git log --grep "MLC-NNN"` — все коммиты задачи (конвенция
  `MLC-NNN: <что>` гарантирует находимость). Якорь в конце строки индекса — основной
  код-коммит задачи: `git show <hash>` покажет полный дифф. Где якоря нет (ранние
  `MLC-001/002/005/006/011` и сводные коммиты) — ищется тем же `--grep`.
- **Довод / обсуждение / ревью:** связанный PR —
  `gh pr list --state merged --search "MLC-NNN"` или `gh pr view <#>`.
- **Развёрнутый отчёт «как было до сжатия»:** полная прозаическая версия любого отчёта —
  в истории этого файла. `git log -p -- docs/PROJECT_BACKLOG_ARCHIVE.md` (вся история);
  `git show <commit>:docs/PROJECT_BACKLOG_ARCHIVE.md` — файл целиком на любую дату
  (последняя несжатая версия — родитель коммита сжатия от 2026-06-13).

## Индекс закрытых задач (MLC-001..144)

Формат: `MLC-NNN` — суть — Done (дата) · `код-коммит`.

### Аудит безопасности и дефектов — MLC-001..019 (2026-06-01..03)

- `MLC-001` — Защита от параллельного цикла согласования (over-kill) — Done (2026-06-01)
- `MLC-002` — Ручной kill: аудит только при реальном завершении + сверка дескриптора — Done (2026-06-01)
- `MLC-003` — Fail-fast старт: миграции/сидинг синхронно до приёма трафика (ADR-18) — Done (2026-06-01) · `f6d9a94`
- `MLC-004` — Глобальный ProblemDetails + backstop гонок уникальности → 409 (ADR-19) — Done (2026-06-02) · `6dbcf4e`
- `MLC-005` — [Doc divergence] ADR-14: ручной деплой без несуществующего скрипта — Done (2026-06-02)
- `MLC-006` — [Doc divergence] Рукописные TS-типы зафиксированы как осознанный выбор (ADR-10.1) — Done (2026-06-02)
- `MLC-007` — Frontend-тесты: ProtectedRoute, CRUD-мутации, маппинг 409 — Done (2026-06-02) · `a38d183`
- `MLC-008` — Контрактные тесты persistence-инвариантов на SQLite — Done (2026-06-02) · `1f27323`
- `MLC-009` — Санитизация инфраструктурных исключений в discovery/reconcile — Done (2026-06-02) · `839bc30`
- `MLC-010` — SettingsSnapshot: bulk-загрузка вне лока + single-flight — Done (2026-06-02) · `2216639`
- `MLC-011` — Vertical-slice data access зафиксирован как осознанный выбор (ADR-20) — Done (2026-06-02)
- `MLC-012` — Прод-хардненинг транспорта за конфиг-флагами (ADR-22) — Done (2026-06-02) · `ac4b217`
- `MLC-013` — Принятый риск: пароль кластера в cmdline rac.exe (ADR-21) — Done (2026-06-02) · `6ed0a30`
- `MLC-014` — FE: единый ConflictBody + readConflictBody — Done (2026-06-02) · `a6114d5`
- `MLC-015` — FE: серверная пагинация списков + точечная проверка занятости кластер-базы — Done (2026-06-02) · `78cd61d`
- `MLC-016` — FE: точечная Zod-валидация на 3 критичных границах — Done (2026-06-03) · `a32f72e`
- `MLC-017` — FE: i18n-чистый 401-редирект через router — Done (2026-06-02) · `a6114d5`
- `MLC-018` — FE: code-splitting маршрутов (React.lazy) + разбиение вендоров — Done (2026-06-03) · `ccfbcb0`
- `MLC-019` — Dev tooling: build.ps1 устойчив к stderr нативных шагов + шаг pnpm test — Done (2026-06-03) · `4cfee2b`

### Техдолг и поддерживаемость — MLC-020..024 (2026-06-03)

- `MLC-020` — Дедуп расчёта потребления лицензий → доменный калькулятор `LicenseConsumption` — Done (2026-06-03) · `a8a5981`
- `MLC-021` — Web-хелперы: uniqueness-backstop + каталог описаний аудита + резолв initiator — Done (2026-06-03) · `b88851a`
- `MLC-022` — Единый источник правил валидации Infobase/Publication (BE `InfobaseValidationRules` + FE `validation.ts` + parity-тесты) — Done (2026-06-03) · `fd99850`
- `MLC-023` — FE: декомпозиция InfobaseFormDialog (`useInfobaseForm` + `PublicationFieldset` + `mapConflictToField`) — Done (2026-06-03) · `487f0e9`
- `MLC-024` — App-id whitelist лицензий → `dbo.Settings` (`OneC.LicenseConsumingAppIds` + UI-поле) — Done (2026-06-03) · `94ef122`

### Рефакторинг-трек Phase 1–2 (REF-01..07) — MLC-029..035 (2026-06-03..04)

- `MLC-029` (REF-01) — Дедуп маппинга `Publication` request→entity (`ApplyPublicationFields`) — Done (2026-06-03) · `f58bc84`
- `MLC-030` (REF-02) — Архитектурные guard-тесты границ слоёв (NetArchTest) — Done (2026-06-03) · `8242259`
- `MLC-031` (REF-03) — Фабрика CRUD-mutation хуков `useInvalidatingMutation` — Done (2026-06-03) · `85f12e1`
- `MLC-032` (REF-04) — Декомпозиция FE-страниц Audit/Publications/Sessions — Done (2026-06-04) · `899e71f`
- `MLC-033` (REF-05) — Обобщённый conflict→descriptor маппер + `toastFormSubmitError` — Done (2026-06-04) · `4d36324`
- `MLC-034` (REF-06) — Web-аудит-фасад `HttpContext.AuditAsync` — Done (2026-06-04) · `9cca7af`
- `MLC-035` (REF-07) — Группировка `Web/Endpoints` по подпапкам-фичам — Done (2026-06-04) · `8422fab`

### Перф-трек Phase 1–2 (PERF-01..07) — MLC-037..043 (2026-06-04..05)

- `MLC-037` (PERF-01) — Метрики горячего пути: спавны `rac.exe` + циклы (Meter) — Done (2026-06-04) · `c80b338`
- `MLC-038` (PERF-02) — Опт-ин профиль EF-запросов + baseline — Done (2026-06-05) · `e8a86b6`
- `MLC-039` (PERF-03) — Нагрузочный seed-харнесс (PerfHarness) — Done (2026-06-04) · `398abba`
- `MLC-040` (PERF-04) — Readiness-проба `/api/v1/health/ready` — Done (2026-06-05) · `48858d1`
- `MLC-041` (PERF-05) — Кросс-вызовный кэш UUID кластера — Done (2026-06-05) · `9671837`
- `MLC-042` (PERF-06) — Составной индекс `AuditLogs` под фильтр+сортировку — Done (2026-06-05) · `8330c89`
- `MLC-043` (PERF-07) — Батч-загрузка публикаций в `DriftCheckJob` (N+1→1) — Done (2026-06-05) · `5908ac7`

### Вне треков — функциональность v1 (2026-06-05..09)

- `MLC-044` — Hot-тир enforce'ит (near-realtime kill ≤5с) + быстрый экран — Done (2026-06-05) · `523d07d`
- `MLC-045` — Публикации через webinst + смена платформы правкой `web.config` (ADR-4) — Done (2026-06-05) · `38b397d`
- `MLC-046` — Публикации: массовые операции (bulk publish / bulk change-platform) — Done (2026-06-05) · `31da190`
- `MLC-047` — Управление жизненным циклом IIS из панели (ADR-24) — Done (2026-06-06) · `8c3f3f4`
- `MLC-053` — dev/ops-утилита сброса пароля администратора `reset-admin` — Done (2026-06-07) · `34afc2a`
- `MLC-072` — Диагностика метрик «Быстродействия»: числа корректны, баг не подтверждён — Done (2026-06-09)
- `MLC-074` — Retention-джобы под execution strategy (retriable-батчи) — Done (2026-06-09) · `878f76a`

### Трек «Отчёты — использование лицензий» (ADR-25) — MLC-048..050, 054 (2026-06-06..07)

- `MLC-048` — Сбор time-series использования лицензий (ADR-25) — Done (2026-06-06) · `97eb67d`
- `MLC-049` — Reports API: сводка + drill-down — Done (2026-06-06) · `661e655`
- `MLC-050` — FE-раздел «Отчёты» `/reports` (первый график recharts) — Done (2026-06-06) · `0e58dbd`
- `MLC-054` — Полировка `/reports`: плашка обрезки + помесячный выбор — Done (2026-06-07) · `b9d7e4c`

### Трек «Экспорт отчётов» — MLC-051..052 (2026-06-07)

- `MLC-051` — Экспорт отчётов: каркас + CSV + XLSX — Done (2026-06-07) · `4c3ac42`
- `MLC-052` — Экспорт отчётов: HTML (интерактивный) + PDF — Done (2026-06-07) · `af25cf3`

### Трек «Полировка /settings» — MLC-055..056 (2026-06-07..08)

- `MLC-055` — Переработка `/settings`: секции, retention, порт RAS, пикер платформы — Done (2026-06-07) · `a607287`
- `MLC-056` — SQL-instance discovery (localhost) + пикер сервера БД — Done (2026-06-08) · `9d59419`

### Трек «Полировка панели v1.1» + мини-трек «Пользователи» — MLC-057..061 (2026-06-08)

- `MLC-057` — Переключатель темы (light/dark/system) — Done (2026-06-08) · `656efff`
- `MLC-058` — Раздел «Администраторы»: API `/admins` + UI — Done (2026-06-08) · `7cb6b70`
- `MLC-059` — Форс-смена пароля при первом входе + последний вход — Done (2026-06-08) · `36a415d`
- `MLC-060` — Переименование «Администраторы»→«Пользователи» (роуты/API/слоты аудита) — Done (2026-06-08) · `5f87e3d`
- `MLC-061` — Смена роли учётки Admin↔Viewer (+слот аудита 107) — Done (2026-06-08) · `ca9ea6c`

### Трек «Анализ быстродействия 1С» (ADR-26) — MLC-063..071 (2026-06-08..09)

- `MLC-063` — Разведка перф-трека: rac perf-поля, DMV, атрибуция — Done (2026-06-08) · `6ffd2c2`
- `MLC-064` — Host-проба `IHostMetricsProbe` на WMI + `/performance/host` (ADR-26) — Done (2026-06-08) · `eead8e5`
- `MLC-064a` — Честный сигнал недоступных процессов host-пробы + FE-баннер — Done (2026-06-08) · `628c8cb`
- `MLC-065` — FE-каркас `/performance`: гейджи сатурации + атрибуция по семьям — Done (2026-06-08) · `c61bf5c`
- `MLC-066` — 1С-сеансы: `ListSessionLoadsAsync`/`ListProcessesAsync` + `/performance/onec-sessions` — Done (2026-06-08) · `6ffd2c2`
- `MLC-067` — FE-секция «кто грузит внутри 1С» — Done (2026-06-08) · `34d38ce`
- `MLC-068` — SQL DMV-проба `ISqlPerformanceProbe` + `/performance/sql` — Done (2026-06-09) · `ddd85a7`
- `MLC-069` — FE-секция «1С грузит SQL?» — Done (2026-06-09) · `658d016`
- `MLC-070` — Запись по требованию: backend (`PerfRecording*` + API) — Done (2026-06-09) · `f34ec13`
- `MLC-071` — Запись по требованию: UI (график + топ-виновники + экспорт) — Done (2026-06-09) · `bec82f0`

### Трек «Резервное копирование баз SQL» (ADR-15 изменён, ADR-27) — MLC-075..078 (2026-06-09..10)

- `MLC-075` — ADR-15 пересмотрен + ADR-27 «Бэкапы SQL» — Done (2026-06-09)
- `MLC-076` — Бэкапы: backend-фундамент (`ISqlBackupService`, COPY_ONLY, keep-latest) — Done (2026-06-09) · `06b5da0`
- `MLC-077` — Бэкапы: оркестрация (очередь + потолок + замок) + `/api/v1/backups` — Done (2026-06-10) · `e508df1`
- `MLC-078` — Бэкапы: frontend + live e2e — трек завершён 4/4 — Done (2026-06-10) · `554fef5`

### Трек «UX-пересборка панели под single-host», этап 1 — MLC-079..086 (2026-06-10)

- `MLC-079` — Аудит-i18n: union `User*`-действий рендерится по-русски (влита в MLC-084) — Done (2026-06-10)
- `MLC-080` — Запас тайминг-чувствительных ожиданий в FE-тестах (влита в MLC-081) — Done (2026-06-10) · `74cd715`
- `MLC-081` (UX-A) — «Базы» = Инфобазы + Публикации (вкладка «IIS», `/publications` удалена) — Done (2026-06-10) · `9f034ea`
- `MLC-082` (UX-B) — Форма инфобазы без выбора сервера; колонка «Сервер БД» снята — Done (2026-06-10) · `a13588e`
- `MLC-083` (UX-C) — `/settings`: секция «SQL Server», «Значения по умолчанию» расформирована — Done (2026-06-10) · `9b6a465`
- `MLC-084` (UX-D) — Профиль в топбар, сайдбар 8 пунктов (+влита `MLC-079`) — Done (2026-06-10) · `9b114e5`
- `MLC-085` (UX-E) — Дашборд-«Обзор»: кликабельные KPI + строка здоровья хоста — Done (2026-06-10) · `3a9be6e`
- `MLC-086` (UX-F) — Финальный док-PR: канон 05/06 переписан под новый UI — Done (2026-06-10) · `5377519`

### Трек «UX-пересборка, этап 2: single-host бек-чистка» (ADR-28) — MLC-087..091 (2026-06-10)

- `MLC-087` (ST-A) — SQL-инстанс: настройка `Sql.Server` — единственный источник — Done (2026-06-10) · `0ea2da0`
- `MLC-088` (ST-B) — Колонка `Infobase.DatabaseServer` удалена (миграция) — Done (2026-06-10) · `254e35e`
- `MLC-089` (ST-C) — Ключ `OneC.Cluster.Server` удалён — Done (2026-06-10) · `e0d7f7b`
- `MLC-090` (ST-D) — Фильтр статуса публикации на «Базах» — Done (2026-06-10) · `e0d7f7b`
- `MLC-091` (ST-E) — Финальный док-PR: ADR-28 «Single-host topology» + канон 01/03/04 — Done (2026-06-10) · `2440875`

### Трек «Нераспределённые базы: discovery-first» (ADR-29) — MLC-092..094 (2026-06-11)

- `MLC-092` (UB-A) — Endpoint нераспределённых баз + игнор-лист (backend) — Done (2026-06-11) · `8d2ecc3`
- `MLC-093` (UB-B) — Баннер + диалог разбора + discovery-first добавление (frontend) — Done (2026-06-11) · `0525ecc`
- `MLC-094` (UB-C) — Канон + ADR-29 (docs) — Done (2026-06-11) · `7bcf675`

### Трек «Обратный дрейф панель↔кластер» — MLC-095..097 (2026-06-11)

- `MLC-095` (RD-A) — `MissingItems` в ответе `/infobases/unassigned` (backend) — Done (2026-06-11, PR #91) · `cdc07ad`
- `MLC-096` (RD-B) — Баннер + метка `danger` + диалог обратного дрейфа (frontend) — Done (2026-06-11) · `1d75fb0`
- `MLC-097` (RD-C) — Канон обратного дрейфа: ADR-29 update-нота + 04/05/06 (docs) — Done (2026-06-11, PR #92) · `3555c83`

### Трек «GUI-установщик (Inno Setup)» (ADR-30, ADR-31) — MLC-098..107 (2026-06-11)

- `MLC-098` — Бэкенд сам отдаёт SPA same-origin (ADR-30; IIS не нужен для хостинга панели) — Done (2026-06-11) · `c5389b7`
- `MLC-099` — Self-contained single-file publish + `publish-release.ps1` — Done (2026-06-11) · `e762b5c`
- `MLC-100` — Inno Setup каркас установщика (служба LocalSystem + firewall + обновление поверх) — Done (2026-06-11) · `a30aafa`
- `MLC-101` — Интерактивный мастер установщика: учётные данные SQL + сеть — Done (2026-06-11) · `3557c63`
- `MLC-102` — Пароль admin задаёт оператор в мастере установщика — Done (2026-06-11) · `e376e18`
- `MLC-103` — Деинсталл keep-data prompt + ярлык «Пуск» + лог установки — Done (2026-06-11) · `d34f91e`
- `MLC-104` — Backend service-aware via `UseWindowsService` — Done (2026-06-11) · `5cbb483`
- `MLC-105` — Установщик распознаёт уже-инициализированную БД (ADR-31) — Done (2026-06-11) · `b29b858`
- `MLC-106` — Bootstrap создаёт БД на пустом инстансе (ADR-18) — Done (2026-06-11) · `ade924d`
- `MLC-107` — Надёжное создание службы (rc-проверка + 1057) + чистый конфиг — Done (2026-06-11) · `6167038`

### Трек «Пред-релизные фиксы по итогам аудита» — MLC-108..115 (2026-06-12..13)

- `MLC-108` — Детерминированный состав релизного артефакта (REL-01) — Done (2026-06-12, PR #119) · `fd4bde9`
- `MLC-109` — Немедленный отзыв доступа при disable/reset/смене роли (SEC-01) — Done (2026-06-12, PR #121) · `fef1c57`
- `MLC-110` — Защита секретов на диске: NTFS ACL + честный ADR-8 (KEYRING-01в + SEC-04 + SEC-02) — Done (2026-06-13, PR #123) · `69c54f1`
- `MLC-111` — Форма привязки базы: явный выбор клиента (убран предвыбор `tenants[0]`) (UX-42) — Done (2026-06-13, PR #127) · `7096478`
- `MLC-112` — Страховка апгрейда: экран бэкапа до замены файлов + провал старта службы = ошибка мастера (REL-02) — Done (2026-06-13, PR #129) · `0577f16`
- `MLC-113` — Снятие IIS-публикации (webinst -delete за ADR-20; эндпоинт + UI + аудит `PublicationUnpublished=23`) (UX-43) — Done (2026-06-13, PR #131) · `aa9c1a8`
- `MLC-114` — Пакет валидации/наблюдаемости (BE-03/05/10 + UX-01-остаток; аудит `LoginFailed=108`) — Done (2026-06-13, PR #133) · `269654b`
- `MLC-115` — Пакет поставки/тестов: `THIRD_PARTY_LICENSES.txt` в артефакт + тесты profile/ForcePasswordChange (REL-10 + FE-01) — Done (2026-06-13, PR #134) · `571776b`

### Вне трека (follow-up ВМ-проверки к установщику) — MLC-116..117 (2026-06-13)

- `MLC-116` — Апгрейд: служба реально останавливается (верный control-код `SERVICE_CONTROL_STOP`; 1056=успех) — Done (2026-06-13, PR #138) · `8654e10`
- `MLC-117` — Публикация видит `OneC.RAS.Endpoint` сразу после установки (сидовый дефолт `localhost:1545` + heal) — Done (2026-06-13, PR #140) · `193d105`

### Пострелизный рефакторинг R1–R12 — MLC-118..139 (2026-06-13..14)

Закрытие остаточного техдолга аудита по итерациям ROADMAP «Backlog / deferred» (R-N = одна
задача = один PR); первоисточник находок — `audit/2026-06/TECH-DEBT.md`. Передаточные записки
(гочи) и полные отчёты — в git/PR (`git log --grep "MLC-NNN:"`); статусы итераций — `ROADMAP.md`.

- `MLC-118` — R2: валидационный барьер BE+FE (длины/метасимволы, parity golden; 03 §3.5) — Done (2026-06-13) · `38f2baa`
- `MLC-119` — R3: аудит-целостность (enlist-примитив, `LimitChanged=201`, KillEnforcer-батч) — Done (2026-06-13) · `0b2ae68`
- `MLC-120` — R4: тестовые слепые зоны (BE-09/12/14/24, FE-11/19 + dashboard Zod-граница) — Done (2026-06-13) · `8056e99`
- `MLC-121` — R5: офлайн-UX (`ApiNetworkError`/`classifyError`/`applyFieldErrors`, RAS-подсказка) — Done (2026-06-13) · `9f0c31b`
- `MLC-122` — R6: видимость лимитов (`lib/quota.ts`; FE-03 инвалидация reports; UX-46) — Done (2026-06-14) · `ed8d19c`
- `MLC-123` — R7: устойчивость джобов/службы (TTL-reaper бэкапов; ADR-40; recovery-политика) — Done (2026-06-14) · `e5a15f3`
- `MLC-124` — R8: релизный конвейер (`release.yml`/dependabot/ci-аудиты; DOC-08/REL-13) — Done (2026-06-14) · `508307e`
- `MLC-125` — R9a: security-барьер бэкенда (SEC-07 заголовки + SEC-08 rate-limit; ADR-41/42) — Done (2026-06-14) · `472cdf2`
- `MLC-126` — R9b: чистка поставки + firewall (REL-08/SEC-06/REL-17; ADR-43) — Done (2026-06-14) · `308fcf3`
- `MLC-127` — R9c: аккаунт службы (REL-06 подпись мастера + SEC-05; ADR-44) — Done (2026-06-14) · `5265954`
- `MLC-128` — R10-остаток: верификация (DOC-10 EventId, BE-15, FE-17/18, REL-11) — Done (2026-06-14) · `c266699`
- `MLC-129` — R11a: аудит — поиск/фильтр инициатора + навигация (UX-20/35/37/38; `SearchableSelect`) — Done (2026-06-14) · `b93aa1d`
- `MLC-130` — R11b: серверная пагинация /backups+/recordings (BE-17) + поиск /tenants (UX-05) — Done (2026-06-14) · `8e86532`
- `MLC-131` — R11c: client-side сорт/пагинация сеансов и диалога «не найдены» (UX-14/15/38) — Done (2026-06-14, PR #183) · `20e3fc9`
- `MLC-132` — R11d: расширение Zod read-границ всех основных фич (FE-09, основная часть) — Done (2026-06-14, PR #184) · `7411ee4`
- `MLC-133` — R11d-хвост: read-границы `useUsers`/`useUnassignedInfobases` (уже валидировались с MLC-060/093) — Done (2026-06-14, PR #185) · `f09d5a4`
- `MLC-134` — R12a: сброс диалогов FE-07/08 + инвалидация счётчиков FE-02 — Done (2026-06-14, PR #186) · `b15d492`
- `MLC-135` — R12b: freeze-дисциплина Perf-enum (BE-13) — Done (2026-06-14, PR #188) · `a013969`
- `MLC-136` — R12c: optimistic concurrency на Tenant (BE-02; аддитивный rowversion + 409) — Done (2026-06-14, PR #190) · `2f15339`
- `MLC-137` — R12d: терминология/тексты UI (UX-кластер D) — Done (2026-06-14, PR #192) · `5dec120`
- `MLC-138` — R12e: доступность WCAG AA + destructive-стиль (UX-кластер E) — Done (2026-06-14, PR #194) · `c0b84a4`
- `MLC-139` — R12f: онбординг и точки трения (UX-кластер F) — Done (2026-06-14, PR #196) · `953a737`

### Релиз 0.4.0-beta — MLC-140..144 (2026-06-14)

- `MLC-140` — Dependabot: 15 PR сведены в один (NuGet/npm/Actions); FA закреплён 6.12.2 (8.x — комм. лицензия) — Done (2026-06-14, PR #203) · `352da77`
- `MLC-141` — Упрощение диалогов подтверждения: type-to-confirm убран дифференцированно по обратимости (ADR-45) — Done (2026-06-14, PR #207) · `76abe51`
- `MLC-142` — Полировка «Быстродействия»: `SOS_WORK_DISPATCHER` в benign-wait + тултипы базы времени CPU — Done (2026-06-14, PR #205) · `5bdb8a3`
- `MLC-143` — Графики дашборда на recharts (топ-клиенты → `ComposedChart`, KPI-клики сохранены) — Done (2026-06-14, PR #206) · `c6aa1e4`
- `MLC-144` — Движок таблиц `@tanstack/react-table` (`DataTable`, ADR-46): 6 списков управления; perf-виджеты на shadcn — Done (2026-06-14, PR #209/212/211/210/213) · `38c5815`
  - `MLC-144a` — `DataTable` + хуки (density/URL-фильтры) + пилоты Tenants/Sessions — PR #209 · `38c5815`
  - `MLC-144b` — Базы (InfobasesPage): bulk-выбор/табы/URL-фильтры сохранены — PR #212 · `02fe829`
  - `MLC-144c` — инфобазы клиента (TenantDetailPage) — PR #211 · `13a13cd`
  - `MLC-144d` — Аудит (AuditTable): фильтры через auditUrlState, бейдж типа 1:1 — PR #213 · `084d2d5`
  - `MLC-144e` — Пользователи (UsersPage) — PR #210 · `54ba5b8`

### Вне трека (после релиза 0.4.0-beta)

- `MLC-146` — Уборка мёртвого кода после MLC-144: удалён осиротевший `InfobaseRow.tsx` (`InfobaseRow`/`InfobaseTableHeader`/`InfobaseHeaderSelection`) + неиспользуемый `infobaseColumnCount` из `infobaseFormat.ts` (`statusBadgeClass` оставлен); поведение не менялось — Done (2026-06-14)

### Релиз 0.5.0-beta — MLC-147..153, 027 (2026-06-14..15)

- `MLC-149` — Версия панели в подвале сайдбара из анонимного `/api/v1/health` (хук `useHealth`, скрыт при недоступном health) — Done (2026-06-14, PR #221) · `7596205`
- `MLC-148` — Холодный старт `/sessions`: прогрев cold-снапшота на старте + `RelativeTime` чинит `DateTime.MinValue` («ещё не обновлялось») holistically — Done (2026-06-14, PR #222) · `29a72ea`
- `MLC-147` — xlsx (SheetJS) → CDN-tarball `0.20.3` (обе high закрыты, `pnpm audit` 0); сборка зависит от cdn.sheetjs.com (DEVELOPMENT.md) — Done (2026-06-14, PR #220) · `bf6ac85`
- `MLC-027` — `i18n/ru.json` разнесён на 19 per-feature файлов `i18n/ru/*.json`, сборка в один namespace в `index.ts` (побайтово идентично) — Done (2026-06-14, PR #224) · `8beece9`
- `MLC-150` — Серверный фильтр «Базы» `notInCluster=true` (общий TTL-снапшот RAS, `ClusterAvailable`-флаг, честный баннер при недоступном RAS) — Done (2026-06-14, PR #226) · `c96a9f7`
- `MLC-151` — Optimistic concurrency на Infobase/Publication (rowversion→409, вариант b: токен и у Publication; фикс — токены в списочной проекции) — Done (2026-06-14, PR #228) · `b431ad9`
- `MLC-152` — Low-priv аккаунт службы: Procmon-трасса на стенде (вывод — в ADR-44 + `.claude/plans/mlc-152-stand-trace.md`); кодовая попытка снять `sysadmin` с бэкапа (`xp_*`→.NET) — Done (2026-06-14, PR #230), но **откачена** `MLC-153` · `96ad04a`
- `MLC-153` — Откат backup-части MLC-152 (`xp_*` восстановлены): для мультитенанта с динамическими базами `sysadmin` штатен (бэкап клиентских баз требует прав per-DB; серверного «backup any DB» короче sysadmin нет), .NET-rewrite давал лишь новую двух-учётную зависимость на каталог бэкапов без выгоды. Вывод трассы сохранён в ADR-44 — Done (2026-06-15, PR #232)

### Трек «Свежесть данных на страницах» — MLC-154..156 (2026-06-15)

- `MLC-155` — Глобальный `refetchOnWindowFocus: true` (React Query): админ-списки без поллинга (инфобазы/клиенты/пользователи/аудит/отчёты) освежаются при возврате на вкладку; «живые» хуки уже ставили true явно, статичные опт-аутятся (`useHealth`/`useAuth`) — Done (2026-06-15, PR #233) · `afcca03`
- `MLC-154` — Скрытый дефект: cold-обход сеансов был Hangfire-джобом `cold-snapshot` (`* * * * *`); минимум CRON = 1 мин делал throttle `Polling.ColdIntervalSeconds` мёртвым → реально 60с вместо 25с. Перенос в `ColdTierPollingService : BackgroundService` (таймер соблюдает интервал, дефолт 25→15с, немедленный warm-up); сняты `ColdThrottleState`, Hangfire-фильтры `[DisableConcurrentExecution]`/`[AutomaticRetry(0)]`, регистрация (RemoveIfExists для апгрейда); over-kill держит `IEnforcementGate` + ADR-28. ADR-6.1 Update + 6 канон-файлов — Done (2026-06-15, PR #234) · `c4c5e4a`
- `MLC-156` — `LiveControls` (shadcn): Пауза/Возобновить (заморозка `refetchInterval`) + «Обновить сейчас» на /sessions и «Быстродействии». На /sessions «Обновить сейчас» = live-форс cold-прогона: порт `ISessionRefreshTrigger.RunNowAsync` (single-flight + ожидание) в `ColdTierPollingService`, эндпоинт `POST /api/v1/sessions/refresh` (Viewer, 204, без аудита/DTO). На «Быстродействии» — refetch трёх live-ключей. Канон 04/05/06 — Done (2026-06-15, PR #235)
- `MLC-157` — Follow-up к MLC-154: устаревшая FE-подсказка поля «Частота полного опроса сеансов» («по умолчанию 25» → «15») в `i18n/ru/settings.json` — backend-дефолт стал 15с в MLC-154, FE-текст остался 25 (только подпись, поведение не затрагивалось). Выпущено патчем `0.5.2-beta` — Done (2026-06-15, PR #237)
- `MLC-158` — Наглядность паузы в `LiveControls`: цвет кодирует текущее состояние (зелёный «Авто-обновление» / янтарная плашка «На паузе»), кнопка «Возобновить» зелёная, «Пауза» нейтральная — снимает путаницу «статус или действие» (фидбэк владельца со стенда). Только фронт + i18n (`common.live`/`pausedStatus`) + UI-гайд. Выпущено патчем `0.5.3-beta` — Done (2026-06-15, PR #238)

### Трек «Авто-регистрация службы RAS» (ADR-47) — MLC-159..161 (2026-06-15)

- `MLC-159` — BE: панель управляет локальной службой RAS через `sc.exe` (порт `IRasServiceManager`/`ScRasServiceManager`, ADR-47). Обнаружение по `binPath` с `ras.exe` (имя не стандартизировано; ФС не трогает → удалённая папка старой платформы не роняет диагностику), 4 состояния (Ok/NotRegistered/Outdated/Stopped), `register`/`update`/`start` (update перенастраивает ту же службу `sc config`, без дублей). Эндпоинты `/api/v1/ras-service/{status,register,update,start}` (Admin), аудит 600/601/602 (freeze-тест + FE-зеркало). Версия из `OneC.DefaultPlatformVersion`, порт из `OneC.RAS.Endpoint`; команда `ras.exe cluster --service --port=<p> localhost:1540` version-agnostic (8.3/8.5); OEM-декод `sc.exe` — Done (2026-06-15, PR #241) · `15c8a66`
- `MLC-160` — FE: блок диагностики и лечения службы RAS в Настройках («Подключение к 1С/RAS», Admin-only): `RasServiceCard` (4 состояния + `StatusBadge`, `targetReady=false`→issue), `RasServiceActionDialog` (ADR-45 «да/нет», человеческое описание + точная команда `sc …`, 409→`detail`), `useRasService` (Zod `omittable`-схемы + omit-null тест; `/status` ленивый по «Проверить» + `staleTime` 60с — не бьёт по дорогому BE-обнаружению). namespace `settings.rasService.*` — Done (2026-06-15, PR #243) · `0e2092b`
- `MLC-161` — FE+релиз: сигнал недоступности RAS на дашборде (`RasHealthCard`, MLC-085) зафиксирован регрессионным тестом инварианта ADR-47 — питается дешёвым `summary.ras`, НЕ зовёт дорогой `/ras-service/status` (дашборд замечает → Настройки лечат). Версия `0.5.4-beta` (`Directory.Build.props` + `package.json` + `README`), ROADMAP → «выпущен», 05_FRONTEND — Done (2026-06-15, PR #245) · `a500bcb`

### Патч `0.5.5-beta` — MLC-162 (2026-06-15)

- `MLC-162` — Фикс (P1): блок RAS в Настройках показывал «не зарегистрирована» при реально существующей службе `1C:Enterprise 8.5 Remote Server` (поймано на dev-сервере владельца). **Первопричина:** `ScRasServiceManager` энумерировал службы через `sc query state= all` + `sc qc` из-под `Process.ArgumentList` — реальный `sc.exe` (нестандартный парсер `key= value`) такой вызов отклонял → пустая энумерация → `NotRegistered` (юнит-тесты мокали `IScProcessRunner`, реальный sc не дёргали). **Фикс:** обнаружение через реестр (`IServiceRegistryReader`/`RegistryServiceReader` — `HKLM\…\Services\*\ImagePath`, фильтр `ras.exe`, без спавнов; снимает и перф-риск MLC-159) + состояние через `IServiceStateReader`/`ServiceControllerStateReader`; `FindRasService` синхронный на чистом списке (тестируется фейк-ридерами, вкл. кейс дефекта). `IScProcessRunner.RunAsync` → одна raw-строка `Arguments` (как установщик), `RasServiceCommandBuilder` отдаёт командные строки create/config/start/stop (юнит-тест точной строки `binPath= "\"…\""`); `ScOutputParser`→`RasImagePathParser` (только path-парсеры). ADR-47 Update, 02/04 канон, версия `0.5.5-beta`. Наземно проверено: реестровый детектор находит службу (Running). Действия на реальном `sc.exe` / UI обновлённой панели — за владельцем — Done (2026-06-15, PR #248) · `0727ad4`

### Патч `0.5.6-beta` — MLC-163 (2026-06-15)

- `MLC-163` — Фикс (P1): «Проверить публикацию» (`POST /api/v1/publications/{id}/check`) падало 500 (`DbUpdateConcurrencyException`); фоновое обновление статусов публикаций молча не работало с релиза `0.5.0`. **Первопричина:** `PublicationStatusRefreshJob.ProcessOneAsync` грузил снимок публикации проекцией БЕЗ `RowVersion` и аттачил probe-`Publication` с пустым concurrency-токеном — после MLC-151 (`Publication.RowVersion`, `IsRowVersion`/`IsConcurrencyToken`) EF строил `UPDATE … SET LastCheck* WHERE Id=@id AND RowVersion=@token` с пустым токеном → 0 строк → исключение всегда (`RefreshAllAsync` гасил per-item; `RefreshOneAsync` пробрасывал → 500). К RAS-работе (MLC-159..162) не относится. **Фикс:** `StatusSnapshot` + `RowVersion` (проекция `p.RowVersion`), `probe.RowVersion = snapshot.RowVersion` → токен совпадает, targeted-UPDATE проходит; `RefreshOneAsync` ловит `DbUpdateConcurrencyException` как benign (Warning + detach probe, без 500 — статус подтянется циклом). Остались на `SaveChanges` (не `ExecuteUpdate` — InMemory не поддерживает). Тесты: регрессионный на **SQLite** (rowversion реально энфорсится; проверено — без фикса падает) + benign-конфликт (InMemory + interceptor). Версия `0.5.6-beta` — Done (2026-06-15, PR #251) · `ff2fabf`

### Трек «Релиз `0.6.0-beta`» (ADR-48) — MLC-164..166 (2026-06-15)

- `MLC-164` — Аудит: при добавлении базы (`POST /infobases`) убрана ложная вторая запись `PublicationCreated` «Публикация … создана». webinst не запускается, на IIS публикации нет (`LastCheckStatus=Unknown`) — запись вводила в заблуждение; реальная публикация логируется отдельно `PublicationPublished`. Остаётся только `InfobaseCreated`. Enum 20 заморожен → помечен историческим слотом (канон 03). Удалён ставший мёртвым хелпер `PublicationCreatedForInfobase`. Гейт: 299 тестов (Audit|Infobase) — Done (2026-06-15, PR #264) · `44294a3`
- `MLC-166` — Ядро (P1, **ADR-48**): `ConsumesLicense` = **факт `rac session list --licenses`**, не эвристика по white-list `app-id`. Адаптер `ListLicensedSessionIdsAsync` (членство `session`-GUID в выводе; нелицензионный сеанс отсутствует — подтверждено на стенде 8.5; exit≠0 → `null` = факт недоступен). Трёхсостояние `LicenseStatus {Consuming,NotConsuming,Pending}` + `SnapshotPayload.LicenseFactAvailable` + singleton `ILicenseFactCache`. Холодный тир — второй вызов `--licenses` + сшивка по `session`-GUID + обновление кэша; горячий — классификация из кэша (один спавн rac/тик). Подсчёт/kill — только `Consuming`; `KillEnforcer` пауза при `!LicenseFactAvailable`, grace-guard `max(KillGrace, ColdInterval)`. White-list `OneC.LicenseConsumingAppIds` удалён (`app-id` остаётся атрибутом сеанса). FE: `licenseStatus` (3 бейджа) + баннеры «данные о лицензиях недоступны»; настройка whitelist удалена. Тесты на **SQLite** (join, пауза, grace-guard). Канон 01/02/03/05/06/USER_GUIDE present-tense. Гейт: 923 backend + 601 frontend — Done (2026-06-15, PR #265) · `3dd0625`
- `MLC-165` — FE: «Сеансы» — фильтр по типам сеансов. Мультивыбор `app-id` (`SearchableMultiSelect`) с человеческими именами (`sessions.appTypes`); по умолчанию показаны только интерактивные клиенты (`INTERACTIVE_APP_IDS ∩ present`), фоновые скрыты до явного выбора; явный пустой выбор = «показать пусто». URL-параметр `appIds` (CSV), три состояния в `resolveAppIds`. `app-id` — атрибут отображения, не лицензия. Бамп версии `0.5.6-beta` → `0.6.0-beta` (закрытие трека). Гейт: 621 frontend-тест — Done (2026-06-15, PR #266) · `23ab1a9`

### Вне трека (follow-up к `0.6.0`) — MLC-167 (2026-06-15)

- `MLC-167` — FE (релиз `0.6.1-beta`): «Сеансы» — по умолчанию полный список **фактически лицензионных** сеансов. Тумблер «Только лицензионные» (`Switch`-примитив, `radix-ui` umbrella) **ВКЛ по умолчанию** (чистый URL = ВКЛ; `consuming=0` выкл): показывает `licenseStatus === "Consuming"`, фильтр типов перекрыт и приглушён. ВЫКЛ → фильтр типов из **полного каталога** `KNOWN_APP_IDS` (13 типов, канонический порядок) ∪ присутствующие незнакомые app-id (можно отметить тип заранее, до появления онлайн) + действие «Выбрать все / Снять все» в `SearchableMultiSelect`. Дефолт выбора типов (без `appIds`) — интерактивные. Бамп `0.6.0-beta` → `0.6.1-beta`. Гейт: 636 frontend-тест, type-check/lint чисто — Done (2026-06-15, PR #267) · `1889f1c`

### Вне трека (follow-up к `0.6.0`) — MLC-168 (2026-06-15)

- `MLC-168` — BE (релиз `0.6.2-beta`): отчёт «Использование лицензий» больше не пишет ложный 0 в окне недоступности факта rac. **Причина:** холодный цикл (`ReconciliationJob`) сэмплировал потребление **безусловно**; при сбое `rac --licenses` (`licenseFactAvailable=false`) все сеансы становятся `Pending` → `CountByTenant`=0 → в `LicenseUsageSnapshot` уходил ложный 0, занижая min/avg бакета. **Фикс:** при `!licenseFactAvailable` холодный цикл **пропускает** `RecordSample` — честный пробел в истории вместо ложного 0 (аккумулятор переживает пропуск циклов: прошлый бакет флашится следующим доступным семплом, ADR-25/48). enforcement-пауза при сбое уже была (MLC-166) — это её недостающая половина для отчёта. Тест на SQLite-независимом пути (InMemory + stub-аккумулятор): при недоступном факте `RecordSample` не вызывается, строки не пишутся. Канон — ADR-48 Update. Гейт: 924 backend-тест — Done (2026-06-15, PR #268) · `mlc-168`

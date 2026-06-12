# A2 — SECURITY: авторизация, аутентификация, инъекции, секреты, инсталлятор

Этап А2 пред-релизного аудита (волна независимых этапов). Дата снятия: **2026-06-12**, репозиторий
`F:\dev\MitLicense Center`, ветка `main`, HEAD `e6b1317`. Режим — **read-only**: код не правился, эксплойты
не запускались. Первоисточник — код, конфиги, скрипты, `installer\MitLicenseCenter.iss`; docs/SECURITY.md
сами объект проверки. Стартовая карта эндпоинтов взята из A0-BASELINE и перепроверена по коду.

Метод: 6 субагентов-opus по зонам (карта авторизации · cookie-auth/CSRF · инъекции · секреты ·
инсталлятор+HTTP · сверка SECURITY.md). Каждый High дополнительно проверен субагентом-скептиком («опровергни»);
по итогам скепсиса понижены SEC-10 (CSRF, был High→Low), SEC-06 (firewall, High→Medium), SEC-07 (заголовки,
High→Medium); SEC-01 (неотзыв сессий) скепсис подтвердил как High.

---

## 0. Сводка

| Severity | Кол-во | Номера |
|---|---|---|
| **Blocker** | 0 | — |
| **High** | 1 | SEC-01 |
| **Medium** | 8 | SEC-02..SEC-09 |
| **Low** | 7 | SEC-10..SEC-16 |
| **Info / accepted** | 1 | SEC-17 |

Дополнительно — **2 расхождения SECURITY.md↔код** (SEC-DOC-1, SEC-DOC-2) и **4 пробела документа** (GAP-1..4).

**Карта авторизации — чистая:** все 71 эндпоинт `/api/v1` имеют явный requirement, анонимных/забытых нет,
админских мутаций под Viewer/без роли нет (кроме сознательного ADR-27 — запуск бэкапа). Инъекций
(командных/SQL) **не найдено** — все внешние процессы через `ArgumentList`, весь raw-SQL параметризован.
Основной профиль рисков — **постустановочная конфигурация хоста**: привилегии службы, ACL на секреты,
firewall и отзыв доступа, а не дыры в коде эндпоинтов.

---

## 1. Карта авторизации (зона 1) — подтверждена, дыр нет

Прошёл все 16 файлов `Endpoints/**` + `Program.cs`. **Число и разбивка A0 подтверждены без расхождений: 71 эндпоинт.**

- Политики (`Program.cs:98-100`): `Admin` = `RequireRole("Admin")`; `Viewer` = `RequireRole("Admin","Viewer")`.
  Имена политик == имена ролей (`Roles.cs:5-6`).
- AllowAnonymous = 3 (`auth/login`, `health`, `health/ready`); голый `RequireAuthorization()` без роли = 3
  (`auth/logout`, `auth/me`, `auth/change-password` — корректно, любой аутентифицированный); Viewer = 23 (чтение);
  Admin = 42 (все мутации + `discovery` + `settings` + `unassigned`).
- **Ни одного эндпоинта без requirement** — отсутствие глобального `FallbackPolicy` не создаёт анонимных дыр.
- Групповые Admin-политики (`discovery`, `infobases/unassigned`) наследуются всеми дочерними маршрутами —
  переопределения в сторону ослабления нет.
- `MapControllers`/`MapHub`/`MapRazorPages` отсутствуют (только minimal API). **CORS не настроен вообще**
  (SPA same-origin из `wwwroot`, ADR-30) — кросс-origin credentialed-доступ закрыт браузером.
- Порядок middleware корректен: `UseAuthentication()` → `UseAuthorization()` (`Program.cs:213-214`) до маппинга.
- `/hangfire`: `AdminOnlyDashboardAuthorizationFilter` — `IsAuthenticated && IsInRole("Admin")`
  (`AdminOnlyDashboardAuthorizationFilter.cs:12`); неаутентифицированный → 401/403, обхода нет.
- `/api/docs` (Swagger): за флагом `Security:EnableSwagger`, дефолт **`false`** и в `appsettings.json`, и в
  `appsettings.Production.json` — в prod по умолчанию закрыт.
- SPA-fallback `AllowAnonymous`, но `/api/*` и `/hangfire/*` → 404, не index.html (`SpaFallback.cs:11-13`).

---

## 2. Находки

### [SEC-01] Отключение пользователя и admin-reset пароля НЕ отзывают активные сессии (нет SecurityStampValidator)
- **Severity: High** · **Confidence: High** · *(скептик подтвердил High)*
- **Evidence:** аутентификация собрана как `AddAuthentication(IdentityConstants.ApplicationScheme)
  .AddCookie(...)` (`Program.cs:73-96`) — **bare AddCookie**, а не `AddIdentity`. DI использует
  `AddIdentityCore<AppUser>()...AddSignInManager()` (`Infrastructure/DependencyInjection.cs:78-92`), который
  **не** регистрирует `SecurityStampValidator` и **не** вешает его на `CookieAuthenticationOptions.Events
  .OnValidatePrincipal`. Grep по `OnValidatePrincipal|SecurityStampValidator|ValidateSecurityStamp|
  ValidationInterval|UpdateSecurityStamp` в `backend/src` — **0 совпадений**. `ExpireTimeSpan=8h`,
  `SlidingExpiration=true` (`Program.cs:83-84`). Disable = `SetLockoutEndDateAsync(user, MaxValue)`
  (`UsersEndpoints.cs:204`) — проверяется только при следующем `PasswordSignInAsync`, не на каждом запросе.
  Альтернативного механизма отзыва нет: ни custom middleware, ни `IClaimsTransformation` (grep — 0).
- **Сценарий атаки:** админ обнаруживает компрометацию учётки сотрудника, отключает её / сбрасывает пароль.
  Атакующий с уже открытой сессией (украденной кукой) сохраняет полный доступ до 8 ч, а при активности —
  бессрочно (sliding). Реакция «отозвать доступ немедленно» по факту не работает. Смена роли Admin→Viewer
  также не вступает в силу до пере-логина (claims в куке старые).
- **Рекомендация:** перейти на полноценную Identity-cookie-связку (`AddIdentity<AppUser,AppRole>()`) либо вручную
  зарегистрировать `SecurityStampValidator` в `OnValidatePrincipal` с коротким `ValidationInterval` (1–5 мин);
  в `DisableAsync`/`ChangeRoleAsync` вызывать `UpdateSecurityStampAsync`. Добавить тест «disabled user → активная
  кука перестаёт работать в пределах интервала».
- **Примечание:** собственная смена пароля (`/auth/change-password`) корректно делает `RefreshSignInAsync`
  (`AuthEndpoints.cs:192`) — своя сессия сохраняется ожидаемо; проблема в **чужих** сессиях при admin-действиях.

### [SEC-02] Plaintext SQL-пароль в appsettings.Production.json (Program Files), читаем всеми Users
- **Severity: Medium** · **Confidence: High** *(ACL-факт), Medium по эксплуатируемости*
- **Evidence:** в режиме SQL-auth (служба под LocalSystem) инсталлятор пишет строку подключения с
  `User Id=...;Password=<plaintext>` (`installer\MitLicenseCenter.iss:362`, `WriteProductionConfig:368-397`) в
  `{app}\appsettings.Production.json` = `C:\Program Files\MitLicense Center\...`. Дефолтный ACL Program Files —
  `Users:(RX)` (read для всех). На файл **не** накладывается `icacls` (`GrantServiceAccountRights` трогает только
  `{commonappdata}`, не `{app}`). Мастер допускает SQL-логин с sysadmin (`iss:~804`).
- **Сценарий атаки:** любой локальный непривилегированный (RDP/интерактивный) пользователь хоста читает файл и
  получает SQL-логин/пароль → доступ к SQL Server (потенциально sysadmin).
- **Рекомендация:** (а) при записи выставлять рестриктивный ACL на `appsettings.Production.json` (снять
  inheritance; оставить SYSTEM/Administrators/сервис-аккаунт); (б) предпочитать Windows-auth (режим A — пароля в
  файле нет); (в) в OPERATIONS зафиксировать требование минимальных прав SQL-логина (db_owner на БД панели, не sa).

### [SEC-03] Key ring DataProtection не защищён DPAPI (мастер-ключи на диске потенциально в plaintext)
- **Severity: Medium** · **Confidence: High**
- **Evidence:** `AddDataProtection().SetApplicationName("MitLicenseCenter").PersistKeysToFileSystem(keyDirectory)`
  (`Infrastructure\DependencyInjection.cs:258-274`) — **без** `.ProtectKeysWithDpapi()` (grep `ProtectKeysWith` по
  коду — 0; есть только в docs/SECURITY). Каталог: prod → `%ProgramData%\MitLicenseCenter\keys`. Этими ключами
  расшифровываются секреты `dbo.Settings` (пароль кластера 1С, purpose `mlc.settings.v1`, `SettingsStore.cs:15`)
  и cookie-тикеты. Под службой/LocalSystem без загруженного user-profile DataProtection может записать XML-ключи
  в открытом виде (документированное поведение фреймворка), полагаясь далее лишь на ACL каталога.
- **Сценарий атаки:** доступ к каталогу `keys` или к его бэкапу (key ring + БД — единый бэкап-юнит) → офлайн-
  расшифровка всех секретных настроек. «Защита в покое» сводится к ACL, а не к крипто.
- **Рекомендация:** добавить явный `.ProtectKeysWithDpapi(protectToLocalMachine: true)` (Windows-only ветка,
  `[SupportedOSPlatform("windows")]`); тогда XML-ключи зашифрованы DPAPI machine-scope и бесполезны вне хоста.
  Связано с SEC-DOC-1.

### [SEC-04] Слабый ACL на %ProgramData%\MitLicenseCenter в режиме B (LocalSystem)
- **Severity: Medium** · **Confidence: High**
- **Evidence:** `[Dirs]` создаёт `{commonappdata}\MitLicenseCenter` без атрибута `Permissions`
  (`installer\MitLicenseCenter.iss:71-75`); дефолтный ACL `%ProgramData%` даёт `Users:(RX)`. `GrantServiceAccount
  Rights` (`iss:445-465`) делает `icacls /grant` только в режиме A (Windows-auth) и **early-exit** при
  `AuthMode<>AUTH_WINDOWS` — в режиме B (LocalSystem) ACL каталога не ужесточается вовсе. Любой непривилегированный
  пользователь хоста может читать key ring XML, зашифрованные секреты и одноразовый `initial-admin.secret` (пока он
  существует до первого старта сидера).
- **Сценарий атаки:** в связке с SEC-03 (ключи незашифрованы) низкопривилегированный пользователь читает ключи +
  данные настроек → восстанавливает пароль кластера 1С; либо успевает прочитать `initial-admin.secret`.
- **Рекомендация:** в установщике явно ужесточать ACL каталога (снять inheritance; SYSTEM + Administrators +
  сервис-аккаунт) в **обоих** режимах, не только A.

### [SEC-05] Служба под LocalSystem / local-Administrators — привилегии выше реальной потребности
- **Severity: Medium** · **Confidence: Medium** *(привилегия реальна; точный минимум требует runtime-проверки)*
- **Evidence:** в режиме B служба регистрируется под **LocalSystem** (`installer\MitLicenseCenter.iss:489,~807`);
  CLAUDE.md/OPERATIONS требуют для IIS-логики членства сервис-аккаунта в локальных Administrators либо явного read
  на `%windir%\system32\inetsrv\config`. Фактическая потребность — **read на `inetsrv\config`**; выдаётся —
  LocalSystem/local-admin (+ SQL sysadmin, проверяется `IS_SRVROLEMEMBER('sysadmin')`, `SqlBackupAdapter.cs:233`).
  Установщик не выдаёт узкий ACE на `inetsrv\config`, полагаясь на ручное добавление аккаунта в Administrators.
- **Сценарий атаки:** компрометация web-процесса (парсит внешний CP866-вывод rac.exe, говорит с IIS/SQL) при
  LocalSystem = немедленно SYSTEM на хосте + sysadmin на SQL. Нет привилегированного барьера.
- **Рекомендация:** выделенный low-priv сервис-аккаунт + `SeServiceLogonRight` + явный read-ACE на `inetsrv\config`
  (или членство в `IIS_IUSRS`), выдаваемый установщиком, вместо Administrators/LocalSystem. Уточнить рантаймом,
  какие именно файлы `inetsrv\config` читает `ServerManager`.

### [SEC-06] Firewall-правило без profile= / remoteip= — открыто на все профили и любой IP
- **Severity: Medium** · **Confidence: High** *(скептик понизил High→Medium по threat-model)*
- **Evidence:** `GetFirewallAddParams` (`installer\MitLicenseCenter.iss:496-497`) строит
  `advfirewall firewall add rule ... dir=in action=allow protocol=TCP localport=<port>` — **без `profile=`**
  (Domain+Private+Public) и **без `remoteip=`** (любой источник). Kestrel слушает `http://+:<port>` (все
  интерфейсы, `iss:379`). `AllowedHosts` — фильтр Host-заголовка, не сетевой ACL.
- **Сценарий атаки:** HTTP-порт панели доступен из всей сети, включая Public-профиль. Смягчение: cookie
  `Secure=Always` в prod (`Program.cs:80-82`) → по plain HTTP браузер куку не пошлёт, рабочая аутентифицированная
  сессия снаружи по этому транспорту недоступна; экспонируются прежде всего **неаутентифицированные** эндпоинты
  (health, login POST — surface для credential-stuffing) и анонимная SPA-оболочка. Поэтому не High.
- **Рекомендация:** scope правила (`profile=domain,private` и/или `remoteip=localsubnet`), либо bind Kestrel на
  localhost с обязательным reverse-proxy (TLS) и документированием.

### [SEC-07] Нет security-заголовков (CSP / X-Frame-Options / X-Content-Type-Options / Referrer-Policy)
- **Severity: Medium** · **Confidence: High** *(скептик понизил High→Medium)*
- **Evidence:** полный grep по `MitLicenseCenter.Web` на `Content-Security-Policy|X-Frame-Options|X-Content-Type|
  Referrer-Policy|frame-ancestors|Headers.Append|OnPrepareResponse` → единственный hit — `Cache-Control` на
  SPA-fallback (`Program.cs:283`). `UseStaticFiles()` без `OnPrepareResponse`. HSTS (`UseHsts`, `Program.cs:203`)
  только при `Security:EnforceHttps=true` (дефолт false, установщик пишет false). Сторонних header-middleware нет.
- **Сценарий атаки:** SPA отдаётся без CSP (любой будущий XSS выполняет действия в сессии; SameSite=Strict не
  ограничивает same-origin XSS-запросы) и без anti-clickjacking (frame + обман оператора). Смягчение: cookie
  `HttpOnly` (XSS не украдёт куку напрямую) + same-origin модель — поэтому Medium, не High.
- **Рекомендация:** одно header-middleware перед `UseStaticFiles`: `Content-Security-Policy` (default-src 'self',
  жёсткие script/style), `X-Frame-Options: DENY` / `frame-ancestors 'none'`, `X-Content-Type-Options: nosniff`,
  `Referrer-Policy: no-referrer`.

### [SEC-08] Нет rate-limiting на /auth/login; lockout по учётке → account-lockout DoS
- **Severity: Medium** · **Confidence: High**
- **Evidence:** `AddRateLimiter`/`UseRateLimiter` отсутствуют (grep — 0). Единственная защита перебора — Identity
  lockout 5 попыток / 15 мин (`DependencyInjection.cs:86-87`, `lockoutOnFailure:true` в `AuthEndpoints.cs:51`).
  Lockout привязан к учётке, не к IP. Дефолтный логин `admin` известен (`IdentitySeeder.cs`).
- **Сценарий атаки:** (а) брутфорс распределяется по множеству username без enumeration; (б) тривиальный DoS —
  5 неверных попыток блокируют легитимного админа на 15 мин (защита «последнего активного admin» — только в UI,
  не от внешнего lockout); нет потолка запросов к `/login` по IP.
- **Рекомендация:** ASP.NET `RateLimiter` (fixed-window по IP) на группу `/auth` как дополнение к per-account lockout.

### [SEC-09] HTTP по умолчанию на всех интерфейсах без enforcement TLS
- **Severity: Medium** · **Confidence: Medium**
- **Evidence:** `appsettings.Production.json` пишется с `EnforceHttps=false` (`iss:388`); Urls `http://+:<port>`;
  cookie `Secure=Always` в prod (`Program.cs:80-82`). Из коробки панель говорит plaintext HTTP на всех интерфейсах;
  при этом по http браузер куку не пошлёт → логин фактически не работает без TLS-терминирующего прокси (это и есть
  предполагаемый деплой, но зависимость от прокси неявная, не форсится).
- **Сценарий атаки:** дефолтная поза — незашифрованный admin-трафик; при попытке работать по http без прокси
  аутентификация ломается (cookie не уходит). Связано с SEC-06.
- **Рекомендация:** документировать/форсить требование reverse-proxy + TLS; рассмотреть bind на localhost по
  умолчанию, чтобы открытое firewall-правило (SEC-06) не экспонировало plaintext.

### [SEC-10] CSRF: единственный барьер — SameSite=Strict, antiforgery-токена нет
- **Severity: Low** · **Confidence: High** *(скептик понизил High→Low; допустимо Medium по политике «глубины обороны»)*
- **Evidence:** в backend нет `AddAntiforgery`/`IAntiforgery`/`ValidateAntiForgeryToken`/проверки `Origin`/`Referer`/
  кастом-заголовка (grep — 0). FE (`frontend/src/lib/api.ts:80-89`) шлёт только `credentials:"include"` +
  `Accept`/`Content-Type`, CSRF-токен не шлёт. Единственная мера — `Cookie.SameSite=Strict` (`Program.cs:79`).
- **Почему Low:** приложение **строго same-origin** (SPA из `wwwroot` тем же Kestrel; dev — vite-proxy), CORS не
  настроен → кросс-origin credentialed-запросы блокирует браузер; SameSite=Strict не прикрепляет куку ни к одному
  кросс-сайтовому запросу → классический CSRF закрыт. Сервер **одноузловой без поддоменов** (ADR-28) → остаточные
  векторы SameSite (sibling-subdomain, same-site gadget) неприменимы. Реалистичной CSRF-эксплуатации на этой
  конфигурации нет.
- **Рекомендация:** зафиксировать как осознанное решение «CSRF mitigated by SameSite=Strict, same-origin, no CORS».
  При будущем добавлении поддоменов/CORS — обязательный antiforgery-токен либо серверная проверка `Origin`.
  Связано с SEC-DOC-2 / GAP-2.

### [SEC-11] Path traversal в путях публикации (VirtualPath / PhysicalPathOverride)
- **Severity: Low** · **Confidence: High**
- **Evidence:** `VirtualPath` валидируется только на «начинается с `/`» и «без пробелов» — `..` не блокируется
  (`Endpoints\Shared\InfobaseValidationRules.cs:44-75`); `PhysicalPathOverride` принимается как любой
  fully-qualified путь и возвращается резолвером как есть (`Publishing\VrdPathResolver.cs:19-28`). Эти значения →
  `Path.Combine` → `File.WriteAllText` web.config/vrd (`OneCIisPublishingService.cs:160`).
- **Почему Low:** операция только под **Admin**; пишется всегда файл `web.config`/`default.vrd` с содержимым,
  производным от существующего файла (патчится только version-сегмент; нет целевого файла → запись не идёт).
  Полноценной «записи произвольного содержимого куда угодно» нет.
- **Рекомендация:** в `AppendPublicationFieldErrors` отклонять `VirtualPath` с `..`/`\`; для `PhysicalPathOverride` —
  валидировать против whitelisted-корня (`IIS.DefaultVrdRoot`) либо задокументировать как доверенный Admin-ввод.

### [SEC-12] DatabaseName без валидации символов/длины → в путь файла бэкапа
- **Severity: Low** · **Confidence: Medium**
- **Evidence:** `ValidateInfobase` проверяет `DatabaseName` только на `IsNullOrWhiteSpace`
  (`Endpoints\Infobases\InfobasesEndpoints.cs:453-472`) — ни длины (гоча: `[StringLength]` в minimal API не
  исполняется в runtime), ни запрета `..`/`\`/абсолютного пути. Имя → `Path.Combine(folderRoot, databaseName)`
  (`Backups\SqlBackupAdapter.cs:122`): абсолютный `databaseName` отбрасывает `folderRoot`.
- **Почему Low/Medium:** создание/правка ИБ — только **Admin**; перед построением пути проверяется
  `DatabaseExistsAsync` (`sys.databases WHERE name=@db`) — БД с таким именем должна реально существовать; запись
  под SQL-сервером, не под процессом панели.
- **Рекомендация:** в `ValidateInfobase` — max-длина + запрет `\ / : * ? " < > |` и `..`; зеркалить на FE
  (`features/infobases/validation.ts`, parity-тесты).

### [SEC-13] Semicolon-injection в строку соединения 1С через Infobase.Name
- **Severity: Low** · **Confidence: Medium**
- **Evidence:** `$"Srvr={clusterServer};Ref={infobaseName};"` (`Publishing\WebinstArgs.cs:37`); `Infobase.Name`
  валидируется только на непустоту. Имя с `;` теоретически добавит лишние пары в connstr 1С (это не shell и не SQL —
  передаётся одним `ArgumentList`-токеном webinst).
- **Почему Low:** только **Admin**; влияние ограничено семантикой строки соединения webinst, за процесс не выходит.
- **Рекомендация:** отклонять `;`/`=` в `Infobase.Name` либо задокументировать ограничение.

### [SEC-14] Пароль кластера 1С в аргументах rac.exe (виден в списке процессов)
- **Severity: Low** · **Confidence: High** · *(принятый риск, раскрыт в SECURITY.md — корректно)*
- **Evidence:** пароль уходит в `--cluster-pwd={password}` через `ProcessStartInfo.ArgumentList`
  (`Clusters\RacExecutableRasClusterClient.cs:466`, runner `SystemProcessRacRunner.cs:53-56`, `UseShellExecute=false`).
  Аргументы дочернего процесса видны другим процессам хоста (WMI `Win32_Process.CommandLine`) на время вызова.
- **Смягчение:** ограничение CLI-контракта rac.exe (нет stdin/env-канала для пароля, ADR-3.3); пароль НЕ попадает
  в логи (`LogRacFailed` логирует только stderr, rac его не эхоит); вызовы короткоживущие.
- **Рекомендация:** принять как остаточный риск (так и заявлено в SECURITY.md); убедиться, что хост не даёт
  непривилегированным пользователям список процессов.

### [SEC-15] Дефолтный admin не форсит смену пароля при первом входе
- **Severity: Low** · **Confidence: Medium**
- **Evidence:** admin создаётся с паролем оператора (из `initial-admin.secret`) либо random в лог Warning
  (`IdentitySeeder.cs:79-100,169-201`); механизма «сменить при первом входе» нет.
- **Смягчение:** парольная политика 12+ с классами; random криптослучаен; смена доступна через UI.
- **Рекомендация:** опционально флаг «требовать смену при первом входе» для admin либо явная рекомендация в
  FinishedLabel/OPERATIONS.

### [SEC-16] User enumeration облегчён известным дефолтным admin (в связке с отсутствием rate-limit)
- **Severity: Low** · **Confidence: High**
- **Evidence:** `LoginAsync` возвращает одинаковый `Unauthorized()` и при неуспехе, и при `user is null`
  (`AuthEndpoints.cs:53-62`) — различия в теле/коде нет (корректно). Остаётся известный дефолтный логин `admin`
  (`IdentitySeeder.DefaultAdminUserName`), что в связке с SEC-08 облегчает таргетированный брутфорс/lockout-DoS.
- **Рекомендация:** косвенно закрывается SEC-08 (rate-limit); опционально — возможность переименовать дефолтного admin.

### [SEC-17] POST /api/v1/backups (запуск бэкапа) доступен роли Viewer
- **Severity: Info / accepted** · **Confidence: High**
- **Evidence:** `MapPost("/", StartAsync).RequireAuthorization(Roles.Viewer)` (`Backups\BackupsEndpoints.cs:34`).
- **Оценка:** **сознательное решение ADR-27** (комментарий `BackupsEndpoints.cs:20-21,72`): запуск = операторы
  (Viewer), удаление = Admin. Дубль активной базы → 409 `BACKUP_ACTIVE`, аудит пишется. Это не дефект относительно
  SECURITY.md (документ не заявляет «все мутации — Admin»). Отметить как accepted risk при сверке с ADR-27; если
  куратор решит сделать запуск Admin-only — правка одной строки, но это расхождение с каноном, а не баг.

---

## 3. Инъекции — инвентарь (зона 3): дыр не найдено

**Процессы — все через `ArgumentList`, `UseShellExecute=false`, shell не используется:**

| Точка | Файл | Вердикт |
|---|---|---|
| rac.exe | `Clusters\SystemProcessRacRunner.cs:44-56` | Безопасно (каждый арг — отдельный `ArgumentList.Add`) |
| webinst.exe | `Publishing\OneCWebinstPublisher.cs:101-110` | Безопасно от арг-инъекции (`ArgumentList`) |
| iisreset.exe | `Publishing\OneCIisLifecycleService.cs:227-236` | Безопасно (только статические `/stop`,`/start`) |
| webinst exePath | `Publishing\WebinstExeResolver.cs:24` | Безопасно (PlatformVersion regex `^\d+\.\d+\.\d+\.\d+$` + `File.Exists`) |
| IIS managed-операции | `OneCIisLifecycleService.cs`,`OneCIisPublishingService.cs` | Безопасно (managed `ServerManager`-индексаторы, не shell) |

**SQL — весь raw/ADO.NET параметризован:**

| Точка | Файл | Вердикт |
|---|---|---|
| Discovery баз | `Discovery\SqlDatabaseDiscovery.cs:51` | Безопасно (статика; сервер → `SqlConnectionStringBuilder.DataSource`) |
| DMV-проба | `Performance\SqlPerformanceProbe.cs:122-223` | Безопасно (`@top`/`@textLen` параметры) |
| Бэкап | `Backups\SqlBackupAdapter.cs:243-357` | Безопасно по SQL (`QUOTENAME(@db)` в `sp_executesql`, путь — `@`-параметр) |
| Audit retention | `Jobs\AuditRetentionJob.cs:68` | Безопасно (`ExecuteSqlInterpolated` авто-параметризует) |
| Создание БД панели | `Persistence\DatabaseBootstrapper.cs:37` | Безопасно (`DB_ID(@name)` + экранирование `]`→`]]`) |

Path-traversal — два места с недостаточной валидацией (SEC-11, SEC-12), оба Low (только Admin + конструктивные
ограничители). Системная слабость, общая для SEC-11/12/13 — **ручные хелперы валидации не режут ни длину, ни
path/connstr-метасимволы** (известная гоча: DataAnnotations в minimal API не исполняются). Закрывается одной
правкой хелперов + зеркалом на FE.

---

## 4. Секреты — как устроено (зона 4)

- **Шифрование секретных настроек** (пароль кластера 1С и пр.): значения `dbo.Settings.Value` шифруются
  DataProtection-протектором purpose `mlc.settings.v1` (`SettingsStore.cs:15,29,106`) — **работает**. Слабость не в
  значениях, а в защите самого key ring (SEC-03/04).
- **Settings API чист:** GET не отдаёт значения `IsSecret` (двойная маскировка `Value:null` — в store и в эндпоинте,
  оба под Admin); PUT audit-description секрета не содержит значения (`SettingsEndpoints.cs:89-92`).
- **Аудит чист:** `AuditLogger` пишет только переданные caller'ом строки; вызовы не кладут секреты.
- **`EnableSensitiveDataLogging`** — двойной opt-in флаг (`Diagnostics:EfQueryProfiling` + `EfSensitiveDataLogging`),
  по умолчанию off, в прод-конфигах флагов нет (`EfQueryProfiling.cs`). Чисто.
- **Connection string при старте не логируется** (`Program.cs:176` — исключение без строки; SqlException не содержит
  пароль). Чисто.
- **Installer test-connection** не светит пароль в командной строке (connstr в single-quoted литерале временного
  `.ps1`, удаляется сразу). Чисто.
- **`initial-admin.secret`** одноразовый, удаляется сидером (`IdentitySeeder.cs:69,143`). Контракт корректен (вопрос
  ACL до удаления — покрыт SEC-04).
- **Committed appsettings** содержат только плейсхолдеры (`YOUR-SQL-HOST`, `Trusted_Connection`) — реальных секретов
  в репо нет (подтверждает A0).

---

## 5. SPA / HTTP / инсталлятор — что чисто (зоны 5,6)

- **5xx санитизированы:** `AddProblemDetails` переписывает все 5xx в общий русский месседж без stack/исключения/SQL
  (`Program.cs:45-62`); в prod — `UseExceptionHandler()`, не `DeveloperExceptionPage` (тот только в Development).
- **Swagger** off по умолчанию в prod (флаг `Security:EnableSwagger`).
- **Статика:** из `wwwroot` (под exe в Program Files); `appsettings.Production.json` лежит в родительском `{app}`,
  **вне** web-root → не раздаётся; directory browsing выключен; только known MIME.
- **Hangfire** под Admin; `DisplayStorageConnectionString=false`.
- **Cookie hardening:** `HttpOnly`, `SameSite=Strict`, `Secure=Always` в prod, 401/403 JSON вместо 302, имя
  `mlc.auth`, 8h+sliding.
- **Upgrade** сохраняет config/keyring/БД, не сбрасывает ACL (`/grant`, не `/reset`); uninstall сохраняет key ring.
- **Парольная политика** 12+ символов, 4 класса — выше дефолта Identity; parity BE↔FE↔генератор временных паролей.
- **Lockout** реально включён (`lockoutOnFailure:true`), 5/15 мин; session fixation — штатная регенерация тикета
  через `SignInManager.PasswordSignInAsync` (кастомного выпуска cookie нет).

---

## 6. Сверка SECURITY.md ↔ код (зона 7)

Документ короткий (~20 строк, 5 «ключевых решений» + модель угроз). В целом **честен и осторожен** (риск с rac.exe
раскрыт корректно, hardening-флаги описаны точно, single-host/perimeter-модель совпадает с кодом). Подтверждено
кодом: модель угроз (ADR-7/15/28), key ring+БД = единый бэкап-юнит, hardening-флаги `Security:*`, серверные роли
Admin/Viewer, парольная политика 12+/lockout 5-15мин, 2FA вне scope, cookie-флаги. **Два расхождения:**

### [SEC-DOC-1] «Секреты в покое — DPAPI» переоценивает гарантию
- **Severity: Medium** · **Confidence: High**
- Заявлено: SECURITY.md «Секреты в покое — DPAPI (ASP.NET Data Protection)»; ADR-8 (`DECISIONS.md:52`)
  «DPAPI-backed on Windows»; `04_INFRASTRUCTURE.md:92` «DPAPI-backed». ↔ Код: `DependencyInjection.cs:270-273` —
  `PersistKeysToFileSystem` **без** `ProtectKeysWithDpapi`. Значения шифруются, но защита **ключей** в покое отдана
  дефолту фреймворка (под LocalSystem может оказаться plaintext-XML).
- **Последствие доверия:** оператор считает каталог `keys` «зашифрованным» и бэкапит как защищённый, тогда как ключи
  могут лежать в открытом виде → расшифровка всех секретов. Связано с SEC-03.
- **Рекомендация:** либо добавить `ProtectKeysWithDpapi` (см. SEC-03), либо переформулировать: «значения шифруются
  Data Protection API; защита ключей в покое — ACL каталога + опционально DPAPI, зависит от учётки службы».

### [SEC-DOC-2] Не раскрыто отсутствие немедленного отзыва доступа
- **Severity: Medium** · **Confidence: High**
- SECURITY.md/ADR-7 подают cookie-auth как полноценную управляемую модель Admin/Viewer, но нигде не оговаривают, что
  отзыв (disable/смена роли/admin-reset) вступает в силу лишь по истечении cookie (≤8 ч, sliding). Это прямое
  следствие SEC-01.
- **Рекомендация:** либо исправить SEC-01, либо явно зафиксировать как принятый риск: «отзыв доступа вступает в силу
  в течение ≤8 ч; немедленный отзыв требует ручного действия».

### Пробелы документа (риски, не упомянутые в SECURITY.md)
- **GAP-1 — привилегии службы не описаны.** LocalSystem/local-admin + SQL sysadmin; компрометация панели = SYSTEM на
  хосте + sysadmin на SQL. Стоит добавить раздел «Service privileges» с принятым риском (см. SEC-05).
- **GAP-2 — CSRF-модель не описана.** Защита только SameSite=Strict; стоит зафиксировать как осознанное решение
  (см. SEC-10).
- **GAP-3 — заголовки безопасности (CSP/X-Frame/nosniff/Referrer) не упомянуты** (см. SEC-07).
- **GAP-4 — Swagger/Hangfire как поверхность** раскрыты в OPERATIONS, но не в SECURITY.md — для полноты модели угроз
  стоит сослаться.

---

## 7. Финал

### (а) Топ-5 рисков
1. **SEC-01 (High)** — отключение/смена пароля не отзывают активные сессии: «уволенный» оператор работает до 8 ч.
   Единственный High; ядро модели cookie-auth.
2. **SEC-02 (Medium)** — plaintext SQL-пароль в `appsettings.Production.json` (Program Files), читаем всеми Users в
   режиме SQL-auth.
3. **SEC-03 + SEC-04 (Medium, связка)** — key ring не защищён DPAPI + слабый ACL каталога в режиме LocalSystem:
   локальный непривилегированный пользователь расшифровывает все секреты БД.
4. **SEC-05 (Medium)** — служба под LocalSystem/local-admin сверх потребности: компрометация web-процесса = захват
   хоста и SQL.
5. **SEC-06 + SEC-09 (Medium)** — открытое firewall-правило (все профили/любой IP) + plaintext HTTP на всех
   интерфейсах без enforced TLS.

### (б) Вердикт: **ГОТОВО С ОГОВОРКАМИ**
**Blocker — 0.** Код эндпоинтов, авторизация и анти-инъекционная дисциплина — на хорошем уровне: карта авторизации
полная и непротиворечивая, командных/SQL-инъекций нет, секреты в API/логах/аудите не утекают, ошибки в prod
санитизированы. Единственный High (SEC-01) и большинство Medium — **исправимы малыми точечными правками** (подключить
`SecurityStampValidator`; `ProtectKeysWithDpapi`; ужесточить ACL на appsettings/keys в установщике; scope firewall;
header-middleware; rate-limiter на `/auth`). Профиль остаточных рисков смещён в **постустановочную конфигурацию хоста**
(привилегии службы, ACL, сеть), что согласуется с perimeter-моделью SECURITY.md, но недостаточно зафиксировано в ней
(SEC-DOC-1/2, GAP-1..4).

**Рекомендация куратору:** в pre-release обязательно закрыть **SEC-01** (High) и связку секретов **SEC-02/03/04**
(plaintext-пароль и незащищённый key ring — самый дешёвый путь к полной компрометации данных). Остальные Medium —
желательны до релиза, Low — бэклог hardening. Привести SECURITY.md в соответствие (SEC-DOC-1/2) и дополнить
пробелы GAP-1..4.

### (в) Что не успел покрыть / ограничения
- **Динамической проверки не было** (read-only статический аудит): фактическое поведение DataProtection под
  LocalSystem без user-profile (plaintext key ring или нет — SEC-03) и реальные ACL после установки в обоих режимах
  **не верифицированы на живом стенде** — выводы по коду установщика и документированному поведению фреймворка;
  рекомендуется подтвердить на стенде (прочитать `keys\*.xml` и `icacls` на appsettings/keys после install в режиме B).
- **Точный минимально-необходимый набор прав службы** (какие именно файлы `inetsrv\config` читает `ServerManager`,
  SEC-05) требует runtime-трассировки — не снят.
- **Аудит зависимостей на известные CVE** (NuGet/npm из A0 §2.5) — вне объёма А2 (профильный этап А-цепочки), здесь
  не проводился.
- **История git на утёкшие секреты** — вне объёма (A0 сканировал только HEAD трекаемых файлов).
- **Анти-CSRF при будущем добавлении CORS/поддоменов** — текущая безопасность SEC-10 опирается на строгий
  same-origin; при изменении топологии вывод по SEC-10 надо пересмотреть.

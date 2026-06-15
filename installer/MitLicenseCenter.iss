; MitLicense Center — установщик (Inno Setup 6, Unicode).
; Каркас (MLC-100): ставит файлы self-contained артефакта, регистрирует и стартует
; одну Windows-службу, открывает порт в firewall, умеет обновление поверх существующей
; установки (стоп службы -> подмена -> старт), сохраняя appsettings.Production.json,
; Data Protection key ring и БД.
;
; Защита секретов на диске (MLC-110): после раскладки файлов [Code] ужесточает NTFS ACL
; (icacls /inheritance:r — режем наследование от ProgramData/Program Files, где Users:RX):
;   - каталог %ProgramData%\MitLicenseCenter (Data Protection key ring + одноразовый
;     initial-admin.secret) — доступ только SYSTEM / Administrators + учётка службы (Modify);
;   - {app}\appsettings.Production.json — только SYSTEM / Administrators + учётка службы (Read).
; Key ring НАМЕРЕННО не шифруется at-rest (ADR-8): ключи переносимы, бэкап «key ring + БД»
; восстанавливается на новом железе/аккаунте; защита ключей — именно эти NTFS ACL.
; Сбой icacls предупреждает (с путём), но НЕ прерывает установку — служба работоспособна,
; страдает только hardening. ACL идемпотентны (накатываются и на чистой установке, и на апгрейде).
;
; Идентичность службы (MLC-170, ADR-49): служба работает под ВИРТУАЛЬНОЙ учётной записью
; NT SERVICE\MitLicenseCenter (по умолчанию) либо под именованной Windows-учёткой / gMSA
; (доменный сценарий). К ЛОКАЛЬНОМУ SQL служба ходит по доверенному подключению Windows
; (Trusted_Connection) — SQL-логин и пароль в appsettings.Production.json БОЛЬШЕ НЕ пишутся.
; SQL-аутентификация (прежний режим B / LocalSystem + SQL-логин) убрана. Установщик САМ создаёт
; SQL-логин учётки (CREATE LOGIN … FROM WINDOWS + sysadmin, идемпотентно). Назначить sysadmin
; может только sysadmin (правило SQL) — поэтому провижининг идёт под sysadmin-личностью НА ВЫБОР
; (MLC-171, страница «Подключение установщика к SQL»): по умолчанию Integrated Security
; запускающего админа (работает и на инстансах «только Windows-аутентификация»), либо введённый
; SQL-логин с ролью sysadmin (sa/иной; требует mixed-mode). SQL-логин ТРАНЗИЕНТЕН — используется
; разово, в appsettings.Production.json НЕ пишется (инвариант «секрета SQL на диске нет»). Учётка
; добавляется в локальную группу Администраторов (по SID S-1-5-32-544) для IIS/iisreset (ADR-44).
;
; Мастер (MLC-101/170/171): интерактивные страницы собирают SQL-инстанс/БД, личность подключения
; установщика к SQL (Integrated Security / SQL-логин), учётную запись службы (виртуальная ИЛИ
; именованная/gMSA), сетевые параметры (порт, AllowedHosts), проверяют достижимость SQL под
; выбранной личностью и генерируют рабочий appsettings.Production.json (всегда Trusted_Connection).
;
; Надёжность (MLC-107/170): создание/конфиг/старт службы, провижининг SQL-логина и firewall
; выполняются в [Code] (CurStepChanged ssPostInstall) с проверкой кода возврата — ошибка
; создания службы или провижининга SQL-логина прерывает установку с подсказкой и откатом, а не
; завершает её «успехом» без работоспособной службы. appsettings.Production.json удаляется при
; деинсталле и на чистой установке перезаписывается из ввода (skip-if-exists — только на апгрейде).
;
; Параметры передаёт scripts\build-installer.ps1:
;   /DMyAppVersion=<версия из backend\Directory.Build.props>
;   /DPublishDir=<каталог self-contained publish (artifacts\<version>\backend)>
;
; AppId зафиксирован — менять нельзя, по нему Inno детектит апгрейд поверх установки.

#ifndef MyAppVersion
  #define MyAppVersion "0.0.0-dev"
#endif

#ifndef PublishDir
  #error PublishDir is not defined. Pass /DPublishDir=<path-to-publish-output> (build-installer.ps1 does this).
#endif

#define MyAppName "MitLicense Center"
#define MyServiceName "MitLicenseCenter"
; Виртуальная учётная запись службы (ADR-49) — единый источник имени. SCM материализует её
; при sc create … obj= "NT SERVICE\MitLicenseCenter"; пароля нет (им управляет Windows).
#define MyVirtualAccount "NT SERVICE\" + MyServiceName
#define MyExeName "MitLicenseCenter.Web.exe"
#define MyFirewallRule "MitLicense Center"
#define MyDefaultPort "8080"

[Setup]
AppId={{B7E9F3A2-4C1D-4E8A-9F6B-2D5A8C3E1F40}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher=MitLicense Center
DefaultDirName={autopf}\MitLicense Center
DefaultGroupName=MitLicense Center
DisableProgramGroupPage=yes
PrivilegesRequired=admin
; Артефакт — win-x64 self-contained; ставим только в 64-битном режиме.
; Идентификатор x64compatible (предпочтительный в Inno 6.3+; x64 устарел).
ArchitecturesInstallIn64BitMode=x64compatible
ArchitecturesAllowed=x64compatible
OutputBaseFilename=MitLicenseCenter-Setup-{#MyAppVersion}
OutputDir=Output
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
UninstallDisplayName={#MyAppName}
UninstallDisplayIcon={app}\{#MyExeName}
; Лог установки во временный каталог (диагностика проблемных установок у оператора).
SetupLogging=yes

[Languages]
Name: "russian"; MessagesFile: "compiler:Languages\Russian.isl"

[Files]
; Self-contained артефакт целиком, КРОМЕ appsettings.Production.json — его генерирует
; [Code] из ввода мастера (ниже), чтобы апгрейд не затирал правки оператора, а чистая
; установка получила рабочую строку подключения и сетевые параметры.
; REL-08 (MLC-126): дополнительно исключаем *.pdb / appsettings.Development.json / web.config —
; defense-in-depth от information disclosure. Эти файлы уже подавлены/удалены в
; publish-release.ps1 и ловятся sanity-чеком build-installer.ps1, но Excludes гарантирует,
; что даже stale publish-каталог (сборка с -SkipPublish поверх старого артефакта) не утащит
; их в Setup.exe. Формат Excludes — список через запятую в двойных кавычках, поддерживает wildcard.
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: recursesubdirs createallsubdirs ignoreversion; Excludes: "appsettings.Production.json,*.pdb,appsettings.Development.json,web.config"

[Dirs]
; Data Protection key ring живёт здесь (purpose mlc.settings.v1) + одноразовый
; initial-admin.secret. Дефолтные ACL ProgramData (Users:RX) НЕДОСТАТОЧНЫ (MLC-110):
; [Code] (HardenDataDirAcl в ssPostInstall) режет наследование и оставляет доступ ТОЛЬКО
; SYSTEM / Administrators + учётка службы (Modify, ADR-49). Папку НЕ удаляем при деинсталле.
Name: "{commonappdata}\MitLicenseCenter"; Flags: uninsneveruninstall

[Run]
; Регистрация/конфиг/старт службы и открытие firewall-порта выполняются в [Code]
; (RegisterService/StartServiceAndFinalize в CurStepChanged(ssPostInstall)) — с проверкой кода
; возврата sc.exe и внятным сообщением при ошибке (MLC-107). Раньше эти шаги жили здесь, в [Run],
; и их провал проходил молча. Теперь create проверяется и прерывает установку с подсказкой,
; а старт — предупреждает и отсылает в Журнал событий.
; --- Опционально открыть панель в браузере (финальный чекбокс) ---
; URL формирует [Code] из введённого порта; postinstall — чекбокс на финальном экране,
; nowait — не ждём браузер, skipifsilent — в тихом режиме не открываем.
Filename: "{code:PanelUrl}"; Description: "Открыть панель в браузере"; \
  Flags: postinstall shellexec nowait skipifsilent

[UninstallRun]
; Стоп + удаление службы. RunOnceId, чтобы шаги не дублировались.
Filename: "{sys}\sc.exe"; Parameters: "stop {#MyServiceName}"; Flags: runhidden; RunOnceId: "StopSvc"
Filename: "{sys}\sc.exe"; Parameters: "delete {#MyServiceName}"; Flags: runhidden; RunOnceId: "DeleteSvc"
; Удаление firewall-правила (по имени — порт-независимо).
Filename: "{sys}\netsh.exe"; \
  Parameters: "advfirewall firewall delete rule name=""{#MyFirewallRule}"""; \
  Flags: runhidden; RunOnceId: "DeleteFwRule"
; БД установщик НЕ трогает (нужны creds, может быть ценной/общей — ручное удаление,
; см. OPERATIONS). Конфиг + key ring в {commonappdata}\MitLicenseCenter удаляются ТОЛЬКО
; по явному согласию оператора в keep-data prompt (CurUninstallStepChanged, ниже).

[InstallDelete]
; REL-13 (MLC-124): перед заменой файлов [Files] удаляем хэшированные SPA-ассеты предыдущей
; версии. Vite генерирует имена вида index-<hash>.js / style-<hash>.css; [Files] с ignoreversion
; кладёт новые поверх, но старые не удаляет — мусор накапливается, возможен «белый экран»
; при кэш-расхождении. Каталог после удаления воссоздаётся [Files] с новыми ассетами.
; На чистой установке секция безвредна: каталога {app}\wwwroot\assets ещё нет.
; Key ring + БД живут в {commonappdata}\MitLicenseCenter — не затрагиваем.
; appsettings.Production.json — в корне {app} — не затрагиваем.
Type: filesandordirs; Name: "{app}\wwwroot\assets\*"

[UninstallDelete]
; Сгенерированный в ssPostInstall интернет-ярлык + его каталог в меню «Пуск».
Type: filesandordirs; Name: "{commonprograms}\MitLicense Center"
; appsettings.Production.json генерится в [Code] (SaveStringToFile), а не секцией [Files],
; поэтому деинсталлятор сам его не удаляет — сносим явно, иначе при переустановке остаётся
; старый конфиг от прошлой установки (MLC-107). Key ring/БД здесь НЕ трогаем.
Type: files; Name: "{app}\appsettings.Production.json"

[Code]
const
  SERVICE_QUERY_STATUS = $0004;
  SERVICE_STOP         = $0020;        { access right для OpenService }
  SERVICE_CONTROL_STOP = $00000001;    { control-код для ControlService (MLC-116): НЕ путать с SERVICE_STOP — раньше в ControlService ошибочно передавался access right $0020, функция возвращала ERROR_INVALID_PARAMETER и служба НЕ останавливалась }
  SC_MANAGER_CONNECT   = $0001;
  SERVICE_STOPPED      = 1;
  SERVICE_STOP_PENDING = 3;

  { Учётная запись службы (индекс radio на странице «Учётная запись службы»), ADR-49. }
  ACCT_VIRTUAL = 0;  { Виртуальная учётка NT SERVICE\MitLicenseCenter (по умолчанию), без пароля. }
  ACCT_NAMED   = 1;  { Именованная Windows-учётка / gMSA (доменный сценарий). }

  { Под какой личностью УСТАНОВЩИК подключается к SQL для создания логина службы / теста
    (индекс radio на странице «Подключение установщика к SQL»), MLC-171. Ортогонально выбору
    учётки службы (ACCT_*) — создаётся ВСЕГДА Windows-логин учётки службы; это лишь личность,
    под которой выполняется провижининг. SQL-логин (sa) транзиентен — в конфиг не пишется. }
  PROV_INTEGRATED = 0;  { Integrated Security запускающего админа (по умолчанию). }
  PROV_SQLLOGIN   = 1;  { SQL-логин с ролью sysadmin (sa или иной) + пароль (mixed-mode). }

type
  TServiceStatus = record
    dwServiceType: Cardinal;
    dwCurrentState: Cardinal;
    dwControlsAccepted: Cardinal;
    dwWin32ExitCode: Cardinal;
    dwServiceSpecificExitCode: Cardinal;
    dwCheckPoint: Cardinal;
    dwWaitHint: Cardinal;
  end;

function OpenSCManager(lpMachineName, lpDatabaseName: string; dwDesiredAccess: Cardinal): THandle;
  external 'OpenSCManagerW@advapi32.dll stdcall';
function OpenService(hSCManager: THandle; lpServiceName: string; dwDesiredAccess: Cardinal): THandle;
  external 'OpenServiceW@advapi32.dll stdcall';
function ControlService(hService: THandle; dwControl: Cardinal; var lpServiceStatus: TServiceStatus): Boolean;
  external 'ControlService@advapi32.dll stdcall';
function QueryServiceStatus(hService: THandle; var lpServiceStatus: TServiceStatus): Boolean;
  external 'QueryServiceStatus@advapi32.dll stdcall';
function CloseServiceHandle(hSCObject: THandle): Boolean;
  external 'CloseServiceHandle@advapi32.dll stdcall';

var
  { Страница «SQL Server»: инстанс + БД. }
  PageSql: TInputQueryWizardPage;
  { Страница «Подключение установщика к SQL» (MLC-171): под какой личностью установщик ходит
    в SQL для создания логина службы и теста — Integrated Security (дефолт) либо SQL-логин с
    ролью sysadmin. Radio + (для SQL-логина) поля логина/пароля на поверхности страницы. }
  PageProv: TInputOptionWizardPage;
  { Поля SQL-логина провижининга (значимы только при PROV_SQLLOGIN), MLC-171. Логин и пароль
    кладём кастомными TNewEdit на поверхность PageProv (как GmsaCheckbox/TestButton на PageCreds),
    чтобы radio и поля жили на одной странице. Метки над полями — TNewStaticText. }
  ProvUserLabel: TNewStaticText;
  ProvUserEdit: TNewEdit;
  ProvPasswordLabel: TNewStaticText;
  { Маскированный ввод — TPasswordEdit (у TNewEdit нет свойства Password). }
  ProvPasswordEdit: TPasswordEdit;
  { Страница «Учётная запись службы»: виртуальная / именованная (gMSA) (ADR-49). }
  PageAuthMode: TInputOptionWizardPage;
  { Страница «Учётные данные» именованной учётки (только ACCT_NAMED). }
  PageCreds: TInputQueryWizardPage;
  { Чекбокс «это gMSA» на странице учётных данных: gMSA-учётка регистрируется БЕЗ пароля
    (явный выбор, не вывод из пустого пароля — иначе тихий провал sc create 1057). ADR-49. }
  GmsaCheckbox: TNewCheckBox;
  { Страница «Сеть»: порт + AllowedHosts. }
  PageNet: TInputQueryWizardPage;
  { Страница «Учётная запись администратора»: пароль admin + подтверждение (только чистая
    установка — на апгрейде admin уже создан, не спрашиваем). MLC-102. }
  PageAdmin: TInputQueryWizardPage;
  { Страница-предупреждение «сделайте бэкап БД + key ring» — показывается ТОЛЬКО на апгрейде
    (служба уже зарегистрирована), ДО замены файлов. Обязательна к подтверждению чекбоксом.
    На чистой установке пропускается (ShouldSkipPage). MLC-112 (REL-02). }
  PageUpgradeBackup: TInputOptionWizardPage;
  { Метка-результат теста подключения на странице учётных данных. }
  TestResultLabel: TNewStaticText;
  TestButton: TNewButton;
  { Прошёл ли последний тест подключения (гейт на Next со страницы учётных данных). }
  ConnTestPassed: Boolean;
  { Целевая БД уже содержит установку панели (есть пользователи auth.Users). Вычисляется
    один раз при уходе со страницы «Сеть» (NextButtonClick), см. DatabaseHasPanelUsers. На
    такой БД сидер не применит заданный пароль admin — страницу пароля пропускаем (MLC-105). }
  DbAlreadyInitialized: Boolean;
  { Показали ли уже информационное предупреждение про уже-инициализированную БД (один раз). }
  DbInitWarningShown: Boolean;
  { Старт службы провалился в StartServiceAndFinalize (sc start rc<>0). Финальный экран wpFinished
    при True рапортует ОШИБКУ, а не «успех» — провал старта не должен выглядеть как успех
    (MLC-107 показывал MsgBox-предупреждение, но финал всё равно рапортовал «запущена»).
    Инициализируется False в InitializeWizard. MLC-112 (REL-02). }
  ServiceStartFailed: Boolean;

{ ===== Хелперы доступа к вводу ===== }

{ Выбор учётной записи службы (ACCT_VIRTUAL / ACCT_NAMED), ADR-49. До построения мастера
  (PageAuthMode = nil) трактуем как виртуальную учётку — дефолт. }
function AccountMode: Integer;
begin
  if PageAuthMode = nil then
    Result := ACCT_VIRTUAL
  else
    Result := PageAuthMode.SelectedValueIndex;
end;

{ True, если на странице именованной учётки отмечен чекбокс «это gMSA» (учётка без пароля). }
function IsGmsa: Boolean;
begin
  Result := (GmsaCheckbox <> nil) and GmsaCheckbox.Checked;
end;

{ Личность подключения УСТАНОВЩИКА к SQL (PROV_INTEGRATED / PROV_SQLLOGIN), MLC-171. До
  построения мастера (PageProv = nil) трактуем как Integrated Security — дефолт. }
function ProvisioningMode: Integer;
begin
  if PageProv = nil then
    Result := PROV_INTEGRATED
  else
    Result := PageProv.SelectedValueIndex;
end;

{ SQL-логин провижининга (sa или иной sysadmin) — значим только при PROV_SQLLOGIN. Триммим:
  имя логина пробелов по краям не имеет. До построения полей (nil) — пусто. }
function ProvUser: string;
begin
  if ProvUserEdit = nil then
    Result := ''
  else
    Result := Trim(ProvUserEdit.Text);
end;

{ Пароль SQL-логина провижининга — значим только при PROV_SQLLOGIN. НЕ триммим (пробелы
  могут быть значимы). Транзиентен: в конфиг не пишется, нигде не сохраняется (ADR-49/MLC-171). }
function ProvPassword: string;
begin
  if ProvPasswordEdit = nil then
    Result := ''
  else
    Result := ProvPasswordEdit.Text;
end;

function SqlInstance: string;
begin
  Result := Trim(PageSql.Values[0]);
end;

function SqlDatabase: string;
begin
  Result := Trim(PageSql.Values[1]);
end;

function CredUser: string;
begin
  Result := Trim(PageCreds.Values[0]);
end;

function CredPassword: string;
begin
  { Пароль НЕ тримим — пробелы могут быть значимы. }
  Result := PageCreds.Values[1];
end;

{ Имя учётной записи службы (ADR-49): виртуальная → NT SERVICE\MitLicenseCenter; именованная
  → введённый CredUser (ДОМЕН\Пользователь, .\Пользователь или ДОМЕН\Имя$ для gMSA). }
function ServiceAccountName: string;
begin
  if AccountMode = ACCT_VIRTUAL then
    Result := '{#MyVirtualAccount}'
  else
    Result := CredUser;
end;

{ Нужен ли password= в sc create/config (ADR-49). Пароль есть ТОЛЬКО у обычной именованной
  Windows-учётки: режим ACCT_NAMED, НЕ gMSA (gMSA — без пароля), и пароль непустой.
  Виртуальная учётка и gMSA регистрируются без пароля (им управляет Windows). }
function ServiceAccountUsesPassword: Boolean;
begin
  Result := (AccountMode = ACCT_NAMED) and (not IsGmsa) and (CredPassword <> '');
end;

function NetPort: string;
begin
  Result := Trim(PageNet.Values[0]);
end;

function NetAllowedHosts: string;
begin
  Result := Trim(PageNet.Values[1]);
end;

{ Пароль admin (заданный оператором) — НЕ тримим: пробелы/спецсимволы значимы. }
function AdminPassword: string;
begin
  Result := PageAdmin.Values[0];
end;

function AdminPasswordConfirm: string;
begin
  Result := PageAdmin.Values[1];
end;

{ Проверка пароля admin по парольной политике Identity (паритет с RequiredLength=12 +
  Require Upper/Lower/Digit/NonAlphanumeric, см. backend AddInfrastructure / IdentitySeeder).
  Сидер всё равно перепроверит при создании (fail-fast), но мастер ловит ошибку заранее. }
function AdminPasswordMeetsPolicy(const pwd: string): Boolean;
var
  i: Integer;
  c: Char;
  hasUpper, hasLower, hasDigit, hasSpecial: Boolean;
begin
  hasUpper := False;
  hasLower := False;
  hasDigit := False;
  hasSpecial := False;
  for i := 1 to Length(pwd) do
  begin
    c := pwd[i];
    if (c >= 'A') and (c <= 'Z') then
      hasUpper := True
    else if (c >= 'a') and (c <= 'z') then
      hasLower := True
    else if (c >= '0') and (c <= '9') then
      hasDigit := True
    else
      hasSpecial := True;  { всё прочее — спецсимвол (NonAlphanumeric) }
  end;
  Result := (Length(pwd) >= 12) and hasUpper and hasLower and hasDigit and hasSpecial;
end;

{ ===== Служебные ===== }

{ Возвращает True, если служба {#MyServiceName} зарегистрирована (есть = апгрейд). }
function ServiceExists: Boolean;
var
  hSCM, hSvc: THandle;
begin
  Result := False;
  hSCM := OpenSCManager('', '', SC_MANAGER_CONNECT);
  if hSCM = 0 then
    Exit;
  try
    hSvc := OpenService(hSCM, '{#MyServiceName}', SERVICE_QUERY_STATUS);
    if hSvc <> 0 then
    begin
      Result := True;
      CloseServiceHandle(hSvc);
    end;
  finally
    CloseServiceHandle(hSCM);
  end;
end;

{ Останавливает службу и ждёт фактической остановки, иначе exe залочен и [Files]
  не сможет подменить его при апгрейде. }
procedure StopServiceAndWait;
var
  hSCM, hSvc: THandle;
  status: TServiceStatus;
  i: Integer;
begin
  hSCM := OpenSCManager('', '', SC_MANAGER_CONNECT);
  if hSCM = 0 then
    Exit;
  try
    hSvc := OpenService(hSCM, '{#MyServiceName}', SERVICE_QUERY_STATUS or SERVICE_STOP);
    if hSvc = 0 then
      Exit;
    try
      if QueryServiceStatus(hSvc, status) then
      begin
        if status.dwCurrentState <> SERVICE_STOPPED then
          { Control-код SERVICE_CONTROL_STOP ($1), НЕ access right SERVICE_STOP ($20) (MLC-116):
            иначе ControlService возвращает ERROR_INVALID_PARAMETER и служба не останавливается,
            exe остаётся залочен -> экран restart-manager «файлы заняты» на апгрейде. }
          ControlService(hSvc, SERVICE_CONTROL_STOP, status);
        { Ждём остановки до ~30 с (60 * 500 мс). }
        for i := 1 to 60 do
        begin
          if not QueryServiceStatus(hSvc, status) then
            Break;
          if status.dwCurrentState = SERVICE_STOPPED then
            Break;
          Sleep(500);
        end;
      end;
    finally
      CloseServiceHandle(hSvc);
    end;
  finally
    CloseServiceHandle(hSCM);
  end;
end;

{ Перед копированием файлов: на апгрейде остановить службу (снять лок с exe). }
function PrepareToInstall(var NeedsRestart: Boolean): String;
begin
  Result := '';
  if ServiceExists then
    StopServiceAndWait;
end;

{ ===== Экранирование ===== }

{ Экранирует значение для подстановки внутрь двойных кавычек JSON: бэкслеши (пути/инстансы
  типа .\SQLEXPRESS) и двойные кавычки. }
function JsonEscape(const S: string): string;
begin
  Result := S;
  StringChangeEx(Result, '\', '\\', True);
  StringChangeEx(Result, '"', '\"', True);
end;

{ Экранирует значение для подстановки внутрь двойных кавычек в командной строке sc/netsh/
  icacls (CreateProcess): внутренняя двойная кавычка удваивается, чтобы кавычка в SQL-пароле
  или имени аккаунта не разорвала команду. }
function CmdQuoteInner(const S: string): string;
begin
  Result := S;
  StringChangeEx(Result, '"', '""', True);
end;

{ Экранирует значение для PowerShell single-quoted literal ('...'): одиночная кавычка
  удваивается. Строку подключения вставляем в '...'-литерал во временном .ps1, чтобы
  пароль не попал в командную строку powershell.exe (нет SetEnvironmentVariable в Pascal
  Script). Временный скрипт удаляется сразу после запуска. }
function PsSingleQuote(const S: string): string;
begin
  Result := S;
  StringChangeEx(Result, '''', '''''', True);
end;

{ ===== URL панели (для финального экрана / открытия в браузере) ===== }

{ URL панели для оператора: http://localhost:<порт>/. Хост — localhost (служба слушит
  http://+:<порт>, локальное имя всегда достижимо с самого хоста). MLC-102. }
function PanelUrl(Param: string): string;
begin
  Result := 'http://localhost:' + NetPort + '/';
end;

{ ===== Строка подключения ===== }

{ Собирает строку подключения. appName уходит в Application Name (Default/Hangfire).
  ADR-49: ВСЕГДА Trusted_Connection — служба ходит к локальному SQL по доверенному подключению
  Windows под своей учёткой (виртуальной / именованной / gMSA). SQL-логин и пароль в конфиг
  больше НЕ пишутся — это и есть главный выигрыш (секрет SQL убран с диска). }
function BuildConnString(const appName: string): string;
begin
  Result := 'Server=' + SqlInstance + ';Database=' + SqlDatabase + ';' +
            'Trusted_Connection=True;' +
            'Encrypt=True;TrustServerCertificate=True;Application Name=' + appName;
end;

{ Строка подключения, которой УСТАНОВЩИК ходит в SQL для теста / создания логина службы
  (MLC-171). НЕ путать с BuildConnString (runtime-строка службы — всегда Trusted_Connection).
  Ветвится по ProvisioningMode:
    PROV_INTEGRATED → Integrated Security запускающего админа (без ввода; работает и на
      инстансах «только Windows-аутентификация»);
    PROV_SQLLOGIN   → введённый SQL-логин с ролью sysadmin (sa/иной) + пароль (mixed-mode).
  Эта строка используется одинаково в TestSqlConnection / DatabaseHasPanelUsers /
  ProvisionSqlLogin — тестируем тем же, чем создаём. Пароль SQL-логина уходит в строку, а та
  всегда подставляется в '...'-литерал временного .ps1 (PsSingleQuote) — в командную строку
  powershell.exe не попадает (тот же контракт, что у Integrated-варианта раньше). Транзиентен:
  в appsettings.Production.json не пишется (инвариант «секрета SQL на диске нет», ADR-49). }
function ProvisioningConnString(const database: string): string;
begin
  Result := 'Server=' + SqlInstance + ';Database=' + database + ';';
  if ProvisioningMode = PROV_SQLLOGIN then
    Result := Result + 'User Id=' + ProvUser + ';Password=' + ProvPassword + ';'
  else
    Result := Result + 'Integrated Security=True;';
  Result := Result + 'Encrypt=True;TrustServerCertificate=True;Connect Timeout=15';
end;

{ ===== Генерация appsettings.Production.json ===== }

procedure WriteProductionConfig;
var
  path, json, urls: string;
begin
  path := ExpandConstant('{app}\appsettings.Production.json');
  { Skip-if-exists применяем ТОЛЬКО на апгрейде (ServiceExists): НЕ затирать правки оператора
    (паритет с MLC-100 onlyifdoesntexist). На ЧИСТОЙ установке (службы ещё нет) пишем конфиг
    ВСЕГДА, перезаписывая возможный остаточный файл от прошлой/прерванной установки — иначе
    ввод мастера молча игнорируется и залипает старый конфиг (MLC-107). }
  if ServiceExists and FileExists(path) then
    Exit;
  urls := 'http://+:' + NetPort;
  json :=
    '{' + #13#10 +
    '  "ConnectionStrings": {' + #13#10 +
    '    "Default": "' + JsonEscape(BuildConnString('MitLicenseCenter')) + '",' + #13#10 +
    '    "Hangfire": "' + JsonEscape(BuildConnString('MitLicenseCenter.Hangfire')) + '"' + #13#10 +
    '  },' + #13#10 +
    '  "Urls": "' + JsonEscape(urls) + '",' + #13#10 +
    '  "Security": {' + #13#10 +
    '    "EnforceHttps": false,' + #13#10 +
    '    "EnableSwagger": false' + #13#10 +
    '  },' + #13#10 +
    '  "AllowedHosts": "' + JsonEscape(NetAllowedHosts) + '"' + #13#10 +
    '}' + #13#10;
  { ASCII-ключи; значения операторские. UTF-8 без BOM. }
  if not SaveStringToFile(path, json, False) then
    MsgBox('Не удалось записать appsettings.Production.json по пути ' + path + '.' + #13#10 +
           'Служба может не стартовать — проверьте конфигурацию вручную.', mbError, MB_OK);
end;

{ ===== Одноразовый файл пароля admin (MLC-102) ===== }

{ На чистой установке кладём заданный оператором пароль admin в одноразовый файл
  initial-admin.secret в каталоге commonappdata\MitLicenseCenter. Сидер бэкенда при первом
  старте читает его, создаёт admin с этим паролем и УДАЛЯЕТ файл (ADR-31). На апгрейде admin
  уже есть — файл не пишем (страница пароля пропущена через ShouldSkipPage). Каталог создаётся
  секцией Dirs; его ACL ужесточает HardenDataDirAcl ПОСЛЕ записи этого файла (MLC-110): доступ
  только SYSTEM/Administrators + учётка службы (Modify, ADR-49), Users отрезаны.
  UTF-8 без BOM (SaveStringToFile с UTF8=False). Пароль не логируется. }
procedure WriteInitialAdminPassword;
var
  path: string;
begin
  { Не пишем .secret, если применять некуда: апгрейд (admin уже создан) ИЛИ чистая установка
    поверх уже-инициализированной БД (DbAlreadyInitialized — страница пароля была пропущена,
    сидер не применит файл). MLC-102/105. }
  if ServiceExists or DbAlreadyInitialized then
    Exit;
  path := ExpandConstant('{commonappdata}\MitLicenseCenter\initial-admin.secret');
  if not SaveStringToFile(path, AdminPassword, False) then
    MsgBox('Не удалось записать файл с паролем администратора по пути ' + path + '.' + #13#10 +
           'Первый администратор будет создан со случайным паролем — он попадёт в Журнал событий Windows.',
           mbError, MB_OK);
end;

{ ===== Ярлык меню «Пуск» (интернет-ярлык на URL панели) ===== }

{ Создаёт commonprograms\MitLicense Center\MitLicense Center.url — интернет-ярлык,
  открывающий панель в браузере по умолчанию. Inno-секция Icons не умеет .url с
  динамическим URL (порт из мастера), поэтому пишем .url вручную через SaveStringToFile.
  Каталог + ярлык сносятся при деинсталляции секцией UninstallDelete. }
procedure CreateStartMenuShortcut;
var
  dir, path, content: string;
begin
  dir := ExpandConstant('{commonprograms}\MitLicense Center');
  if not DirExists(dir) then
    ForceDirectories(dir);
  path := dir + '\MitLicense Center.url';
  content := '[InternetShortcut]' + #13#10 + 'URL=' + PanelUrl('') + #13#10;
  if not SaveStringToFile(path, content, False) then
    { Не критично — установка состоялась, ярлык лишь удобство. }
    Log('Не удалось создать ярлык меню «Пуск»: ' + path);
end;

{ ===== Ужесточение NTFS ACL на секреты на диске (MLC-110) ===== }

{ Запуск icacls с проверкой rc. На сбое — предупреждение (с целью и подсказкой), но БЕЗ
  прерывания установки: служба работоспособна, страдает только hardening. Каждый шаг логируется.
  Используем SID-формы локаленезависимо (на RU Windows «Администраторы», не «Administrators»):
    *S-1-5-18      = NT AUTHORITY\SYSTEM
    *S-1-5-32-544  = BUILTIN\Administrators }
procedure RunIcacls(const target, args, what: string);
var
  rc: Integer;
begin
  Log('icacls (' + what + '): "' + target + '" ' + args);
  if (not Exec(ExpandConstant('{sys}\icacls.exe'),
               '"' + target + '" ' + args,
               '', SW_HIDE, ewWaitUntilTerminated, rc)) or (rc <> 0) then
  begin
    Log('icacls (' + what + ') завершился с кодом ' + IntToStr(rc));
    MsgBox('Не удалось ужесточить права доступа (NTFS ACL) для:' + #13#10 +
           target + #13#10#13#10 +
           'Установка продолжится — служба работоспособна, но защита секретов на диске не ' +
           'применена. Выполните ужесточение вручную (icacls) или проверьте лог установки ' +
           '(код ' + IntToStr(rc) + ').',
           mbError, MB_OK);
  end;
end;

{ ACL каталога %ProgramData%\MitLicenseCenter (key ring + initial-admin.secret).
  Режем наследование (Users:RX от ProgramData) и оставляем доступ только SYSTEM /
  Administrators + учётка службы (Modify). Грант учётке БЕЗУСЛОВНЫЙ (ADR-49: учётка есть в
  обоих режимах — виртуальная или именованная). Вызывается ПОСЛЕ записи initial-admin.secret
  (чтобы (OI)(CI) накрыл и его) и ПОСЛЕ sc create (чтобы имя «NT SERVICE\…» резолвилось —
  иначе Data Protection не прочитает ключи на первом старте). Идемпотентно. }
procedure HardenDataDirAcl;
var
  dataDir, args: string;
begin
  dataDir := ExpandConstant('{commonappdata}\MitLicenseCenter');
  if not DirExists(dataDir) then
    Exit;
  { /inheritance:r снимает унаследованные ACE; затем явные full для SYSTEM/Administrators,
    наследуемые на под-объекты/контейнеры (OI)(CI). Учётке службы — Modify. icacls по имени
    «NT SERVICE\MitLicenseCenter» надёжен и локаленезависим (префикс NT SERVICE\ не локализуется). }
  args := '/inheritance:r' +
          ' /grant *S-1-5-18:(OI)(CI)F' +
          ' /grant *S-1-5-32-544:(OI)(CI)F' +
          ' /grant "' + CmdQuoteInner(ServiceAccountName) + '":(OI)(CI)M';
  RunIcacls(dataDir, args, 'каталог данных');

  { Право «Log on as a service» (SeServiceLogonRight) SCM выдаёт сам при sc create/config
    obj=… на валидном аккаунте; явная выдача через secedit здесь не делается (хрупко на разных
    локалях). Если SCM не сможет — служба не стартует, оператор выдаёт право через secpol.msc
    (подсказано в OPERATIONS). }
end;

{ ACL файла appsettings.Production.json в каталоге установки. Режем наследование (Users:RX от
  Program Files) — только SYSTEM / Administrators full + учётка службы Read. Грант учётке
  БЕЗУСЛОВНЫЙ (ADR-49). Вызывается ПОСЛЕ WriteProductionConfig и ПОСЛЕ sc create (имя
  «NT SERVICE\…» резолвится). Применять и на апгрейде: файл сохраняется от прошлой установки и
  не перезаписывается, но ACL всё равно накатываем. }
procedure HardenConfigAcl;
var
  cfg, args: string;
begin
  cfg := ExpandConstant('{app}\appsettings.Production.json');
  if not FileExists(cfg) then
    Exit;
  args := '/inheritance:r' +
          ' /grant *S-1-5-18:F' +
          ' /grant *S-1-5-32-544:F' +
          ' /grant "' + CmdQuoteInner(ServiceAccountName) + '":R';
  RunIcacls(cfg, args, 'конфиг');
end;

{ ===== Параметры для [Run] (sc/netsh) ===== }

{ REL-03 (ADR-40): выводит имя ЛОКАЛЬНОЙ SQL-службы из выбранного инстанса для
  производной SCM-зависимости. Возвращает '' (пусто), если инстанс не распознан как
  однозначно локальный — тогда зависимость НЕ ставится (полагаемся на recovery-политику).
  Правила (консервативные — НЕ угадываем):
    .  / localhost / (local) / .\           -> MSSQLSERVER        (дефолтный локальный)
    .\NAME / localhost\NAME / (local)\NAME   -> MSSQL$NAME          (именованный локальный)
    SERVER\NAME / SERVER / host,port / …     -> ''                 (возможно удалённый — пропуск)
  Жёсткий depend= MSSQLSERVER НЕ используем: он неверен для именованных инстансов и для
  удалённого SQL (там локальной службы нет, и служба не стартовала бы). }
function DeriveLocalSqlServiceName(const instance: string): string;
var
  s, hostPart, namePart: string;
  bs: Integer;
begin
  Result := '';
  s := Trim(instance);
  if s = '' then
    Exit;
  { Запятая = host,port — это сетевой адрес, локальным не считаем. }
  if Pos(',', s) > 0 then
    Exit;

  bs := Pos('\', s);
  if bs = 0 then
  begin
    { Без '\' — либо локальный дефолтный маркер, либо голое имя хоста (возможно удалённое). }
    if (CompareText(s, '.') = 0) or (CompareText(s, 'localhost') = 0) or
       (CompareText(s, '(local)') = 0) then
      Result := 'MSSQLSERVER';
    { Голое 'SERVER' (имя машины) — не угадываем локальность, пропускаем. }
    Exit;
  end;

  hostPart := Copy(s, 1, bs - 1);
  namePart := Copy(s, bs + 1, Length(s) - bs);

  { Хост-часть должна быть однозначно локальной. }
  if not ((CompareText(hostPart, '.') = 0) or (CompareText(hostPart, 'localhost') = 0) or
          (CompareText(hostPart, '(local)') = 0)) then
    Exit; { SERVER\NAME — возможно удалённый, пропускаем. }

  if namePart = '' then
    Result := 'MSSQLSERVER'  { форма '.\' — дефолтный инстанс }
  else if CompareText(namePart, 'MSSQLSERVER') = 0 then
    Result := 'MSSQLSERVER'  { .\MSSQLSERVER == дефолтный }
  else
    Result := 'MSSQL$' + namePart;  { .\SQLEXPRESS -> MSSQL$SQLEXPRESS }
end;

{ sc create … — на чистой установке (ADR-49). ВСЕГДА obj= <учётка службы>; password= только
  для обычной именованной Windows-учётки (ServiceAccountUsesPassword). Для виртуальной учётки
  «NT SERVICE\MitLicenseCenter» и для gMSA пароль НЕ передаётся (им управляет Windows; SCM сам
  материализует учётку/SID и выдаёт SeServiceLogonRight). }
function GetScCreateParams(Param: string): string;
begin
  Result := 'create {#MyServiceName} binPath= "' +
            ExpandConstant('{app}\{#MyExeName}') + '" start= auto DisplayName= "{#MyAppName}"' +
            ' obj= "' + CmdQuoteInner(ServiceAccountName) + '"';
  if ServiceAccountUsesPassword then
    Result := Result + ' password= "' + CmdQuoteInner(CredPassword) + '"';
end;

{ sc config … — на апгрейде: выравниваем binPath + учётку под текущий выбор (ADR-49).
  ВСЕГДА obj= <учётка службы>; password= только для именованной не-gMSA учётки с паролем. }
function GetScConfigParams(Param: string): string;
begin
  Result := 'config {#MyServiceName} binPath= "' +
            ExpandConstant('{app}\{#MyExeName}') + '" start= auto' +
            ' obj= "' + CmdQuoteInner(ServiceAccountName) + '"';
  if ServiceAccountUsesPassword then
    Result := Result + ' password= "' + CmdQuoteInner(CredPassword) + '"';
end;

{ netsh … add rule — порт из ввода. Старое одноимённое правило снимается в CurStepChanged
  (ssPostInstall, шаг 3) ДО этого вызова (идемпотентность при смене порта).
  SEC-06 (MLC-126): правило открывается ТОЛЬКО на профилях Domain/Private (штатный LAN-сценарий),
  НЕ на Public (недоверенные сети) — порт остаётся закрыт на публичных подключениях, без регресса
  для домена/частной сети. remoteip= намеренно НЕ задаём: localsubnet сломал бы LAN с несколькими
  подсетями; сужение по источнику документируется как опция в OPERATIONS (SEC-09). }
function GetFirewallAddParams(Param: string): string;
begin
  Result := 'advfirewall firewall add rule name="{#MyFirewallRule}"' +
            ' dir=in action=allow protocol=TCP localport=' + NetPort +
            ' profile=domain,private';
end;

{ REL-03 (ADR-40): recovery-политика SCM — ОСНОВНОЙ механизм устойчивости, не зависит от
  расположения SQL. После сбоя (типично: fail-fast bootstrap ADR-18 при ещё не поднятом
  после перезагрузки SQL) SCM перезапускает службу с задержкой. Best-effort: rc<>0 ->
  предупреждение + Log, без Abort (hardening, не гейт). Синтаксис sc как у binPath=/start=:
  значимые пробелы после 'failure'/'reset='/'actions='. reset= 86400 — счётчик сбоев
  сбрасывается раз в сутки; три перезапуска по 30 с. }
procedure ConfigureServiceRecovery;
var
  rc: Integer;
begin
  if (not Exec(ExpandConstant('{sys}\sc.exe'),
               'failure {#MyServiceName} reset= 86400 actions= restart/30000/restart/30000/restart/30000',
               '', SW_HIDE, ewWaitUntilTerminated, rc)) or (rc <> 0) then
  begin
    Log('sc failure ({#MyServiceName}) завершился с кодом ' + IntToStr(rc));
    MsgBox('Не удалось задать политику авто-перезапуска службы «{#MyServiceName}» (код ' + IntToStr(rc) + ').' + #13#10 +
           'Установка продолжится — служба работоспособна, но не будет сама перезапускаться после сбоя. ' +
           'Настройте вручную: services.msc → свойства службы → вкладка «Восстановление» ' +
           '(или sc failure). Подробности — в логе установки.',
           mbError, MB_OK);
  end;
end;

{ REL-03 (ADR-40): производная SCM-зависимость от ЛОКАЛЬНОЙ SQL-службы — best-effort,
  дополнение к recovery-политике. Имя выводится из инстанса (DeriveLocalSqlServiceName);
  если инстанс не распознан как однозначно локальный (удалённый SQL / неоднозначный
  SERVER\NAME) — зависимость НЕ ставится (пусто), полагаемся на recovery. Применяется
  отдельным sc config depend= (не сворачиваем в create/config выше, чтобы пустой случай
  не трогал службу). Best-effort: rc<>0 -> предупреждение + Log, без Abort. }
procedure ConfigureSqlDependency;
var
  svc: string;
  rc: Integer;
begin
  svc := DeriveLocalSqlServiceName(SqlInstance);
  if svc = '' then
  begin
    Log('SQL-зависимость не задаётся: инстанс «' + SqlInstance + '» не распознан как локальный ' +
        '(удалённый или неоднозначный) — устойчивость обеспечивает recovery-политика.');
    Exit;
  end;

  Log('Задаю зависимость службы {#MyServiceName} от локальной SQL-службы «' + svc + '».');
  if (not Exec(ExpandConstant('{sys}\sc.exe'),
               'config {#MyServiceName} depend= ' + svc,
               '', SW_HIDE, ewWaitUntilTerminated, rc)) or (rc <> 0) then
  begin
    Log('sc config depend= ' + svc + ' завершился с кодом ' + IntToStr(rc));
    MsgBox('Не удалось задать зависимость службы «{#MyServiceName}» от SQL-службы «' + svc + '» (код ' + IntToStr(rc) + ').' + #13#10 +
           'Установка продолжится — служба работоспособна; при медленном старте SQL после ' +
           'перезагрузки её перезапустит политика восстановления. Подробности — в логе установки.',
           mbError, MB_OK);
  end;
end;

{ ===== Авто-создание SQL-логина учётки службы (MLC-170, ADR-49) ===== }

{ Виртуальная учётка не существует до sc create, а её SQL-логин создать заранее оператор не
  может (SID появляется только при регистрации службы). Поэтому установщик САМ создаёт логин
  под Integrated Security запускающего администратора (предусловие: он sysadmin на локальном
  SQL). Тем же приёмом, что DatabaseHasPanelUsers: временный .ps1, connstr к master в
  '...'-литерале, имя учётки в '...'-литерале (PsSingleQuote) → в T-SQL N'...'. Идемпотентно:
  CREATE LOGIN [acct] FROM WINDOWS только если логина нет; ALTER SERVER ROLE sysadmin ADD
  MEMBER только если ещё не член (ALTER SERVER ROLE не принимает переменную → через sp_executesql
  + QUOTENAME). Контракт ошибок — ЖЁСТКИЙ FAIL: rc<>0 -> MsgBox(mbCriticalError) + RaiseException
  (откат), как у sc create: иначе служба создастся и упадёт на старте с 18456 (молчаливый провал,
  который закрывал MLC-107). }
procedure ProvisionSqlLogin;
var
  connStr, acct, psScript, scriptPath, cmdLine: string;
  rc: Integer;
begin
  acct := ServiceAccountName;
  { connstr к master под выбранной личностью провижининга (MLC-171): Integrated Security
    запускающего админа (дефолт) либо введённый SQL-логин с ролью sysadmin. SQL-логин
    транзиентен — в строке только для этого разового вызова, в конфиг не пишется. }
  connStr := ProvisioningConnString('master');

  { Имя учётки уходит в SqlParameter @p_acct (ADO.NET), внутри T-SQL присваивается в @acct и
    подставляется через sp_executesql + QUOTENAME (DDL не принимает переменную напрямую — нельзя
    параметризовать CREATE LOGIN / ALTER SERVER ROLE). Имя учётки в командную строку НЕ попадает.
    T-SQL — одной PowerShell '...'-строкой (как в DatabaseHasPanelUsers): одиночные кавычки T-SQL
    удвоены до '' (для PowerShell-литерала); чтобы получить N'...'-строку внутри sp_executesql,
    исходная T-SQL-кавычка '...' пишется как '''' в PS-литерале. }
  psScript :=
    '$ErrorActionPreference=''Stop'';' + #13#10 +
    '$cs = ''' + PsSingleQuote(connStr) + ''';' + #13#10 +
    '$acct = ''' + PsSingleQuote(acct) + ''';' + #13#10 +
    '$tsql = ''DECLARE @acct sysname = @p_acct;' +
      ' IF NOT EXISTS (SELECT 1 FROM sys.server_principals WHERE name = @acct)' +
      ' EXEC sys.sp_executesql N''''CREATE LOGIN ''''+QUOTENAME(@acct)+N'''' FROM WINDOWS;'''';' +
      ' IF NOT EXISTS (SELECT 1 FROM sys.server_role_members rm' +
      ' JOIN sys.server_principals r ON r.principal_id=rm.role_principal_id AND r.name=N''''sysadmin''''' +
      ' JOIN sys.server_principals m ON m.principal_id=rm.member_principal_id AND m.name=@acct)' +
      ' EXEC sys.sp_executesql N''''ALTER SERVER ROLE sysadmin ADD MEMBER ''''+QUOTENAME(@acct)+N'''';'''';'';' + #13#10 +
    'try {' + #13#10 +
    '  $c = New-Object System.Data.SqlClient.SqlConnection $cs;' + #13#10 +
    '  $c.Open();' + #13#10 +
    '  $cmd = $c.CreateCommand();' + #13#10 +
    '  $cmd.CommandText = $tsql;' + #13#10 +
    '  [void]$cmd.Parameters.AddWithValue(''@p_acct'', $acct);' + #13#10 +
    '  [void]$cmd.ExecuteNonQuery();' + #13#10 +
    '  $c.Close();' + #13#10 +
    '  exit 0;' + #13#10 +
    '} catch {' + #13#10 +
    '  [Console]::Error.WriteLine($_.Exception.Message);' + #13#10 +
    '  exit 1;' + #13#10 +
    '}' + #13#10;

  scriptPath := ExpandConstant('{tmp}\mlc-provision-login.ps1');
  if not SaveStringToFile(scriptPath, psScript, False) then
  begin
    MsgBox('Не удалось подготовить временный скрипт создания SQL-логина учётной записи службы.' + #13#10 +
           'Установка прервана. Проверьте права на временный каталог и повторите.',
           mbCriticalError, MB_OK);
    RaiseException('Не удалось записать временный скрипт ProvisionSqlLogin.');
  end;

  cmdLine := '-NoProfile -ExecutionPolicy Bypass -File "' + scriptPath + '"';
  if (not Exec(ExpandConstant('{sys}\WindowsPowerShell\v1.0\powershell.exe'),
               cmdLine, '', SW_HIDE, ewWaitUntilTerminated, rc)) or (rc <> 0) then
  begin
    DeleteFile(scriptPath);
    Log('ProvisionSqlLogin: создание SQL-логина «' + acct + '» завершилось с кодом ' + IntToStr(rc));
    if ProvisioningMode = PROV_SQLLOGIN then
      { Провижининг под введённым SQL-логином: причины — неверный логин/пароль, у логина нет
        роли sysadmin, либо инстанс не в смешанном режиме (SQL-аутентификация запрещена). }
      MsgBox('Не удалось создать SQL-логин для учётной записи службы:' + #13#10 +
             acct + #13#10#13#10 +
             'Установщик создаёт логин и назначает роль sysadmin под указанным SQL-логином ' +
             '(страница «Подключение установщика к SQL»). Чаще всего ошибка значит одно из:' + #13#10 +
             '• неверное имя SQL-логина или пароль;' + #13#10 +
             '• у этого SQL-логина НЕТ роли sysadmin на экземпляре ' + SqlInstance + ';' + #13#10 +
             '• экземпляр SQL Server работает только в режиме Windows-аутентификации ' +
             '(SQL-логины запрещены) — тогда выберите вариант «Integrated Security».' + #13#10#13#10 +
             'Исправьте данные на странице «Подключение установщика к SQL» и повторите. ' +
             'Подробности — в логе установки (код ' + IntToStr(rc) + ').',
             mbCriticalError, MB_OK)
    else
      { Провижининг под Integrated Security: типичная причина — запускающий админ не sysadmin. }
      MsgBox('Не удалось создать SQL-логин для учётной записи службы:' + #13#10 +
             acct + #13#10#13#10 +
             'Установщик создаёт логин и назначает роль sysadmin под учётной записью администратора, ' +
             'запустившего установку (Integrated Security). Чаще всего ошибка значит, что эта учётная ' +
             'запись НЕ имеет роли sysadmin на локальном экземпляре SQL Server (' + SqlInstance + ').' + #13#10#13#10 +
             'Запустите установщик от имени Windows-администратора, который одновременно является ' +
             'sysadmin на этом экземпляре SQL, ИЛИ на странице «Подключение установщика к SQL» ' +
             'укажите SQL-логин с ролью sysadmin (для инстансов в смешанном режиме). Подробности — ' +
             'в логе установки (код ' + IntToStr(rc) + ').',
             mbCriticalError, MB_OK);
    { Жёсткий fail/откат: без логина служба упадёт на старте с 18456 — не завершаем «успехом». }
    RaiseException('Создание SQL-логина учётной записи службы завершилось с кодом ' + IntToStr(rc) + '.');
  end;
  DeleteFile(scriptPath);
  Log('ProvisionSqlLogin: SQL-логин «' + acct + '» создан/подтверждён (sysadmin).');
end;

{ ===== Добавление учётки службы в локальную группу Администраторов (MLC-170, ADR-49) ===== }

{ Для IIS/iisreset/публикаций (ADR-44) учётке нужен admin-эквивалент на хосте. У LocalSystem
  это было даром; виртуальную/именованную учётку добавляем явно. Локаленезависимо — по
  well-known SID S-1-5-32-544 (на RU-Windows группа называется «Администраторы», поэтому
  net localgroup Administrators непригоден). Add-LocalGroupMember (модуль LocalAccounts, есть
  в PowerShell 5.1 на Windows Server 2016+). Идемпотентно: «уже член» (ошибка PrincipalExists)
  трактуем как успех. Контракт — BEST-EFFORT: на прочих сбоях предупреждаем + Log, без Abort
  (без локального админа панель и SQL работают, деградируют только IIS-функции). }
procedure AddServiceAccountToAdministrators;
var
  acct, psScript, scriptPath, cmdLine: string;
  rc: Integer;
begin
  acct := ServiceAccountName;
  { Add-LocalGroupMember бросает, если член уже есть — глотаем именно этот случай (идемпотентность),
    остальные исключения пробрасываем (exit 1). Имя учётки — в '...'-литерале (PsSingleQuote). }
  psScript :=
    '$ErrorActionPreference=''Stop'';' + #13#10 +
    '$acct = ''' + PsSingleQuote(acct) + ''';' + #13#10 +
    'try {' + #13#10 +
    '  Add-LocalGroupMember -SID ''S-1-5-32-544'' -Member $acct;' + #13#10 +
    '  exit 0;' + #13#10 +
    '} catch [Microsoft.PowerShell.Commands.MemberExistsException] {' + #13#10 +
    '  exit 0;' + #13#10 +
    '} catch {' + #13#10 +
    '  if ($_.Exception.Message -match ''уже|already'') { exit 0 }' + #13#10 +
    '  [Console]::Error.WriteLine($_.Exception.Message);' + #13#10 +
    '  exit 1;' + #13#10 +
    '}' + #13#10;

  scriptPath := ExpandConstant('{tmp}\mlc-add-admin.ps1');
  if not SaveStringToFile(scriptPath, psScript, False) then
  begin
    Log('AddServiceAccountToAdministrators: не удалось записать временный скрипт — пропуск (best-effort).');
    Exit;
  end;

  cmdLine := '-NoProfile -ExecutionPolicy Bypass -File "' + scriptPath + '"';
  if (not Exec(ExpandConstant('{sys}\WindowsPowerShell\v1.0\powershell.exe'),
               cmdLine, '', SW_HIDE, ewWaitUntilTerminated, rc)) or (rc <> 0) then
  begin
    DeleteFile(scriptPath);
    Log('AddServiceAccountToAdministrators: добавление «' + acct + '» в Администраторов завершилось с кодом ' + IntToStr(rc));
    MsgBox('Не удалось добавить учётную запись службы в локальную группу «Администраторы»:' + #13#10 +
           acct + #13#10#13#10 +
           'Установка продолжится — панель и подключение к SQL работают. Однако функции IIS ' +
           '(публикация, recycle, iisreset) могут не работать без прав администратора у учётной ' +
           'записи службы. Добавьте её в группу вручную (lusrmgr.msc или ' +
           'Add-LocalGroupMember -SID S-1-5-32-544) либо проверьте лог установки.',
           mbError, MB_OK);
    Exit;
  end;
  DeleteFile(scriptPath);
  Log('AddServiceAccountToAdministrators: «' + acct + '» — член локальной группы Администраторов.');
end;

{ ===== Регистрация/конфиг службы (MLC-107/170) ===== }

{ Создание/конфиг службы (sc create/config + описание) — в Code-секции, с проверкой rc.
  Вызывается в CurStepChanged(ssPostInstall) ПОСЛЕ записи конфига/пароля admin и снятия старого
  firewall-правила, но ДО ProvisionSqlLogin/ACL/StartServiceAndFinalize (порядок ADR-49: учётка
  и её SID должны существовать до провижининга логина и до грантов ACL по имени NT SERVICE\…).
  Контракт ошибок:
    - sc create (чистая установка): rc<>0 -> MsgBox с подсказкой (особо 1057 — невалидная учётка
      службы) и прерывание установки исключением (откат);
    - sc config (апгрейд): rc<>0 -> MsgBox-предупреждение (служба остаётся, но аккаунт/путь
      могли не примениться);
    - sc description: rc не валидируем (косметика).
  Пароль именованной учётки (если есть) фигурирует только в командной строке sc — неизбежно;
  виртуальная учётка и gMSA пароля не имеют. }
procedure RegisterService;
var
  rc: Integer;
begin
  if not ServiceExists then
  begin
    { --- Чистая установка: создаём службу --- }
    if (not Exec(ExpandConstant('{sys}\sc.exe'), GetScCreateParams(''),
                 '', SW_HIDE, ewWaitUntilTerminated, rc)) or (rc <> 0) then
    begin
      if rc = 1057 then
        { 1057 = ERROR_INVALID_SERVICE_ACCOUNT: для obj= указана невалидная учётка службы.
          При виртуальной учётке это маловероятно; типично — опечатка в именованной учётке/gMSA
          или неверный пароль. }
        MsgBox('Не удалось создать службу (код 1057 — недопустимая учётная запись службы).' + #13#10#13#10 +
               'Если на шаге «Учётная запись службы» выбрана именованная учётная запись Windows / gMSA — ' +
               'проверьте её имя (ДОМЕН\Пользователь, .\Пользователь или ДОМЕН\Имя$ для gMSA) и пароль ' +
               '(для gMSA пароль не вводится — должен быть отмечен соответствующий чекбокс).' + #13#10#13#10 +
               'Для виртуальной учётной записи «{#MyVirtualAccount}» ввод не требуется — выберите её, ' +
               'если нет особых требований домена. Подробности — в логе установки.',
               mbCriticalError, MB_OK)
      else
        MsgBox('Не удалось создать службу «{#MyServiceName}» (код ' + IntToStr(rc) + ').' + #13#10#13#10 +
               'Проверьте выбранную учётную запись службы и (для именованной учётки) пароль. ' +
               'Подробности — в логе установки.',
               mbCriticalError, MB_OK);
      { Жёсткое прерывание: исключение из CurStepChanged откатывает установку — служба не
        создалась, нельзя завершать «успехом». }
      RaiseException('Создание службы «{#MyServiceName}» завершилось с кодом ' + IntToStr(rc) + '.');
    end;
  end
  else
  begin
    { --- Апгрейд: пересоздавать службу не нужно, выравниваем binPath/аккаунт (без пароля для
      виртуальной/gMSA) --- }
    if (not Exec(ExpandConstant('{sys}\sc.exe'), GetScConfigParams(''),
                 '', SW_HIDE, ewWaitUntilTerminated, rc)) or (rc <> 0) then
      MsgBox('Не удалось обновить параметры службы «{#MyServiceName}» (код ' + IntToStr(rc) + ').' + #13#10 +
             'Служба сохранена, но путь к программе или учётная запись могли не примениться — ' +
             'проверьте параметры службы вручную (services.msc) и лог установки.',
             mbError, MB_OK);
  end;

  { --- Описание службы (косметика, rc не валидируем) --- }
  Exec(ExpandConstant('{sys}\sc.exe'),
       'description {#MyServiceName} "Панель управления лицензиями 1С (MitLicense Center)"',
       '', SW_HIDE, ewWaitUntilTerminated, rc);
end;

{ ===== Устойчивость + firewall + старт службы (MLC-107/170) ===== }

{ Завершающий шаг ssPostInstall (ADR-49): recovery-политика + SQL-зависимость + открытие
  firewall-порта + sc start ПОСЛЕДНИМ. К моменту вызова логин учётки уже создан (ProvisionSqlLogin),
  учётка в Администраторах (best-effort) и ACL гранты применены — fail-fast bootstrap (ADR-18)
  коннектится к SQL уже как учётка службы, мигрирует и сидит admin.
  Контракт ошибок:
    - sc failure (recovery-политика, REL-03/ADR-40): rc<>0 -> предупреждение (без Abort);
    - sc config depend= (локальная SQL-зависимость): rc<>0 -> предупреждение (без Abort);
      удалённый/неоднозначный инстанс — пропуск;
    - netsh add: rc<>0 -> предупреждение (порт мог не открыться);
    - sc start: rc<>0 (кроме 1056) -> предупреждение со ссылкой на Журнал событий (fail-fast
      bootstrap, ADR-18), без Abort — служба уже создана; rc=1056 (ALREADY_RUNNING) = успех (MLC-116). }
procedure StartServiceAndFinalize;
var
  rc: Integer;
  startWarn: string;
begin
  { --- Устойчивость (REL-03, ADR-40): и на чистой установке, и на апгрейде ---
    Recovery-политика — основной механизм; локальная SQL-зависимость — best-effort
    дополнение. Обе процедуры best-effort (предупреждение без Abort). }
  ConfigureServiceRecovery;
  ConfigureSqlDependency;

  { --- Firewall: входящий TCP на выбранный порт --- }
  if (not Exec(ExpandConstant('{sys}\netsh.exe'), GetFirewallAddParams(''),
               '', SW_HIDE, ewWaitUntilTerminated, rc)) or (rc <> 0) then
    MsgBox('Не удалось открыть TCP-порт ' + NetPort + ' в брандмауэре Windows (код ' + IntToStr(rc) + ').' + #13#10 +
           'Панель установлена, но может быть недоступна по сети — откройте порт вручную ' +
           '(брандмауэр Windows) или проверьте лог установки.',
           mbError, MB_OK);

  { --- Старт службы (ПОСЛЕДНИМ): на провале предупреждаем (без Abort — служба создана) --- }
  { rc=1056 (ERROR_SERVICE_ALREADY_RUNNING) — НЕ ошибка (MLC-116): на апгрейде служба может
    уже работать (например restart-manager перезапустил её), sc start тогда возвращает 1056 —
    это успех, не предупреждаем и не помечаем провал. Любой другой ненулевой rc — провал. }
  if (not Exec(ExpandConstant('{sys}\sc.exe'), 'start {#MyServiceName}',
               '', SW_HIDE, ewWaitUntilTerminated, rc)) or ((rc <> 0) and (rc <> 1056)) then
  begin
    startWarn :=
      'Служба «{#MyServiceName}» не запустилась (код ' + IntToStr(rc) + ').' + #13#10#13#10 +
      'Причину смотрите в Журнале событий Windows (Приложение, источник MitLicenseCenter): ' +
      'бэкенд при старте применяет миграции и проверяет доступ к SQL fail-fast (ADR-18) и ' +
      'пишет туда причину отказа (например SQL недоступен или нет прав у учётной записи службы).' + #13#10#13#10 +
      'После устранения причины запустите службу вручную (services.msc) или перезагрузите хост.';
    MsgBox(startWarn, mbError, MB_OK);
    { Помечаем провал старта: финальный экран wpFinished покажет ОШИБКУ, а не «успех»
      (MsgBox выше — немедленный фидбэк, но его легко закрыть и не заметить). MLC-112. }
    ServiceStartFailed := True;
  end;
end;

{ ===== Тест подключения (PowerShell + System.Data.SqlClient) ===== }

{ Тест достижимости инстанса под ВЫБРАННОЙ личностью провижининга (MLC-171): Integrated
  Security установщика-админа (дефолт) либо введённый SQL-логин с ролью sysadmin. SQL-логин
  учётки службы создаётся установщиком позже (ProvisionSqlLogin) — тест лишь проверяет, что
  инстанс доступен под той же личностью, которой пойдёт провижининг (тестируем тем же, чем
  создаём). Возвращает True при успехе; errMsg — текст. }
function TestSqlConnection(out errMsg: string): Boolean;
var
  connStr, psScript, scriptPath, cmdLine: string;
  rc: Integer;
begin
  Result := False;
  errMsg := '';

  if SqlInstance = '' then
  begin
    errMsg := 'Не указан SQL-инстанс.';
    Exit;
  end;

  { Тест — к master (БД панели может ещё не существовать), под выбранной личностью провижининга. }
  connStr := ProvisioningConnString('master');

  { Сниппет на System.Data.SqlClient (есть в .NET Framework / PS 5.1). Строку подключения
    вставляем в PowerShell '...'-литерал внутри временного .ps1 (Pascal Script не имеет
    SetEnvironmentVariable). Временный скрипт лежит во временном каталоге установщика
    (ACL установщика) и удаляется сразу после запуска. }
  psScript :=
    '$ErrorActionPreference=''Stop'';' + #13#10 +
    '$cs = ''' + PsSingleQuote(connStr) + ''';' + #13#10 +
    'try {' + #13#10 +
    '  $c = New-Object System.Data.SqlClient.SqlConnection $cs;' + #13#10 +
    '  $c.Open();' + #13#10 +
    '  $c.Close();' + #13#10 +
    '  exit 0;' + #13#10 +
    '} catch {' + #13#10 +
    '  [Console]::Error.WriteLine($_.Exception.Message);' + #13#10 +
    '  exit 1;' + #13#10 +
    '}' + #13#10;

  scriptPath := ExpandConstant('{tmp}\mlc-conntest.ps1');
  if not SaveStringToFile(scriptPath, psScript, False) then
  begin
    errMsg := 'Не удалось подготовить временный скрипт проверки.';
    Exit;
  end;

  cmdLine := '-NoProfile -ExecutionPolicy Bypass -File "' + scriptPath + '"';
  if not Exec(ExpandConstant('{sys}\WindowsPowerShell\v1.0\powershell.exe'),
              cmdLine, '', SW_HIDE, ewWaitUntilTerminated, rc) then
  begin
    DeleteFile(scriptPath);
    errMsg := 'Не удалось запустить powershell.exe для проверки.';
    Exit;
  end;

  { Удалить временный скрипт (в нём строка подключения с паролем). }
  DeleteFile(scriptPath);

  if rc = 0 then
    Result := True
  else
    errMsg := 'SQL Server недоступен или учётные данные неверны (код ' + IntToStr(rc) + ').' + #13#10 +
              'Проверьте инстанс, имя БД/логина, пароль и права (см. лог сервера).';
end;

{ ===== Проба: целевая БД уже содержит установку панели? (MLC-105) ===== }

{ Тем же приёмом, что TestSqlConnection (временный .ps1 с connstr в '...'-литерале,
  powershell -File, файл удаляется), но connstr на выбранную БД (НЕ master). PS-сниппет
  открывает соединение и выполняет:
    IF OBJECT_ID('auth.Users','U') IS NULL SELECT 0 ELSE SELECT COUNT(*) FROM auth.Users
  и пишет число в stdout. Зеркалит условие сидера userManager.Users.AnyAsync (любой
  пользователь, не только admin). Fail-open: БД недоступна (ещё не создана) / ошибка
  запроса / соединение не открылось -> вернуть False (трактуем как «не инициализирована»),
  чтобы не было хуже текущего поведения. Подключение — под выбранной личностью провижининга:
  Integrated Security установщика-админа или введённый SQL-логин с ролью sysadmin (MLC-171);
  SQL-логин учётки службы создаётся позже. }
function DatabaseHasPanelUsers: Boolean;
var
  connStr, psScript, scriptPath, outPath, cmdLine: string;
  outData: AnsiString;  { LoadStringFromFile требует var AnsiString — не String. }
  rc: Integer;
begin
  Result := False;

  if (SqlInstance = '') or (SqlDatabase = '') then
    Exit;

  { connstr на выбранную БД под той же личностью провижининга, что и тест/создание логина
    (MLC-171): Integrated Security установщика-админа или введённый SQL-логин с ролью sysadmin. }
  connStr := ProvisioningConnString(SqlDatabase);

  { Результат запроса пишем в файл (не в stdout) — exit-код только индикатор успеха.
    connStr в '...'-литерале временного .ps1. }
  outPath := ExpandConstant('{tmp}\mlc-dbprobe-out.txt');
  DeleteFile(outPath);

  psScript :=
    '$ErrorActionPreference=''Stop'';' + #13#10 +
    '$cs = ''' + PsSingleQuote(connStr) + ''';' + #13#10 +
    '$out = ''' + PsSingleQuote(outPath) + ''';' + #13#10 +
    'try {' + #13#10 +
    '  $c = New-Object System.Data.SqlClient.SqlConnection $cs;' + #13#10 +
    '  $c.Open();' + #13#10 +
    '  $cmd = $c.CreateCommand();' + #13#10 +
    '  $cmd.CommandText = ''IF OBJECT_ID(''''auth.Users'''',''''U'''') IS NULL SELECT 0 ELSE SELECT COUNT(*) FROM auth.[Users]'';' + #13#10 +
    '  $n = $cmd.ExecuteScalar();' + #13#10 +
    '  $c.Close();' + #13#10 +
    '  Set-Content -LiteralPath $out -Value ([string]$n) -Encoding ASCII;' + #13#10 +
    '  exit 0;' + #13#10 +
    '} catch {' + #13#10 +
    '  exit 1;' + #13#10 +
    '}' + #13#10;

  scriptPath := ExpandConstant('{tmp}\mlc-dbprobe.ps1');
  if not SaveStringToFile(scriptPath, psScript, False) then
    Exit;  { fail-open }

  cmdLine := '-NoProfile -ExecutionPolicy Bypass -File "' + scriptPath + '"';
  if not Exec(ExpandConstant('{sys}\WindowsPowerShell\v1.0\powershell.exe'),
              cmdLine, '', SW_HIDE, ewWaitUntilTerminated, rc) then
  begin
    DeleteFile(scriptPath);
    DeleteFile(outPath);
    Exit;  { fail-open: не смогли запустить }
  end;

  { Временный скрипт содержит строку подключения (пароль) — удаляем сразу. }
  DeleteFile(scriptPath);

  if rc = 0 then
  begin
    if LoadStringFromFile(outPath, outData) then
      if StrToIntDef(Trim(outData), 0) > 0 then
        Result := True;
  end;
  { rc <> 0 (БД недоступна / ошибка запроса) -> fail-open, Result остаётся False. }
  DeleteFile(outPath);
end;

{ ===== Обработчик кнопки «Проверить подключение» ===== }

procedure TestButtonClick(Sender: TObject);
var
  ok: Boolean;
  errMsg: string;
begin
  WizardForm.ActiveControl := nil;
  TestResultLabel.Caption := 'Проверка подключения...';
  TestResultLabel.Font.Color := clNavy;
  WizardForm.Refresh;

  ok := TestSqlConnection(errMsg);
  ConnTestPassed := ok;
  if ok then
  begin
    if ProvisioningMode = PROV_SQLLOGIN then
      TestResultLabel.Caption := 'OK: инстанс достижим (проверка под указанным SQL-логином). ' +
        'SQL-логин учётной записи службы установщик создаст автоматически при установке.'
    else
      TestResultLabel.Caption := 'OK: инстанс достижим (проверка под учётной записью администратора-установщика). ' +
        'SQL-логин учётной записи службы установщик создаст автоматически при установке.';
    TestResultLabel.Font.Color := clGreen;
  end
  else
  begin
    TestResultLabel.Caption := 'Ошибка: ' + errMsg;
    TestResultLabel.Font.Color := clRed;
  end;
end;

{ ===== Состояние полей SQL-логина на странице провижининга (MLC-171) ===== }

{ Поля логина/пароля значимы только для варианта «SQL-логин» (PROV_SQLLOGIN). Для Integrated
  Security гасим их (Enabled := False) — визуальная подсказка, что ввод не нужен. Метки тоже
  приглушаем сменой цвета. Вызывается при заходе на страницу (CurPageChanged) и при клике по
  radio (OnClickCheck). Защищено на nil — может вызваться до построения полей. }
procedure UpdateProvFieldsState;
var
  sqlMode: Boolean;
begin
  if (ProvUserEdit = nil) or (ProvPasswordEdit = nil) then
    Exit;
  sqlMode := (ProvisioningMode = PROV_SQLLOGIN);
  ProvUserEdit.Enabled := sqlMode;
  ProvPasswordEdit.Enabled := sqlMode;
  if ProvUserLabel <> nil then
    if sqlMode then ProvUserLabel.Font.Color := clBlack else ProvUserLabel.Font.Color := clGray;
  if ProvPasswordLabel <> nil then
    if sqlMode then ProvPasswordLabel.Font.Color := clBlack else ProvPasswordLabel.Font.Color := clGray;
end;

{ OnClickCheck radio-списка PageProv: переключение варианта меняет доступность полей и
  сбрасывает результат прошлого теста (личность изменилась — прежний тест неактуален). }
procedure ProvModeClick(Sender: TObject);
begin
  UpdateProvFieldsState;
  ConnTestPassed := False;
  if TestResultLabel <> nil then
  begin
    TestResultLabel.Caption := '';
    TestResultLabel.Font.Color := clNavy;
  end;
end;

{ ===== Построение страниц мастера ===== }

procedure InitializeWizard;
begin
  ConnTestPassed := False;
  DbAlreadyInitialized := False;
  DbInitWarningShown := False;
  ServiceStartFailed := False;

  { --- Страница «Резервная копия перед апгрейдом» (MLC-112, только апгрейд) ---
    Показывается ДО замены файлов: апгрейд подменяет файлы новой версией, а на старте новая
    версия применяет миграции БД; согласованного автоматического отката файлы<->БД нет.
    Поэтому требуем явное подтверждение, что оператор сделал бэкап (1) базы данных SQL и
    (2) каталога ключей шифрования (key ring) — без них секреты в БД не расшифровать (единый
    бэкап-юнит). AfterID = wpSelectDir, но из-за обратного порядка вставки Inno (страницы с
    одинаковым AfterID идут в порядке, обратном созданию; PageSql и следующие за ней
    создаются позже и оттесняют эту страницу) фактически она встаёт ПОСЛЕДНЕЙ из кастомных —
    прямо перед «Готово к установке». Это и нужно: подтверждение бэкапа — последний экран
    перед заменой файлов, самое свежее напоминание перед необратимым шагом. На чистой
    установке страница пропускается через ShouldSkipPage (not ServiceExists). Гейт «Далее» —
    в NextButtonClick: без отметки чекбокса дальше не пускаем. }
  PageUpgradeBackup := CreateInputOptionPage(wpSelectDir,
    'Резервная копия перед обновлением',
    'Обновление поверх установленной версии — сначала сделайте бэкап',
    'Обновление заменит файлы программы новой версией, а при первом запуске новая версия ' +
    'применит миграции базы данных. Согласованного автоматического отката «файлы + база ' +
    'данных» НЕТ: при сбое обновления вернуться можно ТОЛЬКО восстановлением из резервной ' +
    'копии, сделанной СЕЙЧАС. Перед продолжением создайте резервную копию: (1) базы данных ' +
    'SQL Server панели и (2) каталога ключей шифрования %ProgramData%\MitLicenseCenter ' +
    '(key ring) — без ключей секреты в базе данных расшифровать нельзя (база и key ring — ' +
    'единый бэкап-юнит).',
    False, False);
  PageUpgradeBackup.Add('Я создал резервную копию базы данных SQL Server и каталога ключей ' +
    'шифрования (key ring, %ProgramData%\MitLicenseCenter) и готов продолжить обновление.');

  { --- Страница «SQL Server»: инстанс + БД --- }
  PageSql := CreateInputQueryPage(wpSelectDir,
    'SQL Server',
    'Параметры подключения к SQL Server',
    'Укажите экземпляр SQL Server и имя базы данных панели. База создаётся автоматически при первом запуске, если её ещё нет.');
  PageSql.Add('Экземпляр SQL Server (например . или СЕРВЕР\SQLEXPRESS):', False);
  PageSql.Add('Имя базы данных:', False);
  PageSql.Values[0] := '.';
  PageSql.Values[1] := 'MitLicenseCenter';

  { --- Страница «Подключение установщика к SQL» (MLC-171) --- }
  { Под какой личностью УСТАНОВЩИК подключается к SQL, чтобы создать Windows-логин учётки
    службы (CREATE LOGIN … FROM WINDOWS + sysadmin) и проверить достижимость. Ортогонально
    выбору учётки службы (страница ниже): создаётся ВСЕГДА Windows-логин; SQL-логин (sa) —
    лишь разовый «ключ» провижининга, в конфиг не пишется и нигде не сохраняется. }
  PageProv := CreateInputOptionPage(PageSql.ID,
    'Подключение установщика к SQL',
    'Под какой учётной записью установщик подключается к SQL Server',
    'Установщик один раз подключается к SQL, чтобы создать Windows-логин учётной записи службы ' +
    '(всегда CREATE LOGIN … FROM WINDOWS) и назначить ему роль sysadmin. Назначить sysadmin может ' +
    'только тот, кто сам sysadmin — выберите подходящую личность.' + #13#10 +
    'Указанные здесь учётные данные используются ОДНОКРАТНО и НЕ сохраняются: служба в любом случае ' +
    'подключается к SQL по доверенному подключению Windows (Trusted_Connection), а SQL-пароль в ' +
    'конфигурацию не пишется. Вариант «SQL-логин» работает только на экземпляре в смешанном режиме ' +
    '(разрешена SQL-аутентификация).',
    True, False);
  PageProv.Add('Под учётной записью администратора, запустившего установку (Integrated Security) — ' +
    'рекомендуется. Подходит и для экземпляров «только Windows-аутентификация». Ввод не требуется.');
  PageProv.Add('SQL-логином с ролью sysadmin (например sa) — для смешанного режима, когда ' +
    'запускающий администратор не является sysadmin на SQL.');
  PageProv.SelectedValueIndex := PROV_INTEGRATED;

  { Поля SQL-логина провижининга на поверхности страницы (значимы только для варианта (1)).
    Кастомные TNewEdit/TPasswordEdit + метки — как GmsaCheckbox/TestButton на PageCreds. Раскладка
    под radio: нижнюю границу блока radio берём по CheckListBox (Exclusive-режим, не ListBox —
    радиокнопки лежат на поверхности, CheckListBox авто-высоты по числу пунктов). Высоту полей
    ввода задаём явно (ScaleY(23)) — у созданных вручную контролов нет авто-высоты до показа. }
  ProvUserLabel := TNewStaticText.Create(WizardForm);
  ProvUserLabel.Parent := PageProv.Surface;
  ProvUserLabel.Left := 0;
  ProvUserLabel.Top := PageProv.CheckListBox.Top + PageProv.CheckListBox.Height + ScaleY(12);
  ProvUserLabel.Width := PageProv.SurfaceWidth;
  ProvUserLabel.Caption := 'SQL-логин с ролью sysadmin (например sa):';

  ProvUserEdit := TNewEdit.Create(WizardForm);
  ProvUserEdit.Parent := PageProv.Surface;
  ProvUserEdit.Left := 0;
  ProvUserEdit.Top := ProvUserLabel.Top + ProvUserLabel.Height + ScaleY(2);
  ProvUserEdit.Width := PageProv.SurfaceWidth;
  ProvUserEdit.Height := ScaleY(23);
  ProvUserEdit.Text := '';

  ProvPasswordLabel := TNewStaticText.Create(WizardForm);
  ProvPasswordLabel.Parent := PageProv.Surface;
  ProvPasswordLabel.Left := 0;
  ProvPasswordLabel.Top := ProvUserEdit.Top + ProvUserEdit.Height + ScaleY(8);
  ProvPasswordLabel.Width := PageProv.SurfaceWidth;
  ProvPasswordLabel.Caption := 'Пароль SQL-логина:';

  ProvPasswordEdit := TPasswordEdit.Create(WizardForm);
  ProvPasswordEdit.Parent := PageProv.Surface;
  ProvPasswordEdit.Left := 0;
  ProvPasswordEdit.Top := ProvPasswordLabel.Top + ProvPasswordLabel.Height + ScaleY(2);
  ProvPasswordEdit.Width := PageProv.SurfaceWidth;
  ProvPasswordEdit.Height := ScaleY(23);
  ProvPasswordEdit.Password := True;
  ProvPasswordEdit.Text := '';

  { Кнопка теста + метка-результат — на странице провижининга (MLC-171): тест проходит под той
    же личностью, которой пойдёт создание логина, и работает для ОБОИХ режимов учётки службы
    (виртуальная учётка своей страницы кредов не имеет). }
  TestButton := TNewButton.Create(WizardForm);
  TestButton.Parent := PageProv.Surface;
  TestButton.Caption := 'Проверить подключение';
  TestButton.Width := ScaleX(150);
  TestButton.Height := ScaleY(25);
  TestButton.Left := 0;
  TestButton.Top := ProvPasswordEdit.Top + ProvPasswordEdit.Height + ScaleY(12);
  TestButton.OnClick := @TestButtonClick;

  TestResultLabel := TNewStaticText.Create(WizardForm);
  TestResultLabel.Parent := PageProv.Surface;
  TestResultLabel.Left := 0;
  TestResultLabel.Top := TestButton.Top + TestButton.Height + ScaleY(10);
  TestResultLabel.Width := PageProv.SurfaceWidth;
  TestResultLabel.AutoSize := False;
  TestResultLabel.Height := ScaleY(48);
  TestResultLabel.WordWrap := True;
  TestResultLabel.Caption := '';

  { Реакция на смену варианта (radio) + начальное состояние полей (по дефолту PROV_INTEGRATED —
    поля погашены). }
  PageProv.CheckListBox.OnClickCheck := @ProvModeClick;
  UpdateProvFieldsState;

  { --- Страница «Учётная запись службы» (ADR-49) --- }
  PageAuthMode := CreateInputOptionPage(PageProv.ID,
    'Учётная запись службы',
    'Под какой учётной записью работает служба панели',
    'Служба подключается к ЛОКАЛЬНОМУ SQL Server по доверенному подключению Windows ' +
    '(Trusted_Connection). SQL-логин и пароль в конфигурации БОЛЬШЕ НЕ используются. ' +
    'Установщик сам создаёт SQL-логин выбранной учётной записи с ролью sysadmin — для этого ' +
    'запускающий установку администратор должен быть sysadmin на локальном экземпляре SQL.',
    True, False);
  PageAuthMode.Add('Виртуальная учётная запись «{#MyVirtualAccount}» (рекомендуется) — без пароля, ' +
    'подходит и для рабочей группы, и для домена. Ввод учётных данных не требуется.');
  PageAuthMode.Add('Указанная учётная запись Windows / gMSA — для доменных сценариев ' +
    '(ДОМЕН\Пользователь или ДОМЕН\Имя$ для gMSA).');
  PageAuthMode.SelectedValueIndex := ACCT_VIRTUAL;

  { --- Страница «Учётные данные» (только для именованной учётки, ShouldSkipPage) --- }
  { Только имя/пароль Windows-учётки службы + чекбокс gMSA. Тест подключения к SQL перенесён на
    страницу «Подключение установщика к SQL» (MLC-171) — он тестирует личность провижининга и
    работает для обоих режимов учётки службы (включая виртуальную, у которой этой страницы нет). }
  PageCreds := CreateInputQueryPage(PageAuthMode.ID,
    'Учётные данные',
    'Именованная учётная запись Windows / gMSA для службы',
    'Введите имя заранее созданной Windows-учётной записи (или gMSA) и, если это не gMSA, её пароль.');
  PageCreds.Add('Windows-аккаунт / gMSA (ДОМЕН\Пользователь или ДОМЕН\Имя$):', False);
  PageCreds.Add('Пароль:', True);

  { Чекбокс gMSA: учётка без пароля. При отмеченном — поле пароля игнорируется, password= в
    sc create не передаётся (ServiceAccountUsesPassword → False). ADR-49. }
  GmsaCheckbox := TNewCheckBox.Create(WizardForm);
  GmsaCheckbox.Parent := PageCreds.Surface;
  GmsaCheckbox.Left := 0;
  GmsaCheckbox.Top := PageCreds.Edits[1].Top + PageCreds.Edits[1].Height + ScaleY(8);
  GmsaCheckbox.Width := PageCreds.SurfaceWidth;
  GmsaCheckbox.Caption := 'Это групповая управляемая учётная запись (gMSA) — без пароля';
  GmsaCheckbox.Checked := False;

  { --- Страница «Сеть» --- }
  PageNet := CreateInputQueryPage(PageCreds.ID,
    'Сеть',
    'Сетевые параметры панели',
    'TCP-порт открывается во входящих правилах брандмауэра и используется в Urls (http://+:<порт>). AllowedHosts — допустимые имена хостов (* — любой).');
  PageNet.Add('TCP-порт панели:', False);
  PageNet.Add('AllowedHosts:', False);
  PageNet.Values[0] := '{#MyDefaultPort}';
  PageNet.Values[1] := '*';

  { --- Страница «Учётная запись администратора» (только чистая установка) --- }
  PageAdmin := CreateInputQueryPage(PageNet.ID,
    'Учётная запись администратора',
    'Пароль первого администратора панели',
    'Задайте пароль администратора (логин — admin). Под ним вы войдёте в панель после установки.' + #13#10 +
    'Требования: не менее 12 символов, заглавная и строчная буквы, цифра и спецсимвол.');
  PageAdmin.Add('Пароль администратора:', True);
  PageAdmin.Add('Подтверждение пароля:', True);
end;

{ Страница пароля admin пропускается, когда применять пароль некуда: на апгрейде (admin уже
  создан в БД, паритет с ServiceExists) ИЛИ на чистой установке поверх уже-инициализированной
  БД (в auth.Users уже есть пользователи — сидер сидит только пустую БД, заданный пароль не
  применится). DbAlreadyInitialized вычисляется один раз в NextButtonClick (см.). MLC-102/105. }
function ShouldSkipPage(PageID: Integer): Boolean;
begin
  Result := False;
  if (PageAdmin <> nil) and (PageID = PageAdmin.ID) then
    Result := ServiceExists or DbAlreadyInitialized;
  { Страница бэкапа перед апгрейдом — только на апгрейде (служба уже зарегистрирована).
    На чистой установке (not ServiceExists) пропускаем. MLC-112 (REL-02). }
  if (PageUpgradeBackup <> nil) and (PageID = PageUpgradeBackup.ID) then
    Result := not ServiceExists;
  { Страница учётных данных — только для именованной учётки / gMSA (ADR-49). Для виртуальной
    учётной записи ввод не требуется, страницу пропускаем. }
  if (PageCreds <> nil) and (PageID = PageCreds.ID) then
    Result := (AccountMode = ACCT_VIRTUAL);
end;

{ Сброс результата теста при заходе на страницу провижининга + обновление состояния полей;
  подсказки по именованной учётке на странице учётных данных. }
procedure CurPageChanged(CurPageID: Integer);
begin
  { Страница «Подключение установщика к SQL» (MLC-171): сбрасываем прежний результат теста
    (личность провижининга могла измениться) и приводим доступность полей SQL-логина к
    выбранному варианту. }
  if (PageProv <> nil) and (CurPageID = PageProv.ID) then
  begin
    ConnTestPassed := False;
    if TestResultLabel <> nil then
    begin
      TestResultLabel.Caption := '';
      TestResultLabel.Font.Color := clNavy;
    end;
    UpdateProvFieldsState;
  end;

  if (PageCreds <> nil) and (CurPageID = PageCreds.ID) then
  begin
    { Страница показывается только для именованной учётки / gMSA (ADR-49; виртуальная — пропуск
      через ShouldSkipPage). REL-06/MLC-127: явные требования к учётке — оператор должен знать ДО
      установки, иначе «установка прошла, а половина продукта не работает». Тест подключения к SQL
      перенесён на страницу «Подключение установщика к SQL» (MLC-171) — здесь только реквизиты учётки. }
    PageCreds.PromptLabels[0].Caption := 'Windows-аккаунт / gMSA (ДОМЕН\Пользователь или ДОМЕН\Имя$):';
    PageCreds.SubCaptionLabel.Caption :=
      'Введите именованную Windows-учётную запись (или gMSA), под которой будет работать служба. ' +
      'Если это gMSA — отметьте чекбокс ниже и оставьте поле пароля пустым (паролем gMSA управляет домен).' + #13#10 +
      '' + #13#10 +
      'Требования к учётной записи:' + #13#10 +
      '1. SQL: SQL-логин этой учётки создаётся установщиком автоматически (роль sysadmin) под личностью, ' +
      'выбранной на странице «Подключение установщика к SQL». Достижимость инстанса проверяется там же.' + #13#10 +
      '2. IIS: для функций публикации/recycle/iisreset учётку установщик добавляет в локальную группу ' +
      'Администраторов автоматически. Если это не удастся — публикации будут в статусе «Ошибка проверки».' + #13#10 +
      '3. Право «Вход в качестве службы» (SeServiceLogonRight): SCM выдаёт его автоматически при создании ' +
      'службы; если нет — задать вручную через secpol.msc.';
  end;

  { Финальный экран: подтверждаем вход и даём URL. Пароль admin НЕ показываем — его задал
    оператор (на апгрейде admin уже был). MLC-102. }
  if CurPageID = wpFinished then
  begin
    if ServiceStartFailed then
    begin
      { Старт службы провалился (MLC-112): финальный экран должен быть недвусмысленно
        ошибочным, а не «обновлена/установлена и запущена». Перекрашиваем заголовок и текст
        в красный. Покрывает и апгрейд, и чистую установку — единая точка истины. }
      WizardForm.FinishedHeadingLabel.Caption := 'Установка завершена с ОШИБКОЙ';
      WizardForm.FinishedLabel.Font.Color := clRed;
      { Формулировка корректна для обоих сценариев (MLC-116): на апгрейде служба не «создана»,
        она уже существовала — при ServiceExists говорим просто «не запустилась». }
      WizardForm.FinishedLabel.Caption :=
        'Служба «{#MyServiceName}» не запустилась — панель сейчас недоступна.' + #13#10#13#10 +
        'Причину смотрите в Журнале событий Windows (Приложение, источник MitLicenseCenter): ' +
        'бэкенд при старте применяет миграции базы данных и проверяет доступ к SQL fail-fast ' +
        '(ADR-18) и пишет туда причину отказа.' + #13#10#13#10 +
        'После устранения причины запустите службу вручную через services.msc.' + #13#10#13#10 +
        'Если это было обновление и миграции применились частично — восстановите базу данных ' +
        'SQL Server и каталог ключей (key ring) из резервной копии, сделанной перед ' +
        'обновлением.';
    end
    else if ServiceExists then
      WizardForm.FinishedLabel.Caption :=
        'Панель обновлена и запущена.' + #13#10#13#10 +
        'Откройте панель: ' + PanelUrl('') + #13#10 +
        'Учётные записи администраторов сохранены.'
    else if DbAlreadyInitialized then
      { Чистая установка поверх уже-инициализированной БД: пароль admin не применялся,
        страница пароля была пропущена. Учётки в существующей БД сохранены. MLC-105. }
      WizardForm.FinishedLabel.Caption :=
        'Панель установлена и запущена.' + #13#10#13#10 +
        'Учётные записи в существующей базе данных сохранены — войдите прежними ' +
        'учётными данными (или сбросьте пароль admin утилитой reset-admin).' + #13#10 +
        'Откройте панель: ' + PanelUrl('')
    else
      WizardForm.FinishedLabel.Caption :=
        'Панель установлена и запущена.' + #13#10#13#10 +
        'Войдите как admin с заданным паролем.' + #13#10 +
        'Откройте панель: ' + PanelUrl('');
  end;
end;

{ Валидация и гейт перехода Next. }
function NextButtonClick(CurPageID: Integer): Boolean;
var
  port, errMsg: string;
  portNum: Integer;
begin
  Result := True;

  { --- Резервная копия перед апгрейдом (MLC-112) --- }
  { Обязательное подтверждение бэкапа на апгрейде: без отметки чекбокса дальше не пускаем.
    На чистой установке страница пропущена (ShouldSkipPage), сюда не доходим. }
  if (PageUpgradeBackup <> nil) and (CurPageID = PageUpgradeBackup.ID) then
  begin
    if not PageUpgradeBackup.Values[0] then
    begin
      MsgBox('Подтвердите, что вы создали резервную копию базы данных SQL Server и каталога ' +
             'ключей шифрования (key ring, %ProgramData%\MitLicenseCenter).' + #13#10#13#10 +
             'Без резервной копии откатить неудачное обновление будет невозможно — ' +
             'продолжать нельзя.',
             mbError, MB_OK);
      Result := False;
      Exit;
    end;
  end;

  { --- SQL Server --- }
  if (PageSql <> nil) and (CurPageID = PageSql.ID) then
  begin
    if SqlInstance = '' then
    begin
      MsgBox('Укажите экземпляр SQL Server.', mbError, MB_OK);
      Result := False;
      Exit;
    end;
    if SqlDatabase = '' then
    begin
      MsgBox('Укажите имя базы данных.', mbError, MB_OK);
      Result := False;
      Exit;
    end;
  end;

  { --- Подключение установщика к SQL (MLC-171) --- }
  { Личность провижининга: для SQL-логина требуем логин+пароль; в обоих режимах требуем
    успешный тест достижимости под выбранной личностью (тестируем тем же, чем будем создавать
    логин службы). Гейт ConnTestPassed единый: если ещё не тестировали/провалили — тестируем
    сейчас и блокируем при ошибке. }
  if (PageProv <> nil) and (CurPageID = PageProv.ID) then
  begin
    if ProvisioningMode = PROV_SQLLOGIN then
    begin
      if ProvUser = '' then
      begin
        MsgBox('Укажите SQL-логин с ролью sysadmin (например sa) — либо выберите вариант ' +
               '«Integrated Security».', mbError, MB_OK);
        Result := False;
        Exit;
      end;
      if ProvPassword = '' then
      begin
        MsgBox('Укажите пароль SQL-логина.', mbError, MB_OK);
        Result := False;
        Exit;
      end;
    end;
    { Гейт: требуем успешный тест достижимости SQL под выбранной личностью. }
    if not ConnTestPassed then
    begin
      if not TestSqlConnection(errMsg) then
      begin
        if TestResultLabel <> nil then
        begin
          TestResultLabel.Caption := 'Ошибка: ' + errMsg;
          TestResultLabel.Font.Color := clRed;
        end;
        MsgBox('Проверка подключения не пройдена:' + #13#10 + errMsg + #13#10#13#10 +
               'Исправьте данные и повторите. Продолжить установку нельзя без успешной проверки. ' +
               'Если экземпляр работает только в режиме Windows-аутентификации — выберите вариант ' +
               '«Integrated Security».',
               mbError, MB_OK);
        Result := False;
        Exit;
      end;
      ConnTestPassed := True;
    end;
  end;

  { --- Учётные данные (только именованная учётка / gMSA; виртуальная — страница пропущена) --- }
  { Тест подключения к SQL — на странице «Подключение установщика к SQL» (MLC-171); здесь
    только реквизиты Windows-учётки службы. }
  if (PageCreds <> nil) and (CurPageID = PageCreds.ID) then
  begin
    if CredUser = '' then
    begin
      MsgBox('Укажите Windows-учётную запись (или gMSA) для службы.', mbError, MB_OK);
      Result := False;
      Exit;
    end;
    { Пароль обязателен ТОЛЬКО для обычной учётки. gMSA — без пароля (управляет домен). }
    if (not IsGmsa) and (CredPassword = '') then
    begin
      MsgBox('Укажите пароль учётной записи.' + #13#10 +
             'Если это групповая управляемая учётная запись (gMSA) — отметьте чекбокс «Это gMSA».',
             mbError, MB_OK);
      Result := False;
      Exit;
    end;
  end;

  { --- Сеть --- }
  if (PageNet <> nil) and (CurPageID = PageNet.ID) then
  begin
    port := NetPort;
    portNum := StrToIntDef(port, -1);
    if (portNum < 1) or (portNum > 65535) then
    begin
      MsgBox('Порт должен быть числом в диапазоне 1..65535.', mbError, MB_OK);
      Result := False;
      Exit;
    end;
    if NetAllowedHosts = '' then
    begin
      MsgBox('Укажите AllowedHosts (например * — любой хост).', mbError, MB_OK);
      Result := False;
      Exit;
    end;

    { Уход со страницы «Сеть» к странице пароля admin — последняя точка, где можно решить,
      показывать ли её. На апгрейде (ServiceExists) проба не нужна — страница и так
      пропущена. Иначе один раз пробим целевую БД на наличие пользователей панели; если
      они есть — задаваемый пароль admin сидер не применит (он сидит только пустую БД),
      поэтому страницу пропускаем и один раз предупреждаем оператора. Проба fail-open:
      недоступная/пустая БД -> «не инициализирована» -> поведение как сейчас. MLC-105. }
    if not ServiceExists then
    begin
      DbAlreadyInitialized := DatabaseHasPanelUsers;
      if DbAlreadyInitialized and (not DbInitWarningShown) then
      begin
        DbInitWarningShown := True;
        MsgBox('База данных "' + SqlDatabase + '" уже содержит установку панели ' +
               '(учётные записи).' + #13#10#13#10 +
               'Заданный пароль администратора НЕ будет применён — существующие учётные ' +
               'записи сохраняются.' + #13#10#13#10 +
               'Сменить пароль admin — утилитой reset-admin (см. OPERATIONS) либо ' +
               'установкой на пустую базу данных.',
               mbInformation, MB_OK);
      end;
    end;
  end;

  { --- Учётная запись администратора (только чистая установка) --- }
  if (PageAdmin <> nil) and (CurPageID = PageAdmin.ID) then
  begin
    if AdminPassword = '' then
    begin
      MsgBox('Задайте пароль администратора.', mbError, MB_OK);
      Result := False;
      Exit;
    end;
    if AdminPassword <> AdminPasswordConfirm then
    begin
      MsgBox('Пароль и подтверждение не совпадают.', mbError, MB_OK);
      Result := False;
      Exit;
    end;
    if not AdminPasswordMeetsPolicy(AdminPassword) then
    begin
      MsgBox('Пароль не соответствует требованиям:' + #13#10 +
             'не менее 12 символов, заглавная и строчная буквы, цифра и спецсимвол.',
             mbError, MB_OK);
      Result := False;
      Exit;
    end;
  end;
end;

{ После копирования файлов (ssPostInstall) — порядок ADR-49 (MLC-170). Учётная запись службы
  (виртуальная или именованная) и её SID не существуют до sc create, а ProvisionSqlLogin и ACL
  (грант по имени NT SERVICE\…) на них ссылаются — поэтому сначала регистрируем службу, затем
  провижиним SQL-логин и применяем ACL, и только последним стартуем службу:
    1. WriteProductionConfig        — конфиг (всегда Trusted_Connection, без секрета SQL)
    2. WriteInitialAdminPassword    — initial-admin.secret (только чистая установка)
    3. netsh delete old rule        — снять одноимённое firewall-правило (идемпотентность порта)
    4. CreateStartMenuShortcut      — ярлык меню «Пуск» на URL панели
    5. RegisterService              — sc create/config (учётка + SID существуют; create — hard-fail)
    6. ProvisionSqlLogin            — CREATE LOGIN + sysadmin (ЖЁСТКИЙ fail/откат)
    7. AddServiceAccountToAdmins    — локальная группа Администраторов (best-effort)
    8. HardenConfigAcl              — ACL конфига (грант учётке R; имя NT SERVICE\… резолвится)
    9. HardenDataDirAcl             — ACL каталога данных (грант учётке M; (OI)(CI) накрывает .secret)
   10. StartServiceAndFinalize      — recovery + depend + firewall add + sc start ПОСЛЕДНИМ
  Инварианты: WriteInitialAdminPassword до HardenDataDirAcl; грант учётке — после sc create;
  sc start — последним. На апгрейде (ServiceExists) шаг 5 = sc config (без пароля), шаги 6-9
  идемпотентны. }
procedure CurStepChanged(CurStep: TSetupStep);
var
  rc: Integer;
begin
  if CurStep = ssPostInstall then
  begin
    { 1. Конфиг: всегда Trusted_Connection, SQL-пароля нет (ADR-49). }
    WriteProductionConfig;
    { 2. Пароль admin — ДО ужесточения ACL каталога: icacls (OI)(CI) затем накроет и этот файл,
      чтобы учётка службы могла прочитать и удалить его при первом старте. }
    WriteInitialAdminPassword;
    { 3. Снять одноимённое firewall-правило, чтобы add в StartServiceAndFinalize не плодил дубли
      и применил актуальный порт. Игнорируем результат (правила может не быть на чистой установке). }
    Exec(ExpandConstant('{sys}\netsh.exe'),
         'advfirewall firewall delete rule name="{#MyFirewallRule}"',
         '', SW_HIDE, ewWaitUntilTerminated, rc);
    { 4. Ярлык меню «Пуск» на URL панели (порт из ввода мастера). }
    CreateStartMenuShortcut;
    { 5. Регистрация службы: sc create/config. На провале create бросает исключение -> откат
      установки (MLC-107). Теперь учётка службы и её SID существуют. }
    RegisterService;
    { 6. Авто-создание SQL-логина учётки + sysadmin (ADR-49). ЖЁСТКИЙ fail: на провале MsgBox +
      RaiseException -> откат (иначе служба упадёт на старте с 18456). }
    ProvisionSqlLogin;
    { 7. Учётку — в локальную группу Администраторов для IIS/iisreset (best-effort, без Abort). }
    AddServiceAccountToAdministrators;
    { 8. ACL конфига: только SYSTEM/Administrators + учётка службы R (имя NT SERVICE\… резолвится
      после sc create). }
    HardenConfigAcl;
    { 9. ACL каталога данных: режем наследование, доступ только SYSTEM/Administrators + учётка
      службы Modify; (OI)(CI) накрывает initial-admin.secret из шага 2. }
    HardenDataDirAcl;
    { 10. В КОНЦЕ: recovery-политика + SQL-зависимость + открытие порта + sc start ПОСЛЕДНИМ. }
    StartServiceAndFinalize;
  end;
end;

{ ===== Деинсталляция: keep-data prompt по ключам/конфигу ===== }

{ Спрашиваем у оператора, удалять ли конфиг и ключи шифрования из
  %ProgramData%\MitLicenseCenter. Дефолт — НЕТ (сохранить): key ring + БД — единый
  бэкап-юнит (ADR-15/CLAUDE.md), без ключей секреты в dbo.Settings не расшифровать.
  БД установщик НЕ трогает (нужны creds + опасно). Служба и firewall-правило снимаются
  секцией [UninstallRun]; ярлык «Пуск» — секцией [UninstallDelete]. }
procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  dataDir: string;
begin
  if CurUninstallStep = usUninstall then
  begin
    dataDir := ExpandConstant('{commonappdata}\MitLicenseCenter');
    if not DirExists(dataDir) then
      Exit;
    { MB_DEFBUTTON2 → дефолтная кнопка «Нет» (сохранить). }
    if MsgBox(
         'Удалить конфигурацию и КЛЮЧИ ШИФРОВАНИЯ из' + #13#10 +
         dataDir + ' ?' + #13#10#13#10 +
         'Нет (рекомендуется) — сохранить для переустановки.' + #13#10#13#10 +
         'Да — удалить ключи. ВНИМАНИЕ: без них секреты в базе данных (dbo.Settings) ' +
         'расшифровать НЕЛЬЗЯ. Удаляйте только если база данных тоже выводится из ' +
         'эксплуатации (ключи шифрования и БД — единый бэкап-юнит).' + #13#10#13#10 +
         'Базу данных SQL Server установщик не трогает — удалите её вручную при ' +
         'необходимости.',
         mbConfirmation, MB_YESNO or MB_DEFBUTTON2) = IDYES then
      DelTree(dataDir, True, True, True);
    { Иначе — оставить каталог (ключи + конфиг) нетронутым. }
  end;
end;

; MitLicense Center — установщик (Inno Setup 6, Unicode).
; Каркас (MLC-100): ставит файлы self-contained артефакта, регистрирует и стартует
; одну Windows-службу, открывает порт в firewall, умеет обновление поверх существующей
; установки (стоп службы -> подмена -> старт), сохраняя appsettings.Production.json,
; Data Protection key ring и БД.
;
; Мастер (MLC-101): интерактивные страницы собирают SQL-инстанс/БД, режим аутентификации
; (Windows-аккаунт ИЛИ SQL-логин), учётные данные и сетевые параметры (порт, AllowedHosts),
; проверяют подключение и генерируют рабочий appsettings.Production.json + настраивают службу
; (под выбранным ОС-аккаунтом или LocalSystem). Аккаунт/логин с правами в SQL (sysadmin)
; создаёт оператор ЗАРАНЕЕ — установщик их потребляет и проверяет, не создаёт.
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
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: recursesubdirs createallsubdirs ignoreversion; Excludes: "appsettings.Production.json"

[Dirs]
; Data Protection key ring живёт здесь (purpose mlc.settings.v1). Под LocalSystem
; (SYSTEM) дефолтных ACL достаточно; под выбранным ОС-аккаунтом (режим A) [Code]
; выдаёт ему явный ACL (icacls) в ssPostInstall. Папку НЕ удаляем при деинсталле.
Name: "{commonappdata}\MitLicenseCenter"; Flags: uninsneveruninstall

[Run]
; --- Регистрация службы ---
; Создание службы — только на чистой установке (на апгрейде ветка ниже обновляет binPath
; и аккаунт). obj=/password= формирует [Code] по выбранному режиму: режим A => указанный
; ОС-аккаунт, режим B => obj не задаём (LocalSystem).
Filename: "{sys}\sc.exe"; \
  Parameters: "{code:GetScCreateParams}"; \
  Flags: runhidden; StatusMsg: "Регистрация службы..."; Check: not ServiceExists
; На апгрейде службу не пересоздаём — выравниваем путь к exe и аккаунт (на случай смены
; режима/{app}). obj=/password= снова формирует [Code].
Filename: "{sys}\sc.exe"; \
  Parameters: "{code:GetScConfigParams}"; \
  Flags: runhidden; StatusMsg: "Обновление службы..."; Check: ServiceExists
Filename: "{sys}\sc.exe"; \
  Parameters: "description {#MyServiceName} ""Панель управления лицензиями 1С (MitLicense Center)"""; \
  Flags: runhidden
; --- Firewall: входящий TCP на выбранный порт панели ---
Filename: "{sys}\netsh.exe"; \
  Parameters: "{code:GetFirewallAddParams}"; \
  Flags: runhidden; StatusMsg: "Открытие порта в брандмауэре..."
; --- Старт службы ---
Filename: "{sys}\sc.exe"; Parameters: "start {#MyServiceName}"; \
  Flags: runhidden; StatusMsg: "Запуск службы..."
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

[UninstallDelete]
; Сгенерированный в ssPostInstall интернет-ярлык + его каталог в меню «Пуск».
Type: filesandordirs; Name: "{commonprograms}\MitLicense Center"

[Code]
const
  SERVICE_QUERY_STATUS = $0004;
  SERVICE_STOP         = $0020;
  SC_MANAGER_CONNECT   = $0001;
  SERVICE_STOPPED      = 1;
  SERVICE_STOP_PENDING = 3;

  { Режимы аутентификации (индекс radio на странице «Аутентификация»). }
  AUTH_WINDOWS = 0;  { Служба бежит под указанным ОС-аккаунтом, Trusted SQL. }
  AUTH_SQL     = 1;  { Служба LocalSystem, SQL-логин в строке подключения. }

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
  { Страница «Аутентификация»: выбор режима. }
  PageAuthMode: TInputOptionWizardPage;
  { Страница «Учётные данные» (поля зависят от режима). }
  PageCreds: TInputQueryWizardPage;
  { Страница «Сеть»: порт + AllowedHosts. }
  PageNet: TInputQueryWizardPage;
  { Страница «Учётная запись администратора»: пароль admin + подтверждение (только чистая
    установка — на апгрейде admin уже создан, не спрашиваем). MLC-102. }
  PageAdmin: TInputQueryWizardPage;
  { Метка-результат теста подключения на странице учётных данных. }
  TestResultLabel: TNewStaticText;
  TestButton: TNewButton;
  { Прошёл ли последний тест подключения (гейт на Next со страницы учётных данных). }
  ConnTestPassed: Boolean;

{ ===== Хелперы доступа к вводу ===== }

function AuthMode: Integer;
begin
  Result := PageAuthMode.SelectedValueIndex;
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
          ControlService(hSvc, SERVICE_STOP, status);
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

{ Собирает строку подключения по режиму. appName уходит в Application Name (Default/Hangfire). }
function BuildConnString(const appName: string): string;
begin
  Result := 'Server=' + SqlInstance + ';Database=' + SqlDatabase + ';';
  if AuthMode = AUTH_WINDOWS then
    Result := Result + 'Trusted_Connection=True;'
  else
    Result := Result + 'User Id=' + CredUser + ';Password=' + CredPassword + ';';
  Result := Result + 'Encrypt=True;TrustServerCertificate=True;Application Name=' + appName;
end;

{ ===== Генерация appsettings.Production.json ===== }

procedure WriteProductionConfig;
var
  path, json, urls: string;
begin
  path := ExpandConstant('{app}\appsettings.Production.json');
  { Апгрейд: НЕ затирать правки оператора (паритет с MLC-100 onlyifdoesntexist). Конфиг
    пишем только если файла ещё нет — на чистой установке из ввода мастера. }
  if FileExists(path) then
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
  секцией Dirs; под ACL %ProgramData%\MitLicenseCenter (режим A — Modify сервис-аккаунту,
  режим B — LocalSystem). UTF-8 без BOM (SaveStringToFile с UTF8=False). Пароль не логируется. }
procedure WriteInitialAdminPassword;
var
  path: string;
begin
  if ServiceExists then
    Exit;  { апгрейд — admin уже создан }
  path := ExpandConstant('{commonappdata}\MitLicenseCenter\initial-admin.secret');
  if not SaveStringToFile(path, AdminPassword, False) then
    MsgBox('Не удалось записать файл с паролем администратора по пути ' + path + '.' + #13#10 +
           'Первый администратор будет создан со случайным паролем — он попадёт в Журнал событий Windows.',
           mbError, MB_OK);
end;

{ ===== Ярлык меню «Пуск» (интернет-ярлык на URL панели) ===== }

{ Создаёт {commonprograms}\MitLicense Center\MitLicense Center.url — интернет-ярлык,
  открывающий панель в браузере по умолчанию. Inno [Icons] не умеет .url с динамическим
  URL (порт из мастера), поэтому пишем .url вручную через SaveStringToFile. Каталог +
  ярлык сносятся при деинсталляции секцией [UninstallDelete]. }
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

{ ===== ACL для ОС-аккаунта (режим A) ===== }

procedure GrantServiceAccountRights;
var
  acct, keyRing: string;
  rc: Integer;
begin
  if AuthMode <> AUTH_WINDOWS then
    Exit;  { LocalSystem — дефолтных ACL достаточно. }

  acct := CredUser;
  keyRing := ExpandConstant('{commonappdata}\MitLicenseCenter');

  { ACL на key ring: Modify, наследуется на под-объекты/контейнеры (OI)(CI). }
  Exec(ExpandConstant('{sys}\icacls.exe'),
       '"' + keyRing + '" /grant "' + CmdQuoteInner(acct) + '":(OI)(CI)M',
       '', SW_HIDE, ewWaitUntilTerminated, rc);

  { Право «Log on as a service» (SeServiceLogonRight) SCM выдаёт сам при sc config
    obj=…/password=… на валидном аккаунте; явная выдача через secedit здесь не делается
    (хрупко на разных локалях). Если SCM не сможет — служба не стартует, оператор выдаёт
    право через secpol.msc (подсказано в OPERATIONS). }
end;

{ ===== Параметры для [Run] (sc/netsh) ===== }

{ sc create … — на чистой установке. Режим A добавляет obj=/password=. }
function GetScCreateParams(Param: string): string;
begin
  Result := 'create {#MyServiceName} binPath= "' +
            ExpandConstant('{app}\{#MyExeName}') + '" start= auto DisplayName= "{#MyAppName}"';
  if AuthMode = AUTH_WINDOWS then
    Result := Result + ' obj= "' + CmdQuoteInner(CredUser) +
              '" password= "' + CmdQuoteInner(CredPassword) + '"';
end;

{ sc config … — на апгрейде: выравниваем binPath + аккаунт под текущий выбор. }
function GetScConfigParams(Param: string): string;
begin
  Result := 'config {#MyServiceName} binPath= "' +
            ExpandConstant('{app}\{#MyExeName}') + '" start= auto';
  if AuthMode = AUTH_WINDOWS then
    Result := Result + ' obj= "' + CmdQuoteInner(CredUser) +
              '" password= "' + CmdQuoteInner(CredPassword) + '"'
  else
    { Вернуть на LocalSystem (на случай смены режима A->B при апгрейде). }
    Result := Result + ' obj= "LocalSystem"';
end;

{ netsh … add rule — порт из ввода. Старое одноимённое правило снимается в
  CurStepChanged(ssPostInstall) ДО этого вызова (идемпотентность при смене порта). }
function GetFirewallAddParams(Param: string): string;
begin
  Result := 'advfirewall firewall add rule name="{#MyFirewallRule}"' +
            ' dir=in action=allow protocol=TCP localport=' + NetPort;
end;

{ ===== Тест подключения (PowerShell + System.Data.SqlClient) ===== }

{ Режим B: полноценный тест введёнными SQL-creds. Режим A: тест достижимости инстанса
  под Integrated Security установщика-админа. Возвращает True при успехе; errMsg — текст. }
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

  { Тест — к master (БД панели может ещё не существовать). }
  if AuthMode = AUTH_WINDOWS then
  begin
    if CredUser = '' then
    begin
      errMsg := 'Не указан аккаунт службы.';
      Exit;
    end;
    connStr := 'Server=' + SqlInstance +
               ';Database=master;Integrated Security=True;Encrypt=True;TrustServerCertificate=True;Connect Timeout=10';
  end
  else
  begin
    if CredUser = '' then
    begin
      errMsg := 'Не указан SQL-логин.';
      Exit;
    end;
    connStr := 'Server=' + SqlInstance +
               ';Database=master;User Id=' + CredUser + ';Password=' + CredPassword +
               ';Encrypt=True;TrustServerCertificate=True;Connect Timeout=10';
  end;

  { Сниппет на System.Data.SqlClient (есть в .NET Framework / PS 5.1). Строку подключения
    вставляем в PowerShell '...'-литерал внутри временного .ps1 (Pascal Script не имеет
    SetEnvironmentVariable) — НЕ в командную строку powershell.exe, поэтому пароль не
    светится в списке процессов; временный скрипт лежит во временном каталоге установщика
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
    if AuthMode = AUTH_WINDOWS then
      TestResultLabel.Caption := 'OK: инстанс достижим. Права сервис-аккаунта в SQL проверятся при первом старте службы (см. Журнал событий Windows).'
    else
      TestResultLabel.Caption := 'OK: подключение SQL-логином успешно.';
    TestResultLabel.Font.Color := clGreen;
  end
  else
  begin
    TestResultLabel.Caption := 'Ошибка: ' + errMsg;
    TestResultLabel.Font.Color := clRed;
  end;
end;

{ ===== Построение страниц мастера ===== }

procedure InitializeWizard;
begin
  ConnTestPassed := False;

  { --- Страница «SQL Server»: инстанс + БД --- }
  PageSql := CreateInputQueryPage(wpSelectDir,
    'SQL Server',
    'Параметры подключения к SQL Server',
    'Укажите экземпляр SQL Server и имя базы данных панели. База создаётся автоматически при первом запуске, если её ещё нет.');
  PageSql.Add('Экземпляр SQL Server (например . или СЕРВЕР\SQLEXPRESS):', False);
  PageSql.Add('Имя базы данных:', False);
  PageSql.Values[0] := '.';
  PageSql.Values[1] := 'MitLicenseCenter';

  { --- Страница «Режим аутентификации» --- }
  PageAuthMode := CreateInputOptionPage(PageSql.ID,
    'Аутентификация в SQL Server',
    'Как панель будет подключаться к SQL Server',
    'Учётную запись с правами в SQL (роль sysadmin на экземпляре) оператор создаёт ЗАРАНЕЕ — установщик её только использует, не создаёт.',
    True, False);
  PageAuthMode.Add('Windows-аутентификация: служба работает под указанным доменным/локальным аккаунтом (Trusted_Connection).');
  PageAuthMode.Add('SQL-аутентификация: служба работает под LocalSystem, подключение SQL-логином и паролем.');
  PageAuthMode.SelectedValueIndex := AUTH_WINDOWS;

  { --- Страница «Учётные данные» --- }
  PageCreds := CreateInputQueryPage(PageAuthMode.ID,
    'Учётные данные',
    'Учётная запись для подключения к SQL Server',
    'Введите учётные данные заранее созданной учётной записи и нажмите «Проверить подключение».');
  PageCreds.Add('Учётная запись (ДОМЕН\Пользователь, .\Пользователь или SQL-логин):', False);
  PageCreds.Add('Пароль:', True);

  { Кнопка теста + метка-результат под полями. }
  TestButton := TNewButton.Create(WizardForm);
  TestButton.Parent := PageCreds.Surface;
  TestButton.Caption := 'Проверить подключение';
  TestButton.Width := ScaleX(150);
  TestButton.Height := ScaleY(25);
  TestButton.Left := 0;
  TestButton.Top := PageCreds.Edits[1].Top + PageCreds.Edits[1].Height + ScaleY(16);
  TestButton.OnClick := @TestButtonClick;

  TestResultLabel := TNewStaticText.Create(WizardForm);
  TestResultLabel.Parent := PageCreds.Surface;
  TestResultLabel.Left := 0;
  TestResultLabel.Top := TestButton.Top + TestButton.Height + ScaleY(10);
  TestResultLabel.Width := PageCreds.SurfaceWidth;
  TestResultLabel.AutoSize := False;
  TestResultLabel.Height := ScaleY(60);
  TestResultLabel.WordWrap := True;
  TestResultLabel.Caption := '';

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

{ Страница пароля admin — только на чистой установке: на апгрейде admin уже создан в БД,
  заново его не спрашиваем (паритет с ServiceExists = апгрейд). MLC-102. }
function ShouldSkipPage(PageID: Integer): Boolean;
begin
  Result := False;
  if (PageAdmin <> nil) and (PageID = PageAdmin.ID) then
    Result := ServiceExists;
end;

{ Сброс результата теста при заходе на страницу учётных данных + подсказки по режиму. }
procedure CurPageChanged(CurPageID: Integer);
begin
  if (PageCreds <> nil) and (CurPageID = PageCreds.ID) then
  begin
    ConnTestPassed := False;
    if TestResultLabel <> nil then
    begin
      TestResultLabel.Caption := '';
      TestResultLabel.Font.Color := clNavy;
    end;
    if AuthMode = AUTH_WINDOWS then
    begin
      PageCreds.PromptLabels[0].Caption := 'Аккаунт службы (ДОМЕН\Пользователь или .\Пользователь):';
      PageCreds.SubCaptionLabel.Caption :=
        'Аккаунт создан заранее, имеет права в SQL (Trusted) и право входа как служба. Служба будет работать под ним.';
    end
    else
    begin
      PageCreds.PromptLabels[0].Caption := 'SQL-логин:';
      PageCreds.SubCaptionLabel.Caption :=
        'SQL-логин создан заранее с нужными правами. Служба работает под LocalSystem, подключается этим логином.';
    end;
  end;

  { Финальный экран: подтверждаем вход и даём URL. Пароль admin НЕ показываем — его задал
    оператор (на апгрейде admin уже был). MLC-102. }
  if CurPageID = wpFinished then
  begin
    if ServiceExists then
      WizardForm.FinishedLabel.Caption :=
        'Панель обновлена и запущена.' + #13#10#13#10 +
        'Откройте панель: ' + PanelUrl('') + #13#10 +
        'Учётные записи администраторов сохранены.'
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

  { --- Учётные данные --- }
  if (PageCreds <> nil) and (CurPageID = PageCreds.ID) then
  begin
    if CredUser = '' then
    begin
      MsgBox('Укажите учётную запись.', mbError, MB_OK);
      Result := False;
      Exit;
    end;
    if CredPassword = '' then
    begin
      MsgBox('Укажите пароль.', mbError, MB_OK);
      Result := False;
      Exit;
    end;
    { Гейт: требуем успешный тест подключения. Если ещё не тестировали или провалили —
      запускаем тест сейчас и блокируем при ошибке. }
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
               'Исправьте данные и повторите. Продолжить установку нельзя без успешной проверки.',
               mbError, MB_OK);
        Result := False;
        Exit;
      end;
      ConnTestPassed := True;
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

{ После копирования файлов: записать конфиг, выдать ACL ОС-аккаунту, снести старое
  firewall-правило (перед add из [Run] — идемпотентность при смене порта на апгрейде). }
procedure CurStepChanged(CurStep: TSetupStep);
var
  rc: Integer;
begin
  if CurStep = ssPostInstall then
  begin
    WriteProductionConfig;
    { Пароль admin — ДО выдачи ACL: icacls на каталог (OI)(CI) затем накроет и этот файл,
      чтобы сервис-аккаунт (режим A) мог прочитать и удалить его при первом старте. }
    WriteInitialAdminPassword;
    GrantServiceAccountRights;
    { Снять одноимённое firewall-правило, чтобы add из [Run] не плодил дубли и применил
      актуальный порт. Игнорируем результат (правила может не быть на чистой установке). }
    Exec(ExpandConstant('{sys}\netsh.exe'),
         'advfirewall firewall delete rule name="{#MyFirewallRule}"',
         '', SW_HIDE, ewWaitUntilTerminated, rc);
    { Ярлык меню «Пуск» на URL панели (порт из ввода мастера). }
    CreateStartMenuShortcut;
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

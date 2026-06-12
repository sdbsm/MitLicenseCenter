; MitLicense Center — установщик (Inno Setup 6, Unicode).
; Каркас (MLC-100): ставит файлы self-contained артефакта, регистрирует и стартует
; одну Windows-службу, открывает порт в firewall, умеет обновление поверх существующей
; установки (стоп службы -> подмена -> старт), сохраняя appsettings.Production.json,
; Data Protection key ring и БД.
;
; Защита секретов на диске (MLC-110): после раскладки файлов [Code] ужесточает NTFS ACL
; в ОБОИХ режимах аутентификации (icacls /inheritance:r — режем наследование от ProgramData/
; Program Files, где Users:RX):
;   - каталог %ProgramData%\MitLicenseCenter (Data Protection key ring + одноразовый
;     initial-admin.secret) — доступ только SYSTEM / Administrators (+ сервис-аккаунт в режиме A);
;   - {app}\appsettings.Production.json (plaintext SQL-пароль) — только SYSTEM / Administrators
;     (+ сервис-аккаунт R в режиме A).
; Key ring НАМЕРЕННО не шифруется at-rest (ADR-8): ключи переносимы, бэкап «key ring + БД»
; восстанавливается на новом железе/аккаунте; защита ключей — именно эти NTFS ACL.
; Сбой icacls предупреждает (с путём), но НЕ прерывает установку — служба работоспособна,
; страдает только hardening. ACL идемпотентны (накатываются и на чистой установке, и на апгрейде).
;
; Мастер (MLC-101): интерактивные страницы собирают SQL-инстанс/БД, режим аутентификации
; (Windows-аккаунт ИЛИ SQL-логин), учётные данные и сетевые параметры (порт, AllowedHosts),
; проверяют подключение и генерируют рабочий appsettings.Production.json + настраивают службу
; (под выбранным ОС-аккаунтом или LocalSystem). Аккаунт/логин с правами в SQL (sysadmin)
; создаёт оператор ЗАРАНЕЕ — установщик их потребляет и проверяет, не создаёт.
;
; Надёжность (MLC-107): создание/конфиг/старт службы и firewall выполняются в [Code]
; (ConfigureService в ssPostInstall) с проверкой кода возврата sc.exe — ошибка создания
; (например 1057: в режиме Windows введён SQL-логин) прерывает установку с подсказкой, а не
; завершает её «успехом» без службы. appsettings.Production.json удаляется при деинсталле и
; на чистой установке перезаписывается из ввода (skip-if-exists — только на апгрейде).
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
; Data Protection key ring живёт здесь (purpose mlc.settings.v1) + одноразовый
; initial-admin.secret. Дефолтные ACL ProgramData (Users:RX) НЕДОСТАТОЧНЫ (MLC-110):
; [Code] (HardenDataDirAcl в ssPostInstall) режет наследование и оставляет доступ ТОЛЬКО
; SYSTEM / Administrators — в ОБОИХ режимах; в режиме A дополнительно даёт сервис-аккаунту
; Modify. Папку НЕ удаляем при деинсталле.
Name: "{commonappdata}\MitLicenseCenter"; Flags: uninsneveruninstall

[Run]
; Регистрация/конфиг/старт службы и открытие firewall-порта выполняются в [Code]
; (процедура ConfigureService в CurStepChanged(ssPostInstall)) — с проверкой кода возврата
; sc.exe и внятным сообщением при ошибке (MLC-107). Раньше эти шаги жили здесь, в [Run], и
; их провал проходил молча: например sc create => 1057, когда в режиме Windows-аутентификации
; введён SQL-логин (sa), завершал установку «успешно» БЕЗ службы. Теперь create проверяется и
; прерывает установку с подсказкой, а старт — предупреждает и отсылает в Журнал событий.
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
; appsettings.Production.json генерится в [Code] (SaveStringToFile), а не секцией [Files],
; поэтому деинсталлятор сам его не удаляет — сносим явно, иначе при переустановке остаётся
; старый конфиг от прошлой установки (MLC-107). Key ring/БД здесь НЕ трогаем.
Type: files; Name: "{app}\appsettings.Production.json"

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
  { Старт службы провалился в ConfigureService (sc start rc<>0). Финальный экран wpFinished
    при True рапортует ОШИБКУ, а не «успех» — провал старта не должен выглядеть как успех
    (MLC-107 показывал MsgBox-предупреждение, но финал всё равно рапортовал «запущена»).
    Инициализируется False в InitializeWizard. MLC-112 (REL-02). }
  ServiceStartFailed: Boolean;

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
  только SYSTEM/Administrators (+ сервис-аккаунт Modify в режиме A), Users отрезаны.
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
  Administrators (+ сервис-аккаунт Modify в режиме A). Вызывается ПОСЛЕ записи
  initial-admin.secret, чтобы (OI)(CI) накрыл и его. Идемпотентно (чистая установка + апгрейд). }
procedure HardenDataDirAcl;
var
  dataDir, args: string;
begin
  dataDir := ExpandConstant('{commonappdata}\MitLicenseCenter');
  if not DirExists(dataDir) then
    Exit;
  { /inheritance:r снимает унаследованные ACE; затем явные full для SYSTEM/Administrators,
    наследуемые на под-объекты/контейнеры (OI)(CI). }
  args := '/inheritance:r' +
          ' /grant *S-1-5-18:(OI)(CI)F' +
          ' /grant *S-1-5-32-544:(OI)(CI)F';
  if AuthMode = AUTH_WINDOWS then
    args := args + ' /grant "' + CmdQuoteInner(CredUser) + '":(OI)(CI)M';
  RunIcacls(dataDir, args, 'каталог данных');

  { Право «Log on as a service» (SeServiceLogonRight) SCM выдаёт сам при sc config
    obj=…/password=… на валидном аккаунте; явная выдача через secedit здесь не делается
    (хрупко на разных локалях). Если SCM не сможет — служба не стартует, оператор выдаёт
    право через secpol.msc (подсказано в OPERATIONS). }
end;

{ ACL файла appsettings.Production.json в каталоге установки (plaintext SQL-пароль). Режем
  наследование (Users:RX от Program Files) — только SYSTEM / Administrators full; в режиме A
  службе достаточно Read. Вызывается ПОСЛЕ WriteProductionConfig. Применять и на апгрейде:
  файл сохраняется от прошлой установки и не перезаписывается, но ACL всё равно накатываем. }
procedure HardenConfigAcl;
var
  cfg, args: string;
begin
  cfg := ExpandConstant('{app}\appsettings.Production.json');
  if not FileExists(cfg) then
    Exit;
  args := '/inheritance:r' +
          ' /grant *S-1-5-18:F' +
          ' /grant *S-1-5-32-544:F';
  if AuthMode = AUTH_WINDOWS then
    args := args + ' /grant "' + CmdQuoteInner(CredUser) + '":R';
  RunIcacls(cfg, args, 'конфиг');
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

{ netsh … add rule — порт из ввода. Старое одноимённое правило снимается в ConfigureService
  ДО этого вызова (идемпотентность при смене порта). }
function GetFirewallAddParams(Param: string): string;
begin
  Result := 'advfirewall firewall add rule name="{#MyFirewallRule}"' +
            ' dir=in action=allow protocol=TCP localport=' + NetPort;
end;

{ ===== Регистрация/конфиг/старт службы + firewall (MLC-107) ===== }

{ Создание/конфиг/старт службы и открытие firewall-порта выполняются здесь, в Code-секции, а
  не в Run-секции — чтобы проверять код возврата sc.exe и при ошибке внятно сообщать оператору
  (а не завершать установку «успешно» без службы). Вызывается в КОНЦЕ CurStepChanged(ssPostInstall),
  ПОСЛЕ WriteProductionConfig/WriteInitialAdminPassword/GrantServiceAccountRights и снятия старого
  firewall-правила. Запускает sc.exe / netsh.exe (из System32) через Exec с проверкой rc.
  Контракт ошибок:
    - sc create (чистая установка): rc<>0 -> MsgBox с подсказкой (особо 1057 — Windows-режим
      выбран для SQL-логина) и прерывание установки исключением (откат);
    - sc config (апгрейд): rc<>0 -> MsgBox-предупреждение (служба остаётся, но аккаунт/путь
      могли не примениться);
    - sc description: rc не валидируем (косметика);
    - netsh add: rc<>0 -> предупреждение (порт мог не открыться);
    - sc start: rc<>0 -> предупреждение со ссылкой на Журнал событий (fail-fast bootstrap,
      ADR-18), без Abort — служба уже создана.
  Пароли (obj password / SQL) в сообщениях не фигурируют. }
procedure ConfigureService;
var
  rc: Integer;
  startWarn: string;
begin
  if not ServiceExists then
  begin
    { --- Чистая установка: создаём службу --- }
    if (not Exec(ExpandConstant('{sys}\sc.exe'), GetScCreateParams(''),
                 '', SW_HIDE, ewWaitUntilTerminated, rc)) or (rc <> 0) then
    begin
      if rc = 1057 then
        { 1057 = ERROR_INVALID_SERVICE_ACCOUNT: для obj= указан невалидный аккаунт службы.
          Типичная причина — выбран режим Windows-аутентификации, а введён SQL-логин (sa). }
        MsgBox('Не удалось создать службу (код 1057 — недопустимая учётная запись службы).' + #13#10#13#10 +
               'Чаще всего это значит, что на шаге «Аутентификация» выбрана Windows-аутентификация (A), ' +
               'но в учётных данных введён SQL-логин (например sa), а не Windows-аккаунт.' + #13#10#13#10 +
               'Если для подключения к SQL используется SQL-логин — вернитесь и выберите ' +
               '«(B) SQL-аутентификация» (служба будет работать под LocalSystem).' + #13#10#13#10 +
               'Если используется Windows-аккаунт — проверьте имя (ДОМЕН\Пользователь или .\Пользователь) ' +
               'и пароль. Подробности — в логе установки.',
               mbCriticalError, MB_OK)
      else
        MsgBox('Не удалось создать службу «{#MyServiceName}» (код ' + IntToStr(rc) + ').' + #13#10#13#10 +
               'Если для подключения к SQL используется SQL-логин (например sa) — на шаге ' +
               '«Аутентификация» выберите «(B) SQL-аутентификация», а не Windows. ' +
               'Подробности — в логе установки.',
               mbCriticalError, MB_OK);
      { Жёсткое прерывание: исключение из CurStepChanged откатывает установку — служба не
        создалась, нельзя завершать «успехом». }
      RaiseException('Создание службы «{#MyServiceName}» завершилось с кодом ' + IntToStr(rc) + '.');
    end;
  end
  else
  begin
    { --- Апгрейд: пересоздавать службу не нужно, выравниваем binPath/аккаунт --- }
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

  { --- Firewall: входящий TCP на выбранный порт --- }
  if (not Exec(ExpandConstant('{sys}\netsh.exe'), GetFirewallAddParams(''),
               '', SW_HIDE, ewWaitUntilTerminated, rc)) or (rc <> 0) then
    MsgBox('Не удалось открыть TCP-порт ' + NetPort + ' в брандмауэре Windows (код ' + IntToStr(rc) + ').' + #13#10 +
           'Панель установлена, но может быть недоступна по сети — откройте порт вручную ' +
           '(брандмауэр Windows) или проверьте лог установки.',
           mbError, MB_OK);

  { --- Старт службы: на провале предупреждаем (без Abort — служба создана) --- }
  if (not Exec(ExpandConstant('{sys}\sc.exe'), 'start {#MyServiceName}',
               '', SW_HIDE, ewWaitUntilTerminated, rc)) or (rc <> 0) then
  begin
    startWarn :=
      'Служба «{#MyServiceName}» создана, но не запустилась (код ' + IntToStr(rc) + ').' + #13#10#13#10 +
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

{ ===== Проба: целевая БД уже содержит установку панели? (MLC-105) ===== }

{ Тем же приёмом, что TestSqlConnection (временный .ps1 с connstr в '...'-литерале,
  powershell -File, файл удаляется), но connstr на выбранную БД (НЕ master). PS-сниппет
  открывает соединение и выполняет:
    IF OBJECT_ID('auth.Users','U') IS NULL SELECT 0 ELSE SELECT COUNT(*) FROM auth.Users
  и пишет число в stdout. Зеркалит условие сидера userManager.Users.AnyAsync (любой
  пользователь, не только admin). Fail-open: БД недоступна (ещё не создана) / ошибка
  запроса / соединение не открылось -> вернуть False (трактуем как «не инициализирована»),
  чтобы не было хуже текущего поведения. Креды/режим — как в тесте: B — SQL-логин;
  A — Integrated Security установщика-админа. }
function DatabaseHasPanelUsers: Boolean;
var
  connStr, psScript, scriptPath, outPath, cmdLine: string;
  outData: AnsiString;  { LoadStringFromFile требует var AnsiString — не String. }
  rc: Integer;
begin
  Result := False;

  if (SqlInstance = '') or (SqlDatabase = '') then
    Exit;

  if AuthMode = AUTH_WINDOWS then
    connStr := 'Server=' + SqlInstance +
               ';Database=' + SqlDatabase +
               ';Integrated Security=True;Encrypt=True;TrustServerCertificate=True;Connect Timeout=10'
  else
    connStr := 'Server=' + SqlInstance +
               ';Database=' + SqlDatabase +
               ';User Id=' + CredUser + ';Password=' + CredPassword +
               ';Encrypt=True;TrustServerCertificate=True;Connect Timeout=10';

  { Результат запроса пишем в файл (не в stdout) — exit-код только индикатор успеха.
    connStr в '...'-литерале временного .ps1 (пароль не попадает в командную строку). }
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
  DbAlreadyInitialized := False;
  DbInitWarningShown := False;
  ServiceStartFailed := False;

  { --- Страница «Резервная копия перед апгрейдом» (MLC-112, только апгрейд) ---
    Показывается ДО замены файлов: апгрейд подменяет файлы новой версией, а на старте новая
    версия применяет миграции БД; согласованного автоматического отката файлы<->БД нет.
    Поэтому требуем явное подтверждение, что оператор сделал бэкап (1) базы данных SQL и
    (2) каталога ключей шифрования (key ring) — без них секреты в БД не расшифровать (единый
    бэкап-юнит). Размещаем рано — сразу после wpSelectDir, ДО PageSql, чтобы требование о
    бэкапе встретило оператора прежде настройки подключения. На чистой установке страница
    пропускается через ShouldSkipPage (not ServiceExists). Гейт «Далее» — в NextButtonClick:
    без отметки чекбокса дальше не пускаем. }
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

  { --- Страница «Режим аутентификации» --- }
  PageAuthMode := CreateInputOptionPage(PageSql.ID,
    'Аутентификация в SQL Server',
    'Как панель будет подключаться к SQL Server',
    'Учётную запись с правами в SQL (роль sysadmin на экземпляре) оператор создаёт ЗАРАНЕЕ — установщик её только использует, не создаёт.',
    True, False);
  PageAuthMode.Add('(A) Windows-аутентификация — служба работает под доменным/локальным Windows-аккаунтом (Trusted_Connection). Выберите, если вводите имя Windows-учётной записи (ДОМЕН\Пользователь или .\Пользователь).');
  PageAuthMode.Add('(B) SQL-аутентификация — служба работает под LocalSystem, подключение SQL-логином (например sa) и паролем. Выберите, если вводите SQL-логин, а не Windows-аккаунт.');
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
      PageCreds.PromptLabels[0].Caption := 'Windows-аккаунт службы (ДОМЕН\Пользователь или .\Пользователь):';
      PageCreds.SubCaptionLabel.Caption :=
        'Режим (A) Windows-аутентификация. Введите Windows-учётную запись (НЕ SQL-логин): она создана заранее, ' +
        'имеет права в SQL (Trusted) и право входа как служба — служба будет работать под ней. ' +
        'Если у вас SQL-логин (например sa) — вернитесь назад и выберите (B) SQL-аутентификация.';
    end
    else
    begin
      PageCreds.PromptLabels[0].Caption := 'SQL-логин (например sa):';
      PageCreds.SubCaptionLabel.Caption :=
        'Режим (B) SQL-аутентификация. Введите SQL-логин (НЕ Windows-аккаунт): он создан заранее с нужными правами. ' +
        'Служба работает под LocalSystem и подключается этим логином и паролем.';
    end;
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
      WizardForm.FinishedLabel.Caption :=
        'Служба «{#MyServiceName}» создана/сохранена, но НЕ запустилась — панель сейчас ' +
        'недоступна.' + #13#10#13#10 +
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

{ После копирования файлов: записать конфиг, ужесточить NTFS ACL на секреты, снести старое
  firewall-правило, затем зарегистрировать/настроить/запустить службу и открыть порт. }
procedure CurStepChanged(CurStep: TSetupStep);
var
  rc: Integer;
begin
  if CurStep = ssPostInstall then
  begin
    WriteProductionConfig;
    { ACL конфига — СРАЗУ после его записи (MLC-110): только SYSTEM/Administrators (+ сервис-
      аккаунт R в режиме A); plaintext SQL-пароль не должен читаться Users. }
    HardenConfigAcl;
    { Пароль admin — ДО ужесточения ACL каталога: icacls (OI)(CI) затем накроет и этот файл,
      чтобы сервис-аккаунт (режим A) мог прочитать и удалить его при первом старте. }
    WriteInitialAdminPassword;
    { ACL каталога данных — ПОСЛЕ записи initial-admin.secret (MLC-110): режем наследование,
      доступ только SYSTEM/Administrators (+ сервис-аккаунт Modify в режиме A). }
    HardenDataDirAcl;
    { Снять одноимённое firewall-правило, чтобы add в ConfigureService не плодил дубли и
      применил актуальный порт. Игнорируем результат (правила может не быть на чистой установке). }
    Exec(ExpandConstant('{sys}\netsh.exe'),
         'advfirewall firewall delete rule name="{#MyFirewallRule}"',
         '', SW_HIDE, ewWaitUntilTerminated, rc);
    { Ярлык меню «Пуск» на URL панели (порт из ввода мастера). }
    CreateStartMenuShortcut;
    { В КОНЦЕ: создание/конфиг/старт службы + firewall с проверкой rc и внятными ошибками.
      На провале create бросает исключение -> установка откатывается (MLC-107). }
    ConfigureService;
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

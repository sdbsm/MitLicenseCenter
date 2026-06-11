; MitLicense Center — установщик (Inno Setup 6, Unicode).
; Каркас (MLC-100): ставит файлы self-contained артефакта, регистрирует и стартует
; одну Windows-службу под LocalSystem, открывает порт в firewall, умеет обновление
; поверх существующей установки (стоп службы -> подмена -> старт), сохраняя
; appsettings.Production.json, Data Protection key ring и БД.
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
#define MyHttpPort "8080"

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

[Languages]
Name: "russian"; MessagesFile: "compiler:Languages\Russian.isl"

[Files]
; Self-contained артефакт целиком, КРОМЕ appsettings.Production.json — его кладём
; отдельно ниже с onlyifdoesntexist, чтобы апгрейд не затирал правки оператора.
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: recursesubdirs createallsubdirs ignoreversion; Excludes: "appsettings.Production.json"
; Дефолт-конфиг каркаса (localhost SQL, порт 8080). На апгрейде НЕ перезаписывается.
Source: "appsettings.Production.default.json"; DestDir: "{app}"; DestName: "appsettings.Production.json"; Flags: onlyifdoesntexist

[Dirs]
; Data Protection key ring живёт здесь (purpose mlc.settings.v1). Под LocalSystem
; (SYSTEM) дефолтных ACL достаточно. Папку НЕ удаляем при деинсталле (uninsneveruninstall).
Name: "{commonappdata}\MitLicenseCenter"; Flags: uninsneveruninstall

[Run]
; --- Регистрация службы ---
; Создание службы — только на чистой установке (на апгрейде ветка ниже обновляет binPath).
; obj не указываем => служба работает под LocalSystem.
Filename: "{sys}\sc.exe"; \
  Parameters: "create {#MyServiceName} binPath= ""{app}\{#MyExeName}"" start= auto DisplayName= ""{#MyAppName}"""; \
  Flags: runhidden; StatusMsg: "Регистрация службы..."; Check: not ServiceExists
; На апгрейде службу не пересоздаём — только выравниваем путь к exe на случай смены {app}.
Filename: "{sys}\sc.exe"; \
  Parameters: "config {#MyServiceName} binPath= ""{app}\{#MyExeName}"" start= auto"; \
  Flags: runhidden; StatusMsg: "Обновление службы..."; Check: ServiceExists
Filename: "{sys}\sc.exe"; \
  Parameters: "description {#MyServiceName} ""Панель управления лицензиями 1С (MitLicense Center)"""; \
  Flags: runhidden
; --- Firewall: входящий TCP на порт панели ---
Filename: "{sys}\netsh.exe"; \
  Parameters: "advfirewall firewall add rule name=""{#MyFirewallRule}"" dir=in action=allow protocol=TCP localport={#MyHttpPort}"; \
  Flags: runhidden; StatusMsg: "Открытие порта в брандмауэре..."
; --- Старт службы ---
Filename: "{sys}\sc.exe"; Parameters: "start {#MyServiceName}"; \
  Flags: runhidden; StatusMsg: "Запуск службы..."

[UninstallRun]
; Стоп + удаление службы. RunOnceId, чтобы шаги не дублировались.
Filename: "{sys}\sc.exe"; Parameters: "stop {#MyServiceName}"; Flags: runhidden; RunOnceId: "StopSvc"
Filename: "{sys}\sc.exe"; Parameters: "delete {#MyServiceName}"; Flags: runhidden; RunOnceId: "DeleteSvc"
; Удаление firewall-правила.
Filename: "{sys}\netsh.exe"; \
  Parameters: "advfirewall firewall delete rule name=""{#MyFirewallRule}"""; \
  Flags: runhidden; RunOnceId: "DeleteFwRule"
; БД и {commonappdata}\MitLicenseCenter\keys (key ring) НЕ трогаем — keep-data
; полировка вынесена в MLC-103.

[Code]
const
  SERVICE_QUERY_STATUS = $0004;
  SERVICE_STOP         = $0020;
  SC_MANAGER_CONNECT   = $0001;
  SERVICE_STOPPED      = 1;
  SERVICE_STOP_PENDING = 3;

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

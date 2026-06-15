const
  SERVICE_QUERY_STATUS = $0004;
  SERVICE_STOP         = $0020;        { access right для OpenService }
  SERVICE_CONTROL_STOP = $00000001;    { control-код для ControlService (MLC-116): НЕ путать с SERVICE_STOP — раньше в ControlService ошибочно передавался access right $0020, функция возвращала ERROR_INVALID_PARAMETER и служба НЕ останавливалась }
  SC_MANAGER_CONNECT   = $0001;
  SERVICE_STOPPED      = 1;

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
  { Страница «Подключение установщика к SQL» (MLC-175): ЯВНЫЙ выбор личности провижининга радио-
    кнопками — index = PROV_INTEGRATED (Integrated Security, по умолчанию) / PROV_SQLLOGIN (SQL-логин
    sysadmin). Раньше режим выводился неявно из пустоты поля логина (MLC-172) — теперь явный выбор.
    Радио и поля ввода разнесены на ДВЕ страницы (как PageAuthMode→PageCreds): кастомные input-контролы
    на CreateInputOptionPage не отрисовываются (баг MLC-172), поэтому креды — на отдельной странице. }
  PageProvMode: TInputOptionWizardPage;
  { Страница «Учётные данные SQL-логина» — показывается ТОЛЬКО при PROV_SQLLOGIN (ShouldSkipPage).
    Values[0] = SQL-логин (sysadmin), Values[1] = пароль (маскируется флагом в .Add). }
  PageProvCreds: TInputQueryWizardPage;
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
  { Прошёл ли последний тест подключения (гейт на Next со страниц провижининга). MLC-175:
    проверка авто-выполняется при «Далее» (отдельной кнопки больше нет). }
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


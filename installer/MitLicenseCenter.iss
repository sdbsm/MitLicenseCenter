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
{ Тело секции [Code] разнесено по installer/include/*.iss и подключается ниже в текущем
  порядке. ISPP #include дословно вставляет строки файла — препроцессор собирает тот же
  поток, что и прежний монолит (компилируемый результат идентичен). Порядок менять нельзя:
  объявления (const/type/var) обязаны идти раньше использований.
    1. code-declarations    — const/type, external advapi32, var-глобалы страниц мастера
    2. code-input-accessors — аксессоры ввода мастера (AccountMode, SqlInstance, AdminPassword, …)
    3. code-service-control — ServiceExists, StopServiceAndWait, PrepareToInstall
    4. code-strings-connstr — экранирование, URL панели, строки подключения
    5. code-config-acl      — appsettings.Production.json, .secret, ярлык «Пуск», NTFS-ACL
    6. code-service-sql     — sc create/config, провижининг SQL-логина, firewall, старт службы
    7. code-sql-test        — тест подключения, проба БД, кнопка «Проверить подключение»
    8. code-wizard          — InitializeWizard, навигация страниц, CurStepChanged, деинсталляция }

#include "include\code-declarations.iss"
#include "include\code-input-accessors.iss"
#include "include\code-service-control.iss"
#include "include\code-strings-connstr.iss"
#include "include\code-config-acl.iss"
#include "include\code-service-sql.iss"
#include "include\code-sql-test.iss"
#include "include\code-wizard.iss"

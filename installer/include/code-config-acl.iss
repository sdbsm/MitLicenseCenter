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


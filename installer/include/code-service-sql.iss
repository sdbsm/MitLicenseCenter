{ ===== Запуск временного PowerShell-сценария (общий помощник) ===== }

{ Все SQL/provisioning-шаги (тест подключения, проба БД, создание SQL-логина, добавление учётки
  в Администраторов) запускают записанный во временный .ps1 сценарий ОДИНАКОВО: powershell.exe по
  полному пути (без PATH-hijack), -NoProfile -ExecutionPolicy Bypass -File. Сценарий может нести
  строку подключения с паролем в '...'-литерале, поэтому файл ВСЕГДА удаляется сразу после запуска
  (и при неудачном запуске тоже). Возвращает True, если powershell удалось запустить (тогда rc —
  код возврата сценария); False, если запуск не удался. Запись сценария и реакцию на rc держит у
  себя вызывающий — они различаются (fail-open / предупреждение / жёсткий откат). }
function RunPowerShellFile(const scriptPath: string; out rc: Integer): Boolean;
begin
  Result := Exec(ExpandConstant('{sys}\WindowsPowerShell\v1.0\powershell.exe'),
                 '-NoProfile -ExecutionPolicy Bypass -File "' + scriptPath + '"',
                 '', SW_HIDE, ewWaitUntilTerminated, rc);
  DeleteFile(scriptPath);
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

{ Общий хвост sc create/config (ADR-49): obj= <учётка службы> [password= …]. password= только
  для обычной именованной Windows-учётки с паролем (ServiceAccountUsesPassword). Для виртуальной
  учётки «NT SERVICE\MitLicenseCenter» и для gMSA пароль НЕ передаётся (им управляет Windows; SCM
  сам материализует учётку/SID и выдаёт SeServiceLogonRight). }
function ScObjectTail: string;
begin
  Result := ' obj= "' + CmdQuoteInner(ServiceAccountName) + '"';
  if ServiceAccountUsesPassword then
    Result := Result + ' password= "' + CmdQuoteInner(CredPassword) + '"';
end;

{ sc create … — на чистой установке (ADR-49). binPath/start/DisplayName + общий хвост учётки. }
function GetScCreateParams: string;
begin
  Result := 'create {#MyServiceName} binPath= "' +
            ExpandConstant('{app}\{#MyExeName}') + '" start= auto DisplayName= "{#MyAppName}"' +
            ScObjectTail;
end;

{ sc config … — на апгрейде: выравниваем binPath + учётку под текущий выбор (ADR-49). }
function GetScConfigParams: string;
begin
  Result := 'config {#MyServiceName} binPath= "' +
            ExpandConstant('{app}\{#MyExeName}') + '" start= auto' +
            ScObjectTail;
end;

{ netsh … add rule — порт из ввода. Старое одноимённое правило снимается в CurStepChanged
  (ssPostInstall, шаг 3) ДО этого вызова (идемпотентность при смене порта).
  SEC-06 (MLC-126): правило открывается ТОЛЬКО на профилях Domain/Private (штатный LAN-сценарий),
  НЕ на Public (недоверенные сети) — порт остаётся закрыт на публичных подключениях, без регресса
  для домена/частной сети. remoteip= намеренно НЕ задаём: localsubnet сломал бы LAN с несколькими
  подсетями; сужение по источнику документируется как опция в OPERATIONS (SEC-09). }
function GetFirewallAddParams: string;
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
  connStr, acct, psScript, scriptPath: string;
  rc: Integer;
begin
  acct := ServiceAccountName;
  { connstr к master под выбранной личностью провижининга (MLC-171): Integrated Security
    запускающего админа (дефолт) либо введённый SQL-логин с ролью sysadmin. SQL-логин
    транзиентен — в строке только для этого разового вызова, в конфиг не пишется. }
  connStr := ProvisioningConnString('master');

  { Имя учётки уходит в SqlParameter @p_acct (ADO.NET), внутри T-SQL присваивается в @acct и
    подставляется через QUOTENAME (DDL не принимает переменную напрямую — нельзя параметризовать
    CREATE LOGIN / ALTER SERVER ROLE). Динамический statement СНАЧАЛА собирается в @sql, и только
    потом EXEC sys.sp_executesql @sql (MLC-172): конкатенацию НЕЛЬЗЯ передавать прямо в аргумент
    sp_executesql — `EXEC sp_executesql N''..''+QUOTENAME(..)` даёт «Неправильный синтаксис около
    "+"» (первый аргумент proc-у — только переменная/литерал, не выражение). Имя учётки в командную
    строку НЕ попадает. T-SQL — одной PowerShell '...'-строкой (как в DatabaseHasPanelUsers):
    одиночные кавычки T-SQL удвоены до '' (для PowerShell-литерала); чтобы получить N'...'-строку,
    исходная T-SQL-кавычка '...' пишется как '''' в PS-литерале. }
  psScript :=
    '$ErrorActionPreference=''Stop'';' + #13#10 +
    '$cs = ''' + PsSingleQuote(connStr) + ''';' + #13#10 +
    '$acct = ''' + PsSingleQuote(acct) + ''';' + #13#10 +
    '$tsql = ''DECLARE @acct sysname = @p_acct; DECLARE @sql nvarchar(max);' +
      ' IF NOT EXISTS (SELECT 1 FROM sys.server_principals WHERE name = @acct)' +
      ' BEGIN SET @sql = N''''CREATE LOGIN ''''+QUOTENAME(@acct)+N'''' FROM WINDOWS;'''';' +
      ' EXEC sys.sp_executesql @sql; END' +
      ' IF NOT EXISTS (SELECT 1 FROM sys.server_role_members rm' +
      ' JOIN sys.server_principals r ON r.principal_id=rm.role_principal_id AND r.name=N''''sysadmin''''' +
      ' JOIN sys.server_principals m ON m.principal_id=rm.member_principal_id AND m.name=@acct)' +
      ' BEGIN SET @sql = N''''ALTER SERVER ROLE sysadmin ADD MEMBER ''''+QUOTENAME(@acct)+N'''';'''';' +
      ' EXEC sys.sp_executesql @sql; END'';' + #13#10 +
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

  if (not RunPowerShellFile(scriptPath, rc)) or (rc <> 0) then
  begin
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
  acct, psScript, scriptPath: string;
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

  if (not RunPowerShellFile(scriptPath, rc)) or (rc <> 0) then
  begin
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
    if (not Exec(ExpandConstant('{sys}\sc.exe'), GetScCreateParams,
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
    if (not Exec(ExpandConstant('{sys}\sc.exe'), GetScConfigParams,
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
  if (not Exec(ExpandConstant('{sys}\netsh.exe'), GetFirewallAddParams,
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


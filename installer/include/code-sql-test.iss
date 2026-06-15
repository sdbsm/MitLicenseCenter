{ ===== Тест подключения (PowerShell + System.Data.SqlClient) ===== }

{ Тест достижимости инстанса под ВЫБРАННОЙ личностью провижининга (MLC-171): Integrated
  Security установщика-админа (дефолт) либо введённый SQL-логин с ролью sysadmin. SQL-логин
  учётки службы создаётся установщиком позже (ProvisionSqlLogin) — тест лишь проверяет, что
  инстанс доступен под той же личностью, которой пойдёт провижининг (тестируем тем же, чем
  создаём). Возвращает True при успехе; errMsg — текст. }
function TestSqlConnection(out errMsg: string): Boolean;
var
  connStr, psScript, scriptPath: string;
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

  { RunPowerShellFile удаляет временный скрипт (в нём строка подключения с паролем) сразу после запуска. }
  if not RunPowerShellFile(scriptPath, rc) then
  begin
    errMsg := 'Не удалось запустить powershell.exe для проверки.';
    Exit;
  end;

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
  connStr, psScript, scriptPath, outPath: string;
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

  { RunPowerShellFile удаляет временный скрипт (несёт строку подключения с паролем) сразу после запуска. }
  if not RunPowerShellFile(scriptPath, rc) then
  begin
    DeleteFile(outPath);
    Exit;  { fail-open: не смогли запустить }
  end;

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
    TestResultLabel.Caption := 'OK: SQL доступен. Логин службы установщик создаст сам.';
    TestResultLabel.Font.Color := clGreen;
  end
  else
  begin
    TestResultLabel.Caption := 'Ошибка: ' + errMsg;
    TestResultLabel.Font.Color := clRed;
  end;
end;


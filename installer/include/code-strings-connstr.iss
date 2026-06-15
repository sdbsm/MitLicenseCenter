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

{ Квотирует значение для ADO.NET-строки подключения (SqlClient): значение, содержащее ';',
  '=' или краевые пробелы, должно быть заключено в кавычки, иначе парсер connstr порвёт строку
  (например пароль SQL-логина с ';'). Правило SqlConnectionStringBuilder: если значение содержит
  двойную кавычку — оборачиваем в одинарные, иначе — в двойные. Так не нужно удваивать кавычки.
  Результат затем уходит в PowerShell '...'-литерал (PsSingleQuote удвоит одиночные) — конфликта
  нет. Применяется к User Id / Password провижининг-строки (MLC-171). }
function ConnStrQuoteValue(const S: string): string;
begin
  if Pos('"', S) > 0 then
    Result := '''' + S + ''''   { есть " → оборачиваем в одинарные кавычки }
  else
    Result := '"' + S + '"';    { иначе — в двойные }
end;

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
    { User Id/Password квотируем (ConnStrQuoteValue) — пароль может содержать ';'/'='/пробелы. }
    Result := Result + 'User Id=' + ConnStrQuoteValue(ProvUser) +
                       ';Password=' + ConnStrQuoteValue(ProvPassword) + ';'
  else
    Result := Result + 'Integrated Security=True;';
  Result := Result + 'Encrypt=True;TrustServerCertificate=True;Connect Timeout=15';
end;


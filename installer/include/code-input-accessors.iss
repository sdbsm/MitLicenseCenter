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

{ SQL-логин провижининга (sa или иной sysadmin) со страницы кредов. Триммим: имя логина краевых
  пробелов не имеет. До построения страницы (nil) — пусто. MLC-175. }
function ProvUser: string;
begin
  if PageProvCreds = nil then
    Result := ''
  else
    Result := Trim(PageProvCreds.Values[0]);
end;

{ Пароль SQL-логина провижининга. НЕ триммим (пробелы могут быть значимы). Транзиентен: в конфиг
  не пишется, нигде не сохраняется (ADR-49/MLC-171). }
function ProvPassword: string;
begin
  if PageProvCreds = nil then
    Result := ''
  else
    Result := PageProvCreds.Values[1];
end;

{ Личность подключения УСТАНОВЩИКА к SQL — ЯВНЫЙ выбор радио на PageProvMode (MLC-175): индекс
  PROV_INTEGRATED (Integrated Security, дефолт) / PROV_SQLLOGIN (введённый SQL-логин с ролью sysadmin).
  До построения мастера (nil) — Integrated. Раньше выводился неявно из пустоты поля логина (MLC-172). }
function ProvisioningMode: Integer;
begin
  if PageProvMode = nil then
    Result := PROV_INTEGRATED
  else
    Result := PageProvMode.SelectedValueIndex;
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


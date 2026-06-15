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
    бэкап-юнит). AfterID = wpSelectDir, но из-за обратного порядка вставки Inno (страницы с
    одинаковым AfterID идут в порядке, обратном созданию; PageSql и следующие за ней
    создаются позже и оттесняют эту страницу) фактически она встаёт ПОСЛЕДНЕЙ из кастомных —
    прямо перед «Готово к установке». Это и нужно: подтверждение бэкапа — последний экран
    перед заменой файлов, самое свежее напоминание перед необратимым шагом. На чистой
    установке страница пропускается через ShouldSkipPage (not ServiceExists). Гейт «Далее» —
    в NextButtonClick: без отметки чекбокса дальше не пускаем. }
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

  { --- Страница «Подключение установщика к SQL» (MLC-171/172) --- }
  { Под какой личностью УСТАНОВЩИК подключается к SQL, чтобы создать Windows-логин учётки службы
    (CREATE LOGIN … FROM WINDOWS + sysadmin) и проверить достижимость. Ортогонально выбору учётки
    службы (страница ниже): создаётся ВСЕГДА Windows-логин; SQL-логин (sa) — лишь разовый «ключ»
    провижининга, в конфиг не пишется и нигде не сохраняется. Режим выводится из поля логина:
    пусто → Integrated Security (дефолт), заполнено → SQL-логин (ProvisioningMode). Родные поля
    InputQueryPage отображаются гарантированно — в отличие от кастомных контролов на radio-странице,
    которые вставали ниже растянутого CheckListBox и были не видны (баг MLC-172). }
  PageProv := CreateInputQueryPage(PageSql.ID,
    'Подключение установщика к SQL',
    'Как установщик подключится к SQL для создания учётной записи службы',
    'Оставьте поля пустыми, если вы — sysadmin на SQL (Integrated Security, рекомендуется). ' +
    'Иначе укажите SQL-логин с ролью sysadmin (например sa) и пароль. Данные используются ' +
    'однократно и не сохраняются.');
  PageProv.Add('SQL-логин (sysadmin); пусто = Integrated Security:', False);
  PageProv.Add('Пароль:', True);

  { Кнопка теста + метка-результат под полями — родной паттерн InputQueryPage (как был на PageCreds
    до MLC-171, рабочий в проде с MLC-101). Тест идёт под выбранной личностью провижининга и работает
    для ОБОИХ режимов учётки службы (у виртуальной своей страницы кредов нет). }
  TestButton := TNewButton.Create(WizardForm);
  TestButton.Parent := PageProv.Surface;
  TestButton.Caption := 'Проверить подключение';
  TestButton.Width := ScaleX(150);
  TestButton.Height := ScaleY(25);
  TestButton.Left := 0;
  TestButton.Top := PageProv.Edits[1].Top + PageProv.Edits[1].Height + ScaleY(16);
  TestButton.OnClick := @TestButtonClick;

  TestResultLabel := TNewStaticText.Create(WizardForm);
  TestResultLabel.Parent := PageProv.Surface;
  TestResultLabel.Left := 0;
  TestResultLabel.Top := TestButton.Top + TestButton.Height + ScaleY(10);
  TestResultLabel.Width := PageProv.SurfaceWidth;
  TestResultLabel.AutoSize := False;
  TestResultLabel.Height := ScaleY(40);
  TestResultLabel.WordWrap := True;
  TestResultLabel.Caption := '';

  { --- Страница «Учётная запись службы» (ADR-49) --- }
  PageAuthMode := CreateInputOptionPage(PageProv.ID,
    'Учётная запись службы',
    'Под какой учётной записью работает служба панели',
    'К SQL во всех случаях — по Windows-аутентификации (Trusted_Connection).',
    True, False);
  PageAuthMode.Add('Виртуальная учётная запись «{#MyVirtualAccount}» (рекомендуется) — без пароля.');
  PageAuthMode.Add('Указанная учётная запись Windows / gMSA (для домена).');
  PageAuthMode.SelectedValueIndex := ACCT_VIRTUAL;

  { --- Страница «Учётные данные» (только для именованной учётки, ShouldSkipPage) --- }
  { Только имя/пароль Windows-учётки службы + чекбокс gMSA. Тест подключения к SQL перенесён на
    страницу «Подключение установщика к SQL» (MLC-171) — он тестирует личность провижининга и
    работает для обоих режимов учётки службы (включая виртуальную, у которой этой страницы нет). }
  PageCreds := CreateInputQueryPage(PageAuthMode.ID,
    'Учётные данные',
    'Именованная учётная запись Windows / gMSA для службы',
    'Имя заранее созданной учётной записи. Для gMSA отметьте чекбокс и оставьте пароль пустым.');
  PageCreds.Add('Windows-аккаунт / gMSA (ДОМЕН\Пользователь или ДОМЕН\Имя$):', False);
  PageCreds.Add('Пароль:', True);

  { Чекбокс gMSA: учётка без пароля. При отмеченном — поле пароля игнорируется, password= в
    sc create не передаётся (ServiceAccountUsesPassword → False). ADR-49. }
  GmsaCheckbox := TNewCheckBox.Create(WizardForm);
  GmsaCheckbox.Parent := PageCreds.Surface;
  GmsaCheckbox.Left := 0;
  GmsaCheckbox.Top := PageCreds.Edits[1].Top + PageCreds.Edits[1].Height + ScaleY(8);
  GmsaCheckbox.Width := PageCreds.SurfaceWidth;
  GmsaCheckbox.Caption := 'Это групповая управляемая учётная запись (gMSA) — без пароля';
  GmsaCheckbox.Checked := False;

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
  { Страница учётных данных — только для именованной учётки / gMSA (ADR-49). Для виртуальной
    учётной записи ввод не требуется, страницу пропускаем. }
  if (PageCreds <> nil) and (PageID = PageCreds.ID) then
    Result := (AccountMode = ACCT_VIRTUAL);
end;

{ Сброс результата теста при заходе на страницу провижининга + обновление состояния полей;
  подсказки по именованной учётке на странице учётных данных. }
procedure CurPageChanged(CurPageID: Integer);
begin
  { Страница «Подключение установщика к SQL» (MLC-171): сбрасываем прежний результат теста
    (личность провижининга могла измениться при правке полей). }
  if (PageProv <> nil) and (CurPageID = PageProv.ID) then
  begin
    ConnTestPassed := False;
    if TestResultLabel <> nil then
    begin
      TestResultLabel.Caption := '';
      TestResultLabel.Font.Color := clNavy;
    end;
  end;

  if (PageCreds <> nil) and (CurPageID = PageCreds.ID) then
  begin
    { Страница показывается только для именованной учётки / gMSA (ADR-49; виртуальная — пропуск
      через ShouldSkipPage). REL-06/MLC-127: явные требования к учётке — оператор должен знать ДО
      установки, иначе «установка прошла, а половина продукта не работает». Тест подключения к SQL
      перенесён на страницу «Подключение установщика к SQL» (MLC-171) — здесь только реквизиты учётки. }
    PageCreds.PromptLabels[0].Caption := 'Windows-аккаунт / gMSA (ДОМЕН\Пользователь или ДОМЕН\Имя$):';
    PageCreds.SubCaptionLabel.Caption :=
      'Учётная запись, под которой работает служба. Для gMSA отметьте чекбокс и оставьте пароль пустым.' + #13#10 +
      'SQL-логин и членство в Администраторах установщик настроит сам. Подробности — docs/INSTALL.';
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
      { Формулировка корректна для обоих сценариев (MLC-116): на апгрейде служба не «создана»,
        она уже существовала — при ServiceExists говорим просто «не запустилась». }
      WizardForm.FinishedLabel.Caption :=
        'Служба «{#MyServiceName}» не запустилась — панель сейчас недоступна.' + #13#10#13#10 +
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

  { --- Подключение установщика к SQL (MLC-171/172) --- }
  { Режим выводится из поля логина: пусто → Integrated Security; заполнено → SQL-логин. Если логин
    задан, требуем и пароль. В обоих режимах требуем успешный тест достижимости под выбранной
    личностью (тестируем тем же, чем будем создавать логин службы). Гейт ConnTestPassed единый. }
  if (PageProv <> nil) and (CurPageID = PageProv.ID) then
  begin
    if (ProvUser <> '') and (ProvPassword = '') then
    begin
      MsgBox('Укажите пароль SQL-логина — либо очистите поле логина, чтобы использовать ' +
             'Integrated Security (текущий администратор).', mbError, MB_OK);
      Result := False;
      Exit;
    end;
    { Гейт: требуем успешный тест достижимости SQL под выбранной личностью. }
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
               'Исправьте данные и повторите. Продолжить установку нельзя без успешной проверки. ' +
               'Если экземпляр работает только в режиме Windows-аутентификации — выберите вариант ' +
               '«Integrated Security».',
               mbError, MB_OK);
        Result := False;
        Exit;
      end;
      ConnTestPassed := True;
    end;
  end;

  { --- Учётные данные (только именованная учётка / gMSA; виртуальная — страница пропущена) --- }
  { Тест подключения к SQL — на странице «Подключение установщика к SQL» (MLC-171); здесь
    только реквизиты Windows-учётки службы. }
  if (PageCreds <> nil) and (CurPageID = PageCreds.ID) then
  begin
    if CredUser = '' then
    begin
      MsgBox('Укажите Windows-учётную запись (или gMSA) для службы.', mbError, MB_OK);
      Result := False;
      Exit;
    end;
    { Пароль обязателен ТОЛЬКО для обычной учётки. gMSA — без пароля (управляет домен). }
    if (not IsGmsa) and (CredPassword = '') then
    begin
      MsgBox('Укажите пароль учётной записи.' + #13#10 +
             'Если это групповая управляемая учётная запись (gMSA) — отметьте чекбокс «Это gMSA».',
             mbError, MB_OK);
      Result := False;
      Exit;
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

{ После копирования файлов (ssPostInstall) — порядок ADR-49 (MLC-170). Учётная запись службы
  (виртуальная или именованная) и её SID не существуют до sc create, а ProvisionSqlLogin и ACL
  (грант по имени NT SERVICE\…) на них ссылаются — поэтому сначала регистрируем службу, затем
  провижиним SQL-логин и применяем ACL, и только последним стартуем службу:
    1. WriteProductionConfig        — конфиг (всегда Trusted_Connection, без секрета SQL)
    2. WriteInitialAdminPassword    — initial-admin.secret (только чистая установка)
    3. netsh delete old rule        — снять одноимённое firewall-правило (идемпотентность порта)
    4. CreateStartMenuShortcut      — ярлык меню «Пуск» на URL панели
    5. RegisterService              — sc create/config (учётка + SID существуют; create — hard-fail)
    6. ProvisionSqlLogin            — CREATE LOGIN + sysadmin (ЖЁСТКИЙ fail/откат)
    7. AddServiceAccountToAdmins    — локальная группа Администраторов (best-effort)
    8. HardenConfigAcl              — ACL конфига (грант учётке R; имя NT SERVICE\… резолвится)
    9. HardenDataDirAcl             — ACL каталога данных (грант учётке M; (OI)(CI) накрывает .secret)
   10. StartServiceAndFinalize      — recovery + depend + firewall add + sc start ПОСЛЕДНИМ
  Инварианты: WriteInitialAdminPassword до HardenDataDirAcl; грант учётке — после sc create;
  sc start — последним. На апгрейде (ServiceExists) шаг 5 = sc config (без пароля), шаги 6-9
  идемпотентны. }
procedure CurStepChanged(CurStep: TSetupStep);
var
  rc: Integer;
begin
  if CurStep = ssPostInstall then
  begin
    { 1. Конфиг: всегда Trusted_Connection, SQL-пароля нет (ADR-49). }
    WriteProductionConfig;
    { 2. Пароль admin — ДО ужесточения ACL каталога: icacls (OI)(CI) затем накроет и этот файл,
      чтобы учётка службы могла прочитать и удалить его при первом старте. }
    WriteInitialAdminPassword;
    { 3. Снять одноимённое firewall-правило, чтобы add в StartServiceAndFinalize не плодил дубли
      и применил актуальный порт. Игнорируем результат (правила может не быть на чистой установке). }
    Exec(ExpandConstant('{sys}\netsh.exe'),
         'advfirewall firewall delete rule name="{#MyFirewallRule}"',
         '', SW_HIDE, ewWaitUntilTerminated, rc);
    { 4. Ярлык меню «Пуск» на URL панели (порт из ввода мастера). }
    CreateStartMenuShortcut;
    { 5. Регистрация службы: sc create/config. На провале create бросает исключение -> откат
      установки (MLC-107). Теперь учётка службы и её SID существуют. }
    RegisterService;
    { 6. Авто-создание SQL-логина учётки + sysadmin (ADR-49). ЖЁСТКИЙ fail: на провале MsgBox +
      RaiseException -> откат (иначе служба упадёт на старте с 18456). }
    ProvisionSqlLogin;
    { 7. Учётку — в локальную группу Администраторов для IIS/iisreset (best-effort, без Abort). }
    AddServiceAccountToAdministrators;
    { 8. ACL конфига: только SYSTEM/Administrators + учётка службы R (имя NT SERVICE\… резолвится
      после sc create). }
    HardenConfigAcl;
    { 9. ACL каталога данных: режем наследование, доступ только SYSTEM/Administrators + учётка
      службы Modify; (OI)(CI) накрывает initial-admin.secret из шага 2. }
    HardenDataDirAcl;
    { 10. В КОНЦЕ: recovery-политика + SQL-зависимость + открытие порта + sc start ПОСЛЕДНИМ. }
    StartServiceAndFinalize;
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

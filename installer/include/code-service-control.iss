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
          { Control-код SERVICE_CONTROL_STOP ($1), НЕ access right SERVICE_STOP ($20) (MLC-116):
            иначе ControlService возвращает ERROR_INVALID_PARAMETER и служба не останавливается,
            exe остаётся залочен -> экран restart-manager «файлы заняты» на апгрейде. }
          ControlService(hSvc, SERVICE_CONTROL_STOP, status);
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


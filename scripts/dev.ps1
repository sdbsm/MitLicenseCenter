<#
.SYNOPSIS
    Поднимает backend (dotnet watch) и frontend (pnpm dev) параллельно.
.DESCRIPTION
    Открывает два дочерних процесса в отдельных окнах PowerShell.
    Закройте окна вручную (либо Ctrl+C в каждом) — отдельный watcher здесь не нужен.

    Backend по умолчанию поднимается с повышением прав (UAC), потому что IIS
    ServerManager читает %windir%\system32\inetsrv\config\redirection.config,
    доступный только Administrators. Без прав drift-check всех публикаций падает
    в статус «Ошибка проверки». Передайте -NoElevate, если IIS-логика не нужна.
.PARAMETER NoElevate
    Запустить backend без повышения прав (как обычный процесс). Drift-check/
    reconcile при этом не работают — публикации будут в статусе «Ошибка проверки».
#>
[CmdletBinding()]
param(
    [switch]$NoElevate
)

$ErrorActionPreference = 'Stop'

$RepoRoot = Split-Path -Parent $PSScriptRoot
$BackendProject = Join-Path $RepoRoot 'backend\src\MitLicenseCenter.Web'
$FrontendDir    = Join-Path $RepoRoot 'frontend'

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    throw "Не найден 'dotnet' в PATH. Установите .NET 10 SDK."
}
if (-not (Get-Command pnpm -ErrorAction SilentlyContinue)) {
    throw "Не найден 'pnpm' в PATH. Установите через winget install pnpm.pnpm."
}

$isAdmin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
$elevateBackend = (-not $NoElevate) -and (-not $isAdmin)

Write-Host "Backend  → dotnet watch run в $BackendProject" -ForegroundColor Cyan
Write-Host "Frontend → pnpm dev      в $FrontendDir"       -ForegroundColor Cyan
Write-Host ""
Write-Host "API:     http://localhost:5080"
Write-Host "Swagger: http://localhost:5080/api/docs"
Write-Host "SPA:     http://localhost:5173"
Write-Host ""

$backendCommand = "Set-Location '$BackendProject'; `$env:ASPNETCORE_ENVIRONMENT='Development'; dotnet watch run --no-launch-profile --urls http://localhost:5080"
$backendArgs = @('-NoExit','-Command',$backendCommand)

if ($elevateBackend) {
    Write-Host "Backend поднимается с повышением прав (UAC): IIS ServerManager читает" -ForegroundColor Yellow
    Write-Host "%windir%\system32\inetsrv\config\redirection.config (только Administrators)." -ForegroundColor Yellow
    Write-Host "Без прав drift-check публикаций даёт статус «Ошибка проверки». -NoElevate отключает." -ForegroundColor Yellow
    Start-Process -FilePath 'powershell' -ArgumentList $backendArgs -Verb RunAs -WindowStyle Normal | Out-Null
} else {
    if ($NoElevate) {
        Write-Host "Backend без повышения прав (-NoElevate): drift-check/reconcile IIS работать не будут." -ForegroundColor Yellow
    }
    Start-Process -FilePath 'powershell' -ArgumentList $backendArgs -WindowStyle Normal | Out-Null
}

Start-Process -FilePath 'powershell' `
    -ArgumentList '-NoExit','-Command',"Set-Location '$FrontendDir'; pnpm dev" `
    -WindowStyle Normal | Out-Null

Write-Host "Процессы запущены в отдельных окнах. Закрывайте их вручную." -ForegroundColor Green

<#
.SYNOPSIS
    Поднимает backend (dotnet watch) и frontend (pnpm dev) параллельно.
.DESCRIPTION
    Открывает два дочерних процесса в отдельных окнах PowerShell.
    Закройте окна вручную (либо Ctrl+C в каждом) — отдельный watcher здесь не нужен.
#>
[CmdletBinding()]
param()

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

Write-Host "Backend  → dotnet watch run в $BackendProject" -ForegroundColor Cyan
Write-Host "Frontend → pnpm dev      в $FrontendDir"       -ForegroundColor Cyan
Write-Host ""
Write-Host "API:     http://localhost:5080"
Write-Host "Swagger: http://localhost:5080/api/docs"
Write-Host "SPA:     http://localhost:5173"
Write-Host ""

Start-Process -FilePath 'powershell' `
    -ArgumentList '-NoExit','-Command',"Set-Location '$BackendProject'; `$env:ASPNETCORE_ENVIRONMENT='Development'; dotnet watch run --no-launch-profile --urls http://localhost:5080" `
    -WindowStyle Normal | Out-Null

Start-Process -FilePath 'powershell' `
    -ArgumentList '-NoExit','-Command',"Set-Location '$FrontendDir'; pnpm dev" `
    -WindowStyle Normal | Out-Null

Write-Host "Процессы запущены в отдельных окнах. Закрывайте их вручную." -ForegroundColor Green

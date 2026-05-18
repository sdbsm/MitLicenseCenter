<#
.SYNOPSIS
    Полная проверка проекта: backend (restore/build/test/format) + frontend (lint/type-check/build).
.DESCRIPTION
    Запускается локально и в CI. Завершается ненулевым exit-кодом при любой ошибке.
.PARAMETER Configuration
    Debug или Release. По умолчанию Release.
.PARAMETER SkipFormat
    Пропустить шаг dotnet format (полезно во время активной разработки).
#>
[CmdletBinding()]
param(
    [ValidateSet('Debug','Release')]
    [string]$Configuration = 'Release',
    [switch]$SkipFormat
)

$ErrorActionPreference = 'Stop'

$RepoRoot = Split-Path -Parent $PSScriptRoot
$Backend  = Join-Path $RepoRoot 'backend'
$Frontend = Join-Path $RepoRoot 'frontend'

function Assert-Cli {
    param([string]$Name)
    if (-not (Get-Command $Name -ErrorAction SilentlyContinue)) {
        throw "Не найдена утилита '$Name' в PATH. Установите её и попробуйте снова."
    }
}

function Invoke-Step {
    param([string]$Title, [scriptblock]$Action)
    Write-Host ""
    Write-Host "==> $Title" -ForegroundColor Cyan
    & $Action
    if ($LASTEXITCODE -ne 0) {
        throw "Шаг провален: $Title (exit code $LASTEXITCODE)."
    }
}

Assert-Cli dotnet
Assert-Cli pnpm
Assert-Cli node

Write-Host "Сборка MitLicense Center · конфигурация: $Configuration" -ForegroundColor Green
Write-Host "Корень репозитория: $RepoRoot"

Push-Location $Backend
try {
    Invoke-Step "Backend · dotnet restore"   { dotnet restore MitLicenseCenter.slnx }
    Invoke-Step "Backend · dotnet build"     { dotnet build MitLicenseCenter.slnx -c $Configuration --no-restore }
    Invoke-Step "Backend · dotnet test"      { dotnet test MitLicenseCenter.slnx -c $Configuration --no-build --logger trx }
    if (-not $SkipFormat) {
        Invoke-Step "Backend · dotnet format" { dotnet format MitLicenseCenter.slnx --verify-no-changes --severity warn }
    }
}
finally {
    Pop-Location
}

Push-Location $Frontend
try {
    Invoke-Step "Frontend · pnpm install"    { pnpm install --frozen-lockfile }
    Invoke-Step "Frontend · pnpm lint"       { pnpm lint }
    Invoke-Step "Frontend · pnpm type-check" { pnpm type-check }
    Invoke-Step "Frontend · pnpm build"      { pnpm build }
}
finally {
    Pop-Location
}

Write-Host ""
Write-Host "Все шаги пройдены успешно." -ForegroundColor Green

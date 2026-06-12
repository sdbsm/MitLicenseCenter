<#
.SYNOPSIS
    Сборка GUI-установщика панели (Inno Setup): self-contained publish бэкенда +
    компиляция installer\MitLicenseCenter.iss в один MitLicenseCenter-Setup-<version>.exe.
.DESCRIPTION
    Обёртка в стиле build.ps1 / publish-release.ps1 (успех нативных шагов — строго
    по $LASTEXITCODE; обход stderr-спама PowerShell 5.1). Шаги:
      1) если не -SkipPublish — гонит scripts\publish-release.ps1 (self-contained,
         single-file win-x64) → artifacts\<version>\backend;
      2) ищет ISCC.exe (компилятор Inno Setup) по нескольким путям;
      3) вызывает ISCC с /DMyAppVersion и /DPublishDir, кладёт Setup.exe в -OutputDir;
      4) печатает путь и размер Setup.exe.

    Установщик (ADR-31) — интерактивный мастер: собирает SQL-инстанс/БД, режим аутентификации
    (Windows-аккаунт под службой ИЛИ SQL-логин при LocalSystem), учётку и сетевые параметры
    (порт → Urls+firewall, AllowedHosts), проверяет подключение (PowerShell System.Data.SqlClient),
    пишет appsettings.Production.json из ввода и настраивает службу; умеет обновление поверх
    (стоп службы → подмена с сохранением appsettings.Production.json / key ring / БД → старт).

    Предусловие: установлен Inno Setup 6 (ISCC.exe). Если не найден —
    `winget install JRSoftware.InnoSetup`.
.PARAMETER Configuration
    Debug или Release (проброс в publish-release.ps1). По умолчанию Release.
.PARAMETER OutputDir
    Каталог для Setup.exe. По умолчанию artifacts\<version> (<version> из
    backend\Directory.Build.props). Self-contained publish при этом ложится в
    artifacts\<version>\backend (дефолт publish-release.ps1).
.PARAMETER SkipPublish
    Не гонять publish-release.ps1 — переиспользовать готовый publish в
    artifacts\<version>\backend (быстрая пересборка только установщика).
#>
[CmdletBinding()]
param(
    [ValidateSet('Debug','Release')]
    [string]$Configuration = 'Release',
    [string]$OutputDir,
    [switch]$SkipPublish
)

$ErrorActionPreference = 'Stop'

$RepoRoot   = Split-Path -Parent $PSScriptRoot
$Backend    = Join-Path $RepoRoot 'backend'
$BuildProps = Join-Path $Backend  'Directory.Build.props'
$IssScript  = Join-Path $RepoRoot 'installer\MitLicenseCenter.iss'
$PublishScript = Join-Path $PSScriptRoot 'publish-release.ps1'

# Версия продукта — единая точка в Directory.Build.props (тег <Version>); та же логика,
# что в publish-release.ps1. Определяет дефолтный publish-каталог и имя Setup.exe.
function Get-ProductVersion {
    if (-not (Test-Path $BuildProps)) {
        throw "Не найден $BuildProps — невозможно определить версию."
    }
    [xml]$props = Get-Content -Path $BuildProps -Raw
    $version = $props.Project.PropertyGroup.Version | Where-Object { $_ } | Select-Object -First 1
    if ([string]::IsNullOrWhiteSpace($version)) {
        throw "В $BuildProps не найден непустой тег <Version>."
    }
    return $version.Trim()
}

# Превращает путь в абсолютный (как в publish-release.ps1).
function Resolve-AbsolutePath {
    param([string]$Path)
    if ([System.IO.Path]::IsPathRooted($Path)) {
        return [System.IO.Path]::GetFullPath($Path)
    }
    return [System.IO.Path]::GetFullPath((Join-Path (Get-Location).Path $Path))
}

# Поиск компилятора Inno Setup. Порядок: PATH → per-user → 32-/64-bit Program Files.
function Find-Iscc {
    $cmd = Get-Command 'ISCC.exe' -ErrorAction SilentlyContinue
    if ($cmd) { return $cmd.Source }

    $candidates = @(
        (Join-Path $env:LOCALAPPDATA 'Programs\Inno Setup 6\ISCC.exe'),
        "C:\Users\andre\AppData\Local\Programs\Inno Setup 6\ISCC.exe",
        (Join-Path ${env:ProgramFiles(x86)} 'Inno Setup 6\ISCC.exe'),
        (Join-Path $env:ProgramFiles 'Inno Setup 6\ISCC.exe')
    )
    foreach ($c in $candidates) {
        if (-not [string]::IsNullOrWhiteSpace($c) -and (Test-Path $c)) {
            return $c
        }
    }
    throw "Не найден компилятор Inno Setup (ISCC.exe). Установите его: winget install JRSoftware.InnoSetup"
}

$version = Get-ProductVersion

if ([string]::IsNullOrWhiteSpace($OutputDir)) {
    $OutputDir = Join-Path $RepoRoot "artifacts\$version"
}
$OutputDir  = Resolve-AbsolutePath $OutputDir
$PublishDir = Resolve-AbsolutePath (Join-Path $RepoRoot "artifacts\$version\backend")

Write-Host "Сборка установщика MitLicense Center · версия: $version · конфигурация: $Configuration" -ForegroundColor Green
Write-Host "Publish-артефакт: $PublishDir"
Write-Host "Каталог установщика: $OutputDir"

# --- Шаг 1: self-contained publish (если не -SkipPublish) ---
if ($SkipPublish) {
    Write-Host ""
    Write-Host "==> -SkipPublish: переиспользую готовый publish" -ForegroundColor Cyan
    if (-not (Test-Path (Join-Path $PublishDir 'MitLicenseCenter.Web.exe'))) {
        throw "Не найден $PublishDir\MitLicenseCenter.Web.exe — нечего переиспользовать. Запустите без -SkipPublish."
    }
}
else {
    Write-Host ""
    Write-Host "==> publish-release.ps1 (self-contained single-file win-x64)" -ForegroundColor Cyan
    & $PublishScript -Configuration $Configuration -OutputDir $PublishDir
    if ($LASTEXITCODE -ne 0) {
        throw "publish-release.ps1 провален (exit code $LASTEXITCODE)."
    }
    if (-not (Test-Path (Join-Path $PublishDir 'MitLicenseCenter.Web.exe'))) {
        throw "Publish не дал MitLicenseCenter.Web.exe в $PublishDir."
    }
}

# --- Шаг 2: sanity-чек состава publish-каталога (REL-01) ---
# Этот скрипт собирает ТОЛЬКО self-contained single-file артефакт (рантайм вшит в exe).
# В таком публише не должно быть следов framework-dependent-режима: *.deps.json /
# *.runtimeconfig.json. Их наличие = в каталог уехал чужой/накопительный публиш →
# падаем ДО ISCC с внятной ошибкой, чтобы в Setup.exe не уехал недетерминированный состав.
# Чек работает и в ветке -SkipPublish (переиспользование), и после свежего publish.
Write-Host ""
Write-Host "==> Sanity-чек состава publish-каталога (self-contained, без framework-dependent следов)" -ForegroundColor Cyan
$forbidden = Get-ChildItem -Path $PublishDir -Recurse -File -Include '*.deps.json', '*.runtimeconfig.json' -ErrorAction SilentlyContinue
if ($forbidden) {
    $list = ($forbidden | ForEach-Object { '  - ' + $_.FullName.Substring($PublishDir.Length).TrimStart('\', '/') }) -join [Environment]::NewLine
    throw @"
Sanity-чек провален: в self-contained publish-каталоге найдены следы framework-dependent-режима.
Каталог: $PublishDir
Лишние файлы (*.deps.json / *.runtimeconfig.json):
$list
Self-contained single-file публиш не содержит этих файлов — значит каталог накопительный или
собран в смешанном режиме. Пересоберите без -SkipPublish (publish-release.ps1 чистит каталог),
либо удалите чужой publish-каталог вручную.
"@
}
Write-Host "Sanity-чек пройден: следов framework-dependent-режима не найдено." -ForegroundColor Green

# --- Шаг 3: найти ISCC ---
$iscc = Find-Iscc
Write-Host ""
Write-Host "==> ISCC: $iscc" -ForegroundColor Cyan

# --- Шаг 4: компиляция установщика ---
if (-not (Test-Path $OutputDir)) {
    New-Item -ItemType Directory -Path $OutputDir -Force | Out-Null
}

$isccArgs = @(
    "/DMyAppVersion=$version",
    "/DPublishDir=$PublishDir",
    "/O$OutputDir",
    $IssScript
)

Write-Host "==> ISCC $($isccArgs -join ' ')" -ForegroundColor Cyan

# Успех нативного шага — ТОЛЬКО по $LASTEXITCODE (обход stderr-спама PS 5.1, как build.ps1).
$previousEap = $ErrorActionPreference
$ErrorActionPreference = 'Continue'
try {
    & $iscc @isccArgs
}
finally {
    $ErrorActionPreference = $previousEap
}
if ($LASTEXITCODE -ne 0) {
    throw "ISCC провален (exit code $LASTEXITCODE)."
}

# --- Шаг 5: итог ---
$setupExe = Join-Path $OutputDir "MitLicenseCenter-Setup-$version.exe"

Write-Host ""
Write-Host "Установщик собран." -ForegroundColor Green
if (Test-Path $setupExe) {
    $sizeMb = [math]::Round((Get-Item $setupExe).Length / 1MB, 1)
    Write-Host "Setup.exe: $setupExe"
    Write-Host "Размер: $sizeMb МБ"
}
else {
    Write-Host "ВНИМАНИЕ: ожидаемый $setupExe не найден — проверьте вывод ISCC." -ForegroundColor Yellow
}

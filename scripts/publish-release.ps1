<#
.SYNOPSIS
    Сборка установочного артефакта панели: self-contained single-file win-x64 публиш
    бэкенда (рантайм вшит → .NET на хосте не нужен) + SPA (wwwroot из MLC-098) + appsettings.
.DESCRIPTION
    Обёртка над `dotnet publish` в стиле build.ps1. По умолчанию собирает self-contained
    single-file артефакт для инсталлятора (MLC-100+) и ручных обновлений: один
    MitLicenseCenter.Web.exe со вшитым рантаймом .NET 10. Таргет CopySpaToPublish
    (MLC-098, AfterTargets="Publish") сам кладёт собранный фронт в <OutputDir>\wwwroot.

    Trimming НЕ используется: EF Core / Hangfire / Identity рефлексивны, обрезка их ломает.

    Предусловие сборки фронта (если не задан -SkipSpaBuild / -PrebuiltSpaDist) — то же,
    что у MLC-098: node ≥ 22.13 + pnpm 11 на build-машине.
.PARAMETER OutputDir
    Каталог артефакта. По умолчанию artifacts\<version>\backend, где <version> читается
    из тега <Version> в backend\Directory.Build.props.
.PARAMETER Configuration
    Debug или Release. По умолчанию Release.
.PARAMETER FrameworkDependent
    Собрать framework-dependent публиш (без -r/--self-contained/PublishSingleFile) —
    fallback для хоста, на котором уже установлен runtime .NET 10. Артефакт меньше,
    но на хосте нужен shared-framework.
.PARAMETER SkipSpaBuild
    Не собирать SPA (проброс -p:SkipSpaBuild=true). wwwroot останется пустым.
.PARAMETER PrebuiltSpaDist
    Путь к уже собранному dist фронта (проброс -p:PrebuiltSpaDist=...) — публиш
    переиспользует его без вызова pnpm (CI / инсталлятор-пайплайн).
#>
[CmdletBinding()]
param(
    [string]$OutputDir,
    [ValidateSet('Debug','Release')]
    [string]$Configuration = 'Release',
    [switch]$FrameworkDependent,
    [switch]$SkipSpaBuild,
    [string]$PrebuiltSpaDist
)

$ErrorActionPreference = 'Stop'

$RepoRoot   = Split-Path -Parent $PSScriptRoot
$Backend    = Join-Path $RepoRoot 'backend'
$WebProject = Join-Path $Backend  'src\MitLicenseCenter.Web\MitLicenseCenter.Web.csproj'
$BuildProps = Join-Path $Backend  'Directory.Build.props'

function Assert-Cli {
    param([string]$Name)
    if (-not (Get-Command $Name -ErrorAction SilentlyContinue)) {
        throw "Не найдена утилита '$Name' в PATH. Установите её и попробуйте снова."
    }
}

# Версия продукта — единая точка в Directory.Build.props (тег <Version>); используется
# для дефолтного OutputDir, чтобы артефакты разных версий не перетирали друг друга.
function Get-ProductVersion {
    if (-not (Test-Path $BuildProps)) {
        throw "Не найден $BuildProps — невозможно определить версию для OutputDir."
    }
    [xml]$props = Get-Content -Path $BuildProps -Raw
    $version = $props.Project.PropertyGroup.Version | Where-Object { $_ } | Select-Object -First 1
    if ([string]::IsNullOrWhiteSpace($version)) {
        throw "В $BuildProps не найден непустой тег <Version>."
    }
    return $version.Trim()
}

Assert-Cli dotnet

$version = Get-ProductVersion

# Превращает путь в абсолютный: относительный — от текущего каталога, абсолютный
# (`F:\...`, `\\share\...`) оставляет как есть. Прямое склеивание абсолютного пути
# через Join-Path с CWD дало бы битый `cwd\F:\...` → GetFullPath бросает.
function Resolve-AbsolutePath {
    param([string]$Path)
    if ([System.IO.Path]::IsPathRooted($Path)) {
        return [System.IO.Path]::GetFullPath($Path)
    }
    return [System.IO.Path]::GetFullPath((Join-Path (Get-Location).Path $Path))
}

if ([string]::IsNullOrWhiteSpace($OutputDir)) {
    $OutputDir = Join-Path $RepoRoot "artifacts\$version\backend"
}
# Нормализуем в абсолютный путь (dotnet publish -o и проверки ниже работают по нему).
$OutputDir = Resolve-AbsolutePath $OutputDir

$mode = if ($FrameworkDependent) { 'framework-dependent' } else { 'self-contained single-file (win-x64)' }
Write-Host "Публиш MitLicense Center · версия: $version · конфигурация: $Configuration" -ForegroundColor Green
Write-Host "Режим: $mode"
Write-Host "Каталог артефакта: $OutputDir"

# Детерминированный состав поставки (REL-01): чистим OutputDir ПЕРЕД dotnet publish.
# Без этого dotnet publish лишь дописывает файлы поверх прежнего прогона, и в каталоге
# копятся чужие следы (например stale *.deps.json / *.runtimeconfig.json от прежней
# framework-dependent-сборки) — состав Setup.exe тогда зависит от истории build-машины.
# Защита от сноса постороннего каталога: отказываемся чистить пустой путь и корень диска.
function Clear-OutputDir {
    param([string]$Path)

    if ([string]::IsNullOrWhiteSpace($Path)) {
        throw "Внутренняя ошибка: пустой путь OutputDir — чистка отменена."
    }

    $full = [System.IO.Path]::GetFullPath($Path)
    $root = [System.IO.Path]::GetPathRoot($full)
    # Сравниваем без хвостового разделителя: 'F:\' == 'F:\' → отказ чистить корень диска.
    $trimmedFull = $full.TrimEnd('\', '/')
    $trimmedRoot = $root.TrimEnd('\', '/')
    if ([string]::IsNullOrEmpty($trimmedFull) -or ($trimmedFull -eq $trimmedRoot)) {
        throw "Отказ чистить '$full': путь — корень диска. Укажите подкаталог в -OutputDir."
    }

    if (Test-Path -LiteralPath $full) {
        Write-Host "==> Очистка каталога артефакта перед публишем: $full" -ForegroundColor Cyan
        Remove-Item -LiteralPath $full -Recurse -Force
    }
    New-Item -ItemType Directory -Path $full -Force | Out-Null
}

Clear-OutputDir $OutputDir

# Собираем аргументы dotnet publish.
# REL-08 (MLC-126): подавляем артефакты, которые НЕ нужны в поставке и раскрывают
# внутренности (information disclosure). Аргументы — ОБЩИЕ (до ветвления режима),
# действуют и в self-contained, и в framework-dependent публише:
#   DebugType=none / DebugSymbols=false → не генерировать *.pdb;
#   IsTransformWebConfigDisabled=true   → не генерировать web.config (нужен только
#     IIS-хостингу; панель хостит SPA сама — ADR-30, web.config мёртв).
# appsettings.Development.json чистого publish-аргумента не имеет — удаляется дефензивно
# после publish (ниже).
$publishArgs = @(
    'publish', $WebProject,
    '-c', $Configuration,
    '-o', $OutputDir,
    '-p:DebugType=none',
    '-p:DebugSymbols=false',
    '-p:IsTransformWebConfigDisabled=true'
)

if ($FrameworkDependent) {
    # Без runtime identifier / self-contained / single-file — нужен .NET 10 на хосте.
}
else {
    $publishArgs += @(
        '-r', 'win-x64',
        '--self-contained', 'true',
        '-p:PublishSingleFile=true',
        '-p:IncludeNativeLibrariesForSelfExtract=true',
        '-p:EnableCompressionInSingleFile=true'
    )
    # Trimming сознательно НЕ включаем (PublishTrimmed не задаём): рефлексивные
    # EF Core / Hangfire / Identity ломаются при обрезке.
}

if ($SkipSpaBuild) {
    $publishArgs += '-p:SkipSpaBuild=true'
}
if (-not [string]::IsNullOrWhiteSpace($PrebuiltSpaDist)) {
    $prebuilt = Resolve-AbsolutePath $PrebuiltSpaDist
    $publishArgs += "-p:PrebuiltSpaDist=$prebuilt"
}

Write-Host ""
Write-Host "==> dotnet $($publishArgs -join ' ')" -ForegroundColor Cyan

# Успех нативного шага определяется ТОЛЬКО по $LASTEXITCODE. Под Windows PowerShell 5.1
# при $ErrorActionPreference='Stop' любая запись dotnet в stderr превращается в
# терминирующую NativeCommandError ещё до реального exit-кода (тот же обход, что в build.ps1).
$previousEap = $ErrorActionPreference
$ErrorActionPreference = 'Continue'
try {
    & dotnet @publishArgs
}
finally {
    $ErrorActionPreference = $previousEap
}
if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish провален (exit code $LASTEXITCODE)."
}

# Файл сведений о сторонних лицензиях — обязательная часть поставки (REL-10): Hangfire
# (LGPL v3) и SheetJS/xlsx (Apache 2.0) требуют вложить тексты лицензий в дистрибутив.
# Кладём ПОСЛЕ publish, т.к. Clear-OutputDir выше пересоздаёт каталог (MLC-108). Файл
# .txt безопасен для sanity-чека build-installer.ps1 (тот ловит только *.deps.json /
# *.runtimeconfig.json). Отсутствие исходника — ошибка сборки, а не тихий пропуск.
$licensesSource = Join-Path $RepoRoot 'THIRD_PARTY_LICENSES.txt'
if (-not (Test-Path -LiteralPath $licensesSource)) {
    throw "Не найден $licensesSource — файл сторонних лицензий обязателен в поставке (REL-10)."
}
Copy-Item -LiteralPath $licensesSource -Destination (Join-Path $OutputDir 'THIRD_PARTY_LICENSES.txt') -Force

# --- REL-08 (MLC-126): дефензивная чистка appsettings.Development.json ---
# Для этого файла чистого publish-аргумента нет, поэтому удаляем явно, если он
# просочился в поставку (dev-конфиг в Setup.exe — information disclosure).
$devSettings = Join-Path $OutputDir 'appsettings.Development.json'
if (Test-Path -LiteralPath $devSettings) {
    Write-Host "==> REL-08: удаляю appsettings.Development.json из поставки" -ForegroundColor Cyan
    Remove-Item -LiteralPath $devSettings -Force
}

# --- REL-08 (MLC-126): sanity-чек состава поставки ---
# После подавляющих publish-аргументов и дефензивной чистки в OutputDir не должно
# остаться *.pdb / appsettings.Development.json / web.config (information disclosure).
# Если что-то просочилось — падаем ДО упаковки с внятным списком (по образцу
# forbidden-чека build-installer.ps1). Второй рубеж — sanity-чек build-installer.ps1.
$secretsLeak = Get-ChildItem -Path $OutputDir -Recurse -File -Include '*.pdb', 'appsettings.Development.json', 'web.config' -ErrorAction SilentlyContinue
if ($secretsLeak) {
    $list = ($secretsLeak | ForEach-Object { '  - ' + $_.FullName.Substring($OutputDir.Length).TrimStart('\', '/') }) -join [Environment]::NewLine
    throw @"
REL-08 sanity-чек провален: в поставке найдены запрещённые артефакты (information disclosure).
Каталог: $OutputDir
Лишние файлы (*.pdb / appsettings.Development.json / web.config):
$list
Эти файлы не должны попадать в дистрибутив: pdb/web.config подавляются publish-аргументами
(DebugType=none / DebugSymbols=false / IsTransformWebConfigDisabled=true), appsettings.Development.json
удаляется дефензивно выше. Если они всё же появились — проверьте csproj/таргеты публиша.
"@
}
Write-Host "REL-08 sanity-чек пройден: pdb / appsettings.Development.json / web.config в поставке нет." -ForegroundColor Green

# --- Итоговый вывод: путь артефакта, размер exe, наличие SPA ---
$exePath = Join-Path $OutputDir 'MitLicenseCenter.Web.exe'
$spaIndex = Join-Path $OutputDir 'wwwroot\index.html'

Write-Host ""
Write-Host "Публиш завершён." -ForegroundColor Green
Write-Host "Артефакт: $OutputDir"

if (Test-Path $exePath) {
    $sizeMb = [math]::Round((Get-Item $exePath).Length / 1MB, 1)
    Write-Host "MitLicenseCenter.Web.exe: $sizeMb МБ"
}
else {
    Write-Host "ВНИМАНИЕ: MitLicenseCenter.Web.exe не найден в артефакте." -ForegroundColor Yellow
}

if (Test-Path $spaIndex) {
    Write-Host "SPA: wwwroot\index.html присутствует."
}
else {
    Write-Host "SPA: wwwroot\index.html ОТСУТСТВУЕТ (ожидаемо при -SkipSpaBuild)." -ForegroundColor Yellow
}

if (Test-Path (Join-Path $OutputDir 'THIRD_PARTY_LICENSES.txt')) {
    Write-Host "Лицензии: THIRD_PARTY_LICENSES.txt вложен в поставку."
}

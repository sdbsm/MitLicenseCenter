<#
.SYNOPSIS
    MLC-039 (PERF-03) — dev/test-only нагрузочный seed для проверки роста.
.DESCRIPTION
    Засевает dev-БД синтетическими данными (N клиентов, M инфобаз+публикаций, K строк аудита)
    через консольный харнесс MitLicenseCenter.Tools.PerfHarness и пишет scenario.json
    (S активных сессий + over-limit тенанты) для фейкового rac.exe.
    НЕ для прода: тот же бинарь в stub-режиме подставляется как OneC.RAS.ExePath.
    Перед прогоном пересоздайте/накатите БД (scripts\db-reset.ps1).
.PARAMETER Tenants / Infobases / Audit / Sessions
    Ростовые точки. Дефолты = baseline; ×10 = ростовая точка (см. docs\OPERATIONS.md).
.PARAMETER OverLimitFraction
    Доля тенантов, заведомо превышающих лимит (триггерит enforcement/kill-путь).
    Дефолт зависит от -Realistic: 0.10 (realistic) / 0.30 (perf).
.PARAMETER Realistic
    Реалистичные demo-данные «как будто пользовались»: лимиты клиентов 5..150 (СМБ-микс),
    правдоподобные названия, история /reports = лимиту клиента (coupled), живые сессии =
    текущему потреблению. Дефолты сдвигаются: over-limit ~10%, usage-days 365.
    Без флага — perf-поведение MLC-039 1:1 (лимиты 1/1e6).
.PARAMETER ConnectionString
    Строка подключения к dev-БД (с Database=). По умолчанию локальный MSSQL / MitLicenseCenter.
.PARAMETER ScenarioPath
    Куда писать scenario.json. По умолчанию %LOCALAPPDATA%\MitLicenseCenter\perf\scenario.json.
#>
[CmdletBinding()]
param(
    [int]$Tenants = 20,
    [int]$Infobases = 50,
    [int]$Audit = 100000,
    [int]$AuditDays = 365,
    [int]$Sessions = 500,
    [int]$UsageDays = 0,
    [double]$OverLimitFraction = 0.30,
    [switch]$Realistic,
    [int]$Seed = 1039,
    [string]$ConnectionString = 'Server=.;Database=MitLicenseCenter;Trusted_Connection=True;TrustServerCertificate=True;Encrypt=False',
    [string]$ScenarioPath,
    [ValidateSet('Debug','Release')]
    [string]$Configuration = 'Release'
)

$ErrorActionPreference = 'Stop'

$RepoRoot = Split-Path -Parent $PSScriptRoot
$Project  = Join-Path $RepoRoot 'backend\tools\MitLicenseCenter.Tools.PerfHarness'

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    throw "Не найден 'dotnet' в PATH. Установите .NET 10 SDK."
}

$env:ConnectionStrings__Default = $ConnectionString

# Realistic-дефолты применяем, только если оператор НЕ задал параметр явно (PSBoundParameters).
if (-not $PSBoundParameters.ContainsKey('OverLimitFraction')) {
    $OverLimitFraction = if ($Realistic) { 0.10 } else { 0.30 }
}
if (-not $PSBoundParameters.ContainsKey('UsageDays')) {
    $UsageDays = if ($Realistic) { 365 } else { 0 }
}

# Доля форматируется инвариантно — иначе на RU-локали уйдёт «0,3» и парсер харнесса упадёт.
$fraction = $OverLimitFraction.ToString([System.Globalization.CultureInfo]::InvariantCulture)

$seedArgs = @(
    'seed',
    '--tenants', $Tenants,
    '--infobases', $Infobases,
    '--audit', $Audit,
    '--audit-days', $AuditDays,
    '--sessions', $Sessions,
    '--usage-days', $UsageDays,
    '--over-limit-fraction', $fraction,
    '--seed', $Seed
)
if ($Realistic) { $seedArgs += '--realistic' }
if ($ScenarioPath) { $seedArgs += @('--scenario', $ScenarioPath) }

Write-Host "Сидинг dev-БД нагрузочными данными..." -ForegroundColor Cyan
Write-Host "Цель: $ConnectionString"

# Успех нативного шага определяется ТОЛЬКО по $LASTEXITCODE (паттерн build.ps1/db-reset.ps1):
# под Windows PowerShell 5.1 при $ErrorActionPreference='Stop' вывод dotnet в stderr может
# стать терминирующей ошибкой ещё до реального exit-кода. Снимаем Stop вокруг вызова.
$previousEap = $ErrorActionPreference
$ErrorActionPreference = 'Continue'
try {
    dotnet run --project $Project -c $Configuration -- @seedArgs
}
finally {
    $ErrorActionPreference = $previousEap
}
if ($LASTEXITCODE -ne 0) {
    throw "PerfHarness seed завершился с ошибкой (exit code $LASTEXITCODE)."
}

$exe = Join-Path $Project "bin\$Configuration\net10.0\MitLicenseCenter.Tools.PerfHarness.exe"
Write-Host ""
Write-Host "Готово. Теперь:" -ForegroundColor Green
Write-Host "  1. Выставьте OneC.RAS.ExePath = $exe (OneC.RAS.Endpoint оставьте пустым)."
Write-Host "  2. Запустите backend (scripts\dev.ps1) и снимите метрики (scripts\perf-counters.ps1)."

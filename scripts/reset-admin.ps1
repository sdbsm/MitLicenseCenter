<#
.SYNOPSIS
    MLC-053 — сброс пароля администратора без потери данных.
.DESCRIPTION
    Сбрасывает пароль учётной записи администратора через штатный UserManager (тот же Identity-хеш
    и парольная политика, что и у приложения) консольным харнессом MitLicenseCenter.Tools.PerfHarness.
    В ОТЛИЧИЕ от scripts\db-reset.ps1 НЕ пересоздаёт базу — клиенты/инфобазы/аудит сохраняются.
    Применяйте, когда утерян пароль, напечатанный сидером при первом старте.
    Итоговый пароль печатается в консоль (stdout) — запишите его и смените при первом входе.
.PARAMETER User
    Логин администратора. По умолчанию 'admin' (IdentitySeeder.DefaultAdminUserName).
.PARAMETER Password
    Явный новый пароль (>= 12 символов, заглавная/строчная/цифра/спецсимвол). Если не задан —
    генерируется криптослучайный, удовлетворяющий политике.
.PARAMETER Unlock
    Дополнительно снять блокировку (lockout) и обнулить счётчик неудачных входов.
.PARAMETER ConnectionString
    Строка подключения к БД (с Database=). По умолчанию локальный MSSQL / MitLicenseCenter.
#>
[CmdletBinding()]
param(
    [string]$User = 'admin',
    [string]$Password,
    [switch]$Unlock,
    [string]$ConnectionString = 'Server=.;Database=MitLicenseCenter;Trusted_Connection=True;TrustServerCertificate=True;Encrypt=False',
    [ValidateSet('Debug','Release')]
    [string]$Configuration = 'Release'
)

$ErrorActionPreference = 'Stop'

$RepoRoot = Split-Path -Parent $PSScriptRoot
$Project  = Join-Path $RepoRoot 'backend\tools\MitLicenseCenter.Tools.PerfHarness'

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    throw "Не найден 'dotnet' в PATH. Установите .NET 10 SDK."
}

# Dev-прогон использует Development-key ring (LocalAppData) — гарантированно writable без elevation.
# На корректность сброса не влияет: токен сброса одноразовый и потребляется внутри того же процесса.
$env:DOTNET_ENVIRONMENT = 'Development'

$resetArgs = @('reset-admin', '--user', $User, '--connection', $ConnectionString)
if ($Password) { $resetArgs += @('--password', $Password) }
if ($Unlock)   { $resetArgs += '--unlock' }

Write-Host "Сброс пароля администратора '$User'..." -ForegroundColor Cyan
Write-Host "Цель: $ConnectionString"

# Успех нативного шага определяется ТОЛЬКО по $LASTEXITCODE (паттерн build.ps1/perf-seed.ps1):
# под Windows PowerShell 5.1 при $ErrorActionPreference='Stop' вывод dotnet в stderr может
# стать терминирующей ошибкой ещё до реального exit-кода. Снимаем Stop вокруг вызова.
$previousEap = $ErrorActionPreference
$ErrorActionPreference = 'Continue'
try {
    dotnet run --project $Project -c $Configuration -- @resetArgs
}
finally {
    $ErrorActionPreference = $previousEap
}
if ($LASTEXITCODE -ne 0) {
    throw "PerfHarness reset-admin завершился с ошибкой (exit code $LASTEXITCODE)."
}

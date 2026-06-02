<#
.SYNOPSIS
    Полностью пересоздаёт локальную MSSQL-базу MitLicenseCenter и накатывает миграции.
.DESCRIPTION
    Использует System.Data.SqlClient (ADO.NET через .NET) — sqlcmd не обязателен.
    После пересоздания зовёт `dotnet ef database update`. Первый запуск Web-сервиса
    создаст начального администратора и напечатает пароль в лог.
.PARAMETER ConnectionString
    Connection string к серверу MSSQL (БЕЗ Database= — БД будет создана). По умолчанию
    Server=.;Trusted_Connection=True;TrustServerCertificate=True;Encrypt=False.
.PARAMETER DatabaseName
    Имя базы. По умолчанию MitLicenseCenter.
.PARAMETER Force
    Удалить существующую базу без вопроса.
#>
[CmdletBinding()]
param(
    [string]$ConnectionString = 'Server=.;Trusted_Connection=True;TrustServerCertificate=True;Encrypt=False',
    [string]$DatabaseName     = 'MitLicenseCenter',
    [switch]$Force
)

$ErrorActionPreference = 'Stop'

$RepoRoot = Split-Path -Parent $PSScriptRoot
$Backend  = Join-Path $RepoRoot 'backend'
$EfProject       = Join-Path $Backend 'src\MitLicenseCenter.Infrastructure'
$StartupProject  = Join-Path $Backend 'src\MitLicenseCenter.Web'

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    throw "Не найден 'dotnet' в PATH. Установите .NET 10 SDK."
}

Add-Type -AssemblyName 'System.Data'

function Invoke-SqlMaster {
    param([string]$Sql)
    $masterCs = "$ConnectionString;Database=master"
    $cn = New-Object System.Data.SqlClient.SqlConnection $masterCs
    $cn.Open()
    try {
        $cmd = $cn.CreateCommand()
        $cmd.CommandText = $Sql
        $cmd.CommandTimeout = 60
        [void]$cmd.ExecuteNonQuery()
    }
    finally {
        $cn.Dispose()
    }
}

function Test-DatabaseExists {
    $masterCs = "$ConnectionString;Database=master"
    $cn = New-Object System.Data.SqlClient.SqlConnection $masterCs
    $cn.Open()
    try {
        $cmd = $cn.CreateCommand()
        $cmd.CommandText = "SELECT database_id FROM sys.databases WHERE name = @name"
        [void]$cmd.Parameters.AddWithValue('@name', $DatabaseName)
        $reader = $cmd.ExecuteReader()
        $exists = $reader.Read()
        $reader.Dispose()
        return $exists
    }
    finally {
        $cn.Dispose()
    }
}

Write-Host "Целевой сервер: $ConnectionString" -ForegroundColor Cyan
Write-Host "База:           $DatabaseName"      -ForegroundColor Cyan

if (Test-DatabaseExists) {
    if (-not $Force) {
        $answer = Read-Host "База '$DatabaseName' существует. Удалить и пересоздать? [y/N]"
        if ($answer -ne 'y' -and $answer -ne 'Y') {
            Write-Host "Отменено." -ForegroundColor Yellow
            return
        }
    }
    Write-Host "Удаляю $DatabaseName..." -ForegroundColor Yellow
    Invoke-SqlMaster "ALTER DATABASE [$DatabaseName] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;"
    Invoke-SqlMaster "DROP DATABASE [$DatabaseName];"
}

Write-Host "Создаю $DatabaseName..." -ForegroundColor Yellow
Invoke-SqlMaster "CREATE DATABASE [$DatabaseName];"

Write-Host ""
Write-Host "Накатываю миграции..." -ForegroundColor Cyan
$env:ConnectionStrings__Default  = "$ConnectionString;Database=$DatabaseName"
$env:ConnectionStrings__Hangfire = "$ConnectionString;Database=$DatabaseName"
# Успех нативного шага определяется ТОЛЬКО по $LASTEXITCODE (тот же паттерн, что в
# build.ps1 Invoke-Step): под Windows PowerShell 5.1 при $ErrorActionPreference='Stop'
# вывод `dotnet ef` в stderr может стать терминирующей ошибкой ещё до реального exit-кода
# (особенно при захвате лога). Локально снимаем Stop вокруг вызова — реальный ненулевой
# код по-прежнему обрабатывается проверкой ниже.
$previousEap = $ErrorActionPreference
$ErrorActionPreference = 'Continue'
try {
    dotnet ef database update --project $EfProject --startup-project $StartupProject
}
finally {
    $ErrorActionPreference = $previousEap
}
if ($LASTEXITCODE -ne 0) {
    throw "dotnet ef database update завершился с ошибкой."
}

Write-Host ""
Write-Host "База готова. Запустите бэкенд (scripts\dev.ps1 или dotnet run) —" -ForegroundColor Green
Write-Host "при первом старте будет создан администратор. Пароль появится в логе одной строкой." -ForegroundColor Green

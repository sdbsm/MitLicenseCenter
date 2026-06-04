<#
.SYNOPSIS
    MLC-039 (PERF-03) — снятие метрик горячего пути (MLC-037) через dotnet-counters.
.DESCRIPTION
    Тонкая обёртка над dotnet-counters по Meter'ам MitLicenseCenter.Rac /
    MitLicenseCenter.Reconciliation (см. docs\OPERATIONS.md «Наблюдаемость перфа»).
    Без -ExportPath — интерактивный monitor; с -ExportPath — collect в CSV/JSON для отчёта.
.PARAMETER ProcessName
    Имя процесса backend. По умолчанию MitLicenseCenter.Web.
.PARAMETER ExportPath
    Если задан — collect в файл (расширение определяет формат: .csv / .json).
#>
[CmdletBinding()]
param(
    [string]$ProcessName = 'MitLicenseCenter.Web',
    [int]$RefreshInterval = 1,
    [string]$ExportPath
)

$ErrorActionPreference = 'Stop'

if (-not (Get-Command dotnet-counters -ErrorAction SilentlyContinue)) {
    throw "Не найден 'dotnet-counters'. Установите: dotnet tool install --global dotnet-counters"
}

$counters = 'MitLicenseCenter.Rac,MitLicenseCenter.Reconciliation'

$previousEap = $ErrorActionPreference
$ErrorActionPreference = 'Continue'
try {
    if ($ExportPath) {
        dotnet-counters collect -n $ProcessName --counters $counters --refresh-interval $RefreshInterval -o $ExportPath
    }
    else {
        dotnet-counters monitor -n $ProcessName --counters $counters --refresh-interval $RefreshInterval
    }
}
finally {
    $ErrorActionPreference = $previousEap
}
if ($LASTEXITCODE -ne 0) {
    throw "dotnet-counters завершился с ошибкой (exit code $LASTEXITCODE)."
}

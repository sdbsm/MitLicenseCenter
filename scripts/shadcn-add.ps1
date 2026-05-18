<#
.SYNOPSIS
    Устанавливает shadcn-компоненты в frontend/ через workaround для Windows + pnpm.
.DESCRIPTION
    Прямой `pnpm dlx shadcn@latest add ...` падает на Windows с
    ERR_PNPM_NO_IMPORTER_MANIFEST_FOUND (конфликт изоляции pnpm с тем, как
    shadcn 4.x резолвит свои deps). Скрипт:
      1. Создаёт временную папку и ставит туда `shadcn` через
         `pnpm add shadcn --config.node-linker=hoisted` (плоское дерево
         модулей, без виртуальных store-ссылок).
      2. Из `frontend/` запускает `node <temp>/node_modules/shadcn/dist/index.js add <components>`.

    После генерации временная папка удаляется. components.json и существующие
    компоненты под frontend/src/components/ui/ не трогаются — shadcn-CLI пишет
    только новые файлы.
.PARAMETER Components
    Список компонентов, как их называет shadcn (`button`, `dialog`, `pagination`…).
.PARAMETER Version
    Версия пакета `shadcn` для установки. По умолчанию `4.7.0` — та, что закреплена в Stage 1 baseline.
.EXAMPLE
    .\scripts\shadcn-add.ps1 pagination
.EXAMPLE
    .\scripts\shadcn-add.ps1 -Components pagination, tabs, popover
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true, Position = 0, ValueFromRemainingArguments = $true)]
    [string[]]$Components,

    [string]$Version = '4.7.0'
)

$ErrorActionPreference = 'Stop'

$RepoRoot   = Split-Path -Parent $PSScriptRoot
$FrontendDir = Join-Path $RepoRoot 'frontend'

if (-not (Test-Path $FrontendDir)) {
    throw "Не нашёл папку frontend по пути $FrontendDir."
}
if (-not (Get-Command pnpm -ErrorAction SilentlyContinue)) {
    throw "Не найден 'pnpm' в PATH. Установите через winget install pnpm.pnpm."
}
if (-not (Get-Command node -ErrorAction SilentlyContinue)) {
    throw "Не найден 'node' в PATH. Установите Node 22.13+ (winget install OpenJS.NodeJS.LTS)."
}

$TempDir = Join-Path ([System.IO.Path]::GetTempPath()) ("shadcn-add-" + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $TempDir | Out-Null

try {
    Write-Host "Устанавливаю shadcn@$Version во временную папку..." -ForegroundColor Cyan
    Write-Host "  $TempDir" -ForegroundColor DarkGray

    Push-Location $TempDir
    try {
        # Минимальный package.json — без него pnpm add ругается на отсутствие manifest.
        '{ "name": "shadcn-add-temp", "private": true, "version": "0.0.0" }' |
            Out-File -FilePath (Join-Path $TempDir 'package.json') -Encoding utf8 -NoNewline

        # --ignore-scripts: shadcn тянет в transitive deps msw, у которого build-script;
        # pnpm 11 без allow-builds выходит с кодом 1 (ERR_PNPM_IGNORED_BUILDS),
        # хотя сами модули уже распакованы. Скрипты пакетов нам не нужны — CLI это JS.
        & pnpm add "shadcn@$Version" --config.node-linker=hoisted --ignore-scripts --silent
        if ($LASTEXITCODE -ne 0) {
            throw "pnpm add shadcn@$Version завершился с кодом $LASTEXITCODE."
        }
    }
    finally {
        Pop-Location
    }

    $ShadcnCli = Join-Path $TempDir 'node_modules\shadcn\dist\index.js'
    if (-not (Test-Path $ShadcnCli)) {
        throw "Не нашёл shadcn CLI по пути $ShadcnCli. Установка не дала ожидаемой структуры."
    }

    $componentsLine = ($Components -join ', ')
    Write-Host "Добавляю компоненты: $componentsLine" -ForegroundColor Cyan

    Push-Location $FrontendDir
    try {
        & node $ShadcnCli add @Components
        if ($LASTEXITCODE -ne 0) {
            throw "shadcn add завершился с кодом $LASTEXITCODE."
        }
    }
    finally {
        Pop-Location
    }

    Write-Host ""
    Write-Host "Готово. Проверьте git diff: новые файлы должны лежать в frontend/src/components/ui/." -ForegroundColor Green
}
finally {
    if (Test-Path $TempDir) {
        Remove-Item -Path $TempDir -Recurse -Force -ErrorAction SilentlyContinue
    }
}

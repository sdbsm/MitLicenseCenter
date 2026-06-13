# MitLicense Center — среда разработчика

Единый источник команд и «гочей» для разработчика. Покрывает пререквизиты,
структуру репозитория, все скрипты, тесты, CI и специфику Windows/1С.

---

## 1. Пререквизиты

| Инструмент | Требование | Источник |
|---|---|---|
| .NET SDK | `10.0.100`, `rollForward: latestFeature` | `backend/global.json` |
| Node.js | `>=22.13` | `frontend/package.json` → `engines.node` |
| pnpm | `11.0.8` (standalone) | `frontend/package.json` → `packageManager` |
| SQL Server | локальный MSSQL-инстанс (SSMS опционально) | `scripts/db-reset.ps1` |
| IIS (для работы с публикациями) | включён на хосте, запуск бэкенда — с правами администратора | `scripts/dev.ps1` |

### Установка .NET SDK

```powershell
winget install Microsoft.DotNet.SDK.10
```

### Установка Node.js

```powershell
winget install OpenJS.NodeJS.LTS
```

### Гоча: pnpm устанавливается только standalone

**Нельзя** устанавливать pnpm через `npm install -g pnpm` — он не совпадёт
по версии с `packageManager` в `package.json` и pre-commit hook потребует
несовпадающую версию. Правильная установка:

```powershell
winget install pnpm.pnpm
```

После установки через winget pnpm доступен по пути
`%LOCALAPPDATA%\Microsoft\WinGet\Packages\pnpm.pnpm_<hash>\` или
`%LOCALAPPDATA%\pnpm`. Pre-commit hook в `.husky/pre-commit` автоматически
добавляет оба варианта расположения в `PATH` при запуске в Git Bash.

Проверить: `pnpm --version` → `11.0.8`.

### 1С-утилиты

`rac.exe` и `webinst.exe` входят в состав дистрибутива 1С:Предприятие 8.3.
Пути к утилитам задаются оператором в панели («Параметры» → `OneC.RAS.ExePath`,
`OneC.Webinst.ExePath`). Для локальной разработки без реального кластера 1С
используется stub-исполнитель `MitLicenseCenter.Tools.PerfHarness` в режиме
`rac`-заглушки (см. `scripts/perf-seed.ps1`).

---

## 2. Структура репозитория

```
F:\dev\MitLicense Center\
├── backend\                      # .NET 10 бэкенд
│   ├── src\
│   │   ├── MitLicenseCenter.Domain\          # доменная модель, Value Objects
│   │   ├── MitLicenseCenter.Application\     # use-cases, интерфейсы, Hangfire-джобы
│   │   ├── MitLicenseCenter.Infrastructure\  # EF Core, rac/webinst, IIS, SQL
│   │   └── MitLicenseCenter.Web\             # Minimal API endpoints, Program.cs
│   ├── tests\
│   │   └── MitLicenseCenter.Tests.Unit\      # 636 тестов (xUnit, NSubstitute, FluentAssertions)
│   ├── tools\
│   │   └── MitLicenseCenter.Tools.PerfHarness\  # консольный харнесс: seed, reset-admin, rac-stub
│   ├── MitLicenseCenter.slnx                 # solution-файл (SLNX-формат)
│   ├── Directory.Build.props                 # единая версия продукта (<Version>)
│   └── Directory.Packages.props             # central package management
├── frontend\                     # React + TypeScript + Vite + Tailwind CSS 4
├── scripts\                      # PowerShell-скрипты разработки и поставки
├── installer\                    # Inno Setup .iss (GUI-установщик)
├── docs\                         # документация v2
├── .github\workflows\ci.yml      # GitHub Actions CI (backend: windows-latest; frontend: ubuntu-latest)
├── .husky\pre-commit             # pre-commit hook: lint-staged + dotnet format
├── .editorconfig                 # форматирование: utf-8, lf; ps1/cmd → crlf
└── .gitattributes                # нормализация eol в git: cs/ts/json/md → lf; ps1/cmd → crlf
```

---

## 3. Скрипты `scripts/`

Все `.ps1` сохранены с **UTF-8 BOM** (PowerShell 5.1 требует BOM для кириллицы
в строковых литералах). Окончания строк в репозитории — LF; `.gitattributes`
(`*.ps1 text eol=crlf`) и `.editorconfig` (`[*.{ps1,psm1,psd1}] end_of_line = crlf`)
задают CRLF на checkout, но текущие закоммиченные blob'ы содержат LF и не
перенормированы (`core.autocrlf=false`).

### `build.ps1` — полный локальный CI-прогон

```powershell
.\scripts\build.ps1                  # Release (по умолчанию)
.\scripts\build.ps1 -Configuration Debug
.\scripts\build.ps1 -SkipFormat      # пропустить dotnet format (для быстрой итерации)
```

Шаги последовательно:
1. `dotnet restore MitLicenseCenter.slnx`
2. `dotnet build` `-c Release --no-restore`
3. `dotnet test` `--no-build --logger trx`
4. `dotnet format --verify-no-changes --severity warn` (пропускается при `-SkipFormat`)
5. `pnpm install --frozen-lockfile`
6. `pnpm lint`
7. `pnpm type-check`
8. `pnpm test`
9. `pnpm build`

Завершается ненулевым кодом при любой ошибке. **Это штатный гейт** — см. §5 (CI).

### `dev.ps1` — локальный dev-сервер

```powershell
.\scripts\dev.ps1            # backend с повышением прав (UAC) + frontend
.\scripts\dev.ps1 -NoElevate # без elevation (drift-check публикаций не работает)
```

Открывает два окна PowerShell:
- backend: `dotnet watch run` на `http://localhost:5080` (Swagger: `http://localhost:5080/api/docs`)
- frontend: `pnpm dev` на `http://localhost:5173`

По умолчанию запрашивает UAC для backend, потому что `Microsoft.Web.Administration`
читает `%windir%\system32\inetsrv\config\redirection.config`, доступный только
членам группы Administrators. Без прав проверка статуса публикаций возвращает «Ошибка проверки».

### `db-reset.ps1` — пересоздание локальной БД

```powershell
.\scripts\db-reset.ps1                                          # локальный MSSQL, БД MitLicenseCenter
.\scripts\db-reset.ps1 -Force                                   # без запроса подтверждения
.\scripts\db-reset.ps1 -ConnectionString "Server=HOST;..." -DatabaseName MyDb
```

Пересоздаёт базу через ADO.NET (`System.Data.SqlClient`, без `sqlcmd`) и накатывает миграции
(`dotnet ef database update`). При первом запуске бэкенда после сброса сидер создаёт учётную
запись `admin` и печатает сгенерированный пароль в лог одной строкой.

**Дефолтная строка подключения** содержит `Encrypt=False` — это допустимо для локального
dev-стенда; на продуктивном хосте используется строка из `appsettings.Production.json`,
где `Encrypt` задаётся явно (см. гочу ниже).

### `reset-admin.ps1` — сброс пароля администратора

```powershell
.\scripts\reset-admin.ps1                          # генерирует случайный пароль
.\scripts\reset-admin.ps1 -Password "NewPass1!"    # явный пароль
.\scripts\reset-admin.ps1 -Unlock                  # снять lockout + сбросить пароль
.\scripts\reset-admin.ps1 -User admin -Configuration Release
```

Сбрасывает пароль через `MitLicenseCenter.Tools.PerfHarness` (команда `reset-admin`),
**не пересоздаёт базу** — все данные сохраняются.

### `perf-seed.ps1` — наполнение dev-БД тестовыми данными

```powershell
.\scripts\perf-seed.ps1                       # 20 клиентов, 50 инфобаз, 100k аудита
.\scripts\perf-seed.ps1 -Realistic            # реалистичные данные «как будто пользовались»
.\scripts\perf-seed.ps1 -Tenants 200 -Infobases 500 -Sessions 5000
```

Dev/test-only. Записывает `scenario.json` для rac-заглушки (путь по умолчанию:
`%LOCALAPPDATA%\MitLicenseCenter\perf\scenario.json`). После сида выставьте
`OneC.RAS.ExePath` = путь к `PerfHarness.exe` (оставьте `OneC.RAS.Endpoint` пустым).

> Параметр `OverLimitFraction` форматируется инвариантно (`InvariantCulture`), чтобы
> на RU-локали не уйти «0,3» вместо «0.3» — парсер харнесса упал бы.

### `shadcn-add.ps1` — добавление shadcn-компонентов

```powershell
.\scripts\shadcn-add.ps1 pagination
.\scripts\shadcn-add.ps1 -Components pagination, tabs, popover
```

Обёртка для обхода ошибки Windows + pnpm (см. гочу ниже). Компоненты появляются
в `frontend/src/components/ui/`. Версия shadcn закреплена параметром `-Version` (дефолт `4.7.0`).

### `publish-release.ps1` — self-contained publish бэкенда

```powershell
.\scripts\publish-release.ps1                            # Release, self-contained single-file win-x64
.\scripts\publish-release.ps1 -Configuration Debug
.\scripts\publish-release.ps1 -FrameworkDependent        # без вшитого рантайма
.\scripts\publish-release.ps1 -SkipSpaBuild              # без пересборки фронта
.\scripts\publish-release.ps1 -PrebuiltSpaDist <путь>   # переиспользовать готовый dist
```

Артефакт: `artifacts\<version>\backend\MitLicenseCenter.Web.exe`. Версия читается из
`backend/Directory.Build.props` → `<Version>`. Trimming **не используется**: EF Core,
Hangfire и Identity рефлексивны, обрезка их ломает. THIRD_PARTY_LICENSES.txt копируется
в артефакт (обязательное требование поставки).

### `build-installer.ps1` — сборка GUI-установщика

```powershell
.\scripts\build-installer.ps1                          # полный цикл: publish + ISCC
.\scripts\build-installer.ps1 -SkipPublish             # только пересборка .iss (переиспользует артефакт)
.\scripts\build-installer.ps1 -IsccPath "C:\...\ISCC.exe"  # явный путь к ISCC
```

Требует Inno Setup 6: `winget install JRSoftware.InnoSetup`. Порядок поиска `ISCC.exe`:
`-IsccPath` → `$env:ISCC_PATH` → PATH → стандартные каталоги (`%LOCALAPPDATA%\Programs\Inno Setup 6`,
`Program Files (x86)`, `Program Files`). Результат: `artifacts\<version>\MitLicenseCenter-Setup-<version>.exe`.

### `perf-counters.ps1` — снятие метрик

```powershell
.\scripts\perf-counters.ps1                         # интерактивный monitor (dotnet-counters)
.\scripts\perf-counters.ps1 -ExportPath metrics.csv # collect в файл
```

Слушает Meter'ы `MitLicenseCenter.Rac` и `MitLicenseCenter.Reconciliation`.
Требует: `dotnet tool install --global dotnet-counters`.

---

## 4. Сборка и тесты

### Локальный CI-прогон

```powershell
.\scripts\build.ps1
```

Единственная команда для полной проверки. Завершается «Все шаги пройдены успешно.»
Ориентировочное время: ~55 секунд на прогретом кэше.

### Backend: тесты отдельно

```powershell
cd backend
dotnet test MitLicenseCenter.slnx -c Release --no-build --logger trx
```

Один тестовый контейнер: `MitLicenseCenter.Tests.Unit` (net10.0).
**636 тестов: 636 пройдено, 0 не пройдено, 0 пропущено** (снято на HEAD 2026-06-12).

### Frontend: команды отдельно

```powershell
cd frontend
pnpm install --frozen-lockfile  # обновить node_modules
pnpm lint                       # ESLint
pnpm type-check                 # tsc --noEmit
pnpm test                       # vitest run (355 тестов, 69 файлов)
pnpm build                      # vite build → dist/
```

---

## 5. CI

### GitHub Actions

| Workflow | Триггер | Job | Runner | Шаги |
|---|---|---|---|---|
| `ci.yml` | push/PR (не на чисто-doc) | `backend` | `windows-latest` | restore → build → test → format-check → vuln-audit |
| `ci.yml` | push/PR (не на чисто-doc) | `frontend` | `ubuntu-latest` | install → lint → type-check → format:check → build → test → pnpm audit |
| `release.yml` | `workflow_dispatch` / push тега `v*` | `build-installer` | `windows-latest` | publish + ISCC → Setup.exe (артефакт) |

`ci.yml` пропускает backend-job на чисто-frontend изменениях и наоборот; оба пропускаются на чисто-doc изменениях (`docs/**`, `*.md`). Аудиты уязвимостей (`dotnet list package --vulnerable`, `pnpm audit`) — информационные, не блокируют сборку (`continue-on-error: true`). `release.yml` — CI-гейт упаковочного конвейера (REL-12, ADR-14); dependabot (`dependabot.yml`) еженедельно открывает PR на обновления NuGet, npm и Actions.

**Статус: красный (биллинг)**. Джобы не стартуют из-за биллинговых ограничений
аккаунта GitHub Actions. Это известное состояние (решение 2026-06-10). **Штатный гейт — локальный
`.\scripts\build.ps1`**; он воспроизводит те же шаги, что и CI, и выдаёт ненулевой
код при любой ошибке.

### Pre-commit hook (`.husky/pre-commit`)

Hook устанавливается автоматически при `pnpm install` (скрипт `prepare` → `install-git-hooks.mjs`,
который устанавливает `core.hooksPath = .husky`).

Что проверяет при коммите:
1. **Staged `.ts`/`.tsx`/`.js`/`.json`/`.css`**: запускает `pnpm exec lint-staged` (Prettier + ESLint).
2. **Staged `.cs`**: запускает `dotnet format ... --include <файлы> --verify-no-changes`.

Hook завершается с ненулевым кодом при любой ошибке и блокирует коммит.
В CI hook отключён (переменная `CI=true` в `install-git-hooks.mjs`).

---

## 6. Чеклист перед релизной сборкой

Страховочный список шагов перед запуском `scripts/build-installer.ps1` (REL-14, MLC-124).
CI Actions красный по биллингу — выполняется вручную.

- [ ] **Обновить .NET SDK до актуального патч-релиза.** `backend/global.json` пинит
  `"version": "10.0.100"` с `rollForward: latestFeature` — SDK автоматически берёт
  последний feature-band, но патч ОС/сборочной машины обновить вручную:
  `winget upgrade Microsoft.DotNet.SDK.10`.
- [ ] **Закрыть открытые dependabot-PR.** Просмотреть и влить (или отклонить) накопившиеся
  PR на обновления NuGet / npm / Actions (`dependabot.yml`).
- [ ] **Проверить NuGet-уязвимости:** `cd backend && dotnet list package --vulnerable --include-transitive`.
- [ ] **Проверить npm-уязвимости:** `cd frontend && pnpm audit`.
- [ ] **Прогнать локальный гейт:** `.\scripts\build.ps1` — должен завершиться
  «Все шаги пройдены успешно.»
- [ ] **Собрать установщик:** `.\scripts\build-installer.ps1` → убедиться, что
  `artifacts\<version>\MitLicenseCenter-Setup-<version>.exe` создан.

---

## 7. Гочи

### 6.1 rac.exe / OEM-кодировка (не хардкод CP866)

`rac.exe` пишет вывод в **активную OEM-кодовую страницу процесса** (на RU Windows = CP866,
но это не хардкод). `SystemProcessRacRunner.ResolveOemEncoding()` определяет кодировку динамически:

```csharp
var oemCp = CultureInfo.CurrentCulture.TextInfo.OEMCodePage;
return Encoding.GetEncoding(oemCp);  // fallback → UTF-8
```

Перед вызовом `Encoding.GetEncoding` регистрируется `CodePagesEncodingProvider.Instance` —
обязательный шаг для .NET Core+, который не включает CP866/CP1251 по умолчанию.

Вывод читается из `BaseStream` (raw bytes), затем декодируется явно. Прямое использование
`ProcessStartInfo.StandardOutputEncoding = UTF8` не работает: дочерний процесс не меняет
свой output на UTF-8, StreamReader декодирует байты неверно.

**Тот же паттерн** применяется в `OneCIisLifecycleService` для `iisreset.exe` (тоже OEM-вывод).
`webinst.exe` исключение — он пишет **UTF-16 LE**, декодируется как `Encoding.Unicode`.

### 6.2 .ps1 — UTF-8 с BOM

Все скрипты в `scripts/` сохранены с UTF-8 BOM.
PowerShell 5.1 (стандартный в Windows) требует BOM для корректной обработки кириллицы
в строках скрипта.

**Контроль в репозитории:**
- `.gitattributes`: `*.ps1 text eol=crlf` — задаёт CRLF при нормализации/checkout.
- `.editorconfig`: секция `[*.{ps1,psm1,psd1}]` задаёт `end_of_line = crlf`.
  Глобальная секция `[*]` задаёт `charset = utf-8`.

**Важно про окончания строк**: несмотря на `eol=crlf` в `.gitattributes`,
закоммиченные blob'ы `.ps1` сейчас содержат **LF**, и при `core.autocrlf=false`
рабочая копия тоже LF (нормализация на существующие файлы не применена). На запуск
скриптов это не влияет: PowerShell исполняет и LF-, и CRLF-скрипты.

**Важно**: `charset = utf-8` в `.editorconfig` не означает «без BOM» — большинство редакторов
(VS Code, Rider) интерпретируют это как UTF-8 без BOM. **BOM не обеспечивается инструментально** —
его нужно сохранять явно при создании нового `.ps1`:

- **VS Code**: `Ctrl+Shift+P` → `Change File Encoding` → `Save with Encoding: UTF-8 with BOM`.
- **PowerShell ISE**: сохраняет с BOM по умолчанию.
- **`Out-File -Encoding utf8`** в PowerShell 5.1 добавляет BOM автоматически.

Если скрипт сохранён без BOM, PowerShell 5.1 читает кириллицу как латиницу — скрипт
не упадёт, но строки `Write-Host` будут нечитаемы.

### 6.3 shadcn только через `scripts/shadcn-add.ps1`

Прямой `pnpm dlx shadcn@latest add ...` падает на Windows с
`ERR_PNPM_NO_IMPORTER_MANIFEST_FOUND` — конфликт изоляции pnpm 11 с тем, как
shadcn 4.x резолвит зависимости.

Скрипт обходит это: ставит `shadcn@<версия>` во временную директорию с
`--config.node-linker=hoisted` (плоское дерево, без виртуальных store-ссылок),
запускает shadcn-CLI из этой директории в контексте `frontend/`. Временная папка
удаляется после завершения.

```powershell
.\scripts\shadcn-add.ps1 button, dialog, pagination
```

После добавления проверить `git diff`: новые файлы должны появиться в
`frontend/src/components/ui/`.

### 6.4 EF-миграции: UTF-8 без BOM + LF

EF Core генерирует файлы миграций с кодировкой **UTF-8 без BOM** и окончаниями **LF**.
`.gitattributes` задаёт `*.cs text eol=lf` — git нормализует строки в LF при checkout.
Таким образом миграции корректно нормализуются автоматически при checkout (CRLF → LF).

**Процедура добавления миграции:**

```powershell
cd backend
dotnet ef migrations add <ИмяМиграции> \
  --project src\MitLicenseCenter.Infrastructure \
  --startup-project src\MitLicenseCenter.Web
```

После генерации убедиться, что файл не получил BOM (VS Code показывает в статус-баре
`UTF-8` без метки BOM). Если редактор добавил BOM — пересохранить без BOM.

Накатить миграцию локально:

```powershell
.\scripts\db-reset.ps1   # пересоздаёт БД и накатывает все миграции
# или только обновить без пересоздания:
dotnet ef database update \
  --project backend\src\MitLicenseCenter.Infrastructure \
  --startup-project backend\src\MitLicenseCenter.Web
```

Итого миграций на 2026-06-12: **19** (первая: `20260518010940_InitialCreate`,
последняя: `20260610212042_MLC092HiddenClusterInfobases`).

### 6.5 Бэкенд требует прав администратора (IIS)

`Microsoft.Web.Administration` читает
`%windir%\system32\inetsrv\config\redirection.config`, доступный только членам
группы Administrators. Без прав:
- drift-check статуса публикаций возвращает «Ошибка проверки»;
- операции publish/unpublish/recycle/restart IIS упадут с `UnauthorizedAccessException`.

`scripts/dev.ps1` по умолчанию запрашивает UAC (ключ `-Verb RunAs`) и запускает
backend в повышенном окне PowerShell. Для работы только с API без IIS-операций
используйте `-NoElevate`.

На продуктивном хосте сервис запускается под учётной записью с необходимыми правами
(настраивается мастером установки из `build-installer.ps1`).

### 6.6 PlatformVersion — ровно 4 числовых сегмента

Версия платформы 1С (`Publication.PlatformVersion`) должна соответствовать формату
`N.N.N.N` (Major.Minor.Build.Revision), например `8.3.23.1865`.

**Backend** (`InfobaseValidationRules.cs`):
```csharp
[GeneratedRegex(@"^\d+\.\d+\.\d+\.\d+$", RegexOptions.CultureInvariant)]
public static partial Regex PlatformVersionRegex();
```

**Frontend** (`frontend/src/features/infobases/validation.ts`):
```typescript
export const PLATFORM_VERSION_PATTERN = /^\d+\.\d+\.\d+\.\d+$/;
```

Совпадение паттернов зафиксировано parity-тестом:
`InfobasesValidationTests.cs` → `Validation_rules_match_documented_spec()` проверяет
`PlatformVersionRegex().ToString()` на точное строковое равенство.

Длины сегментов не фиксированы — `8.5.1.1302` валиден так же, как `8.3.23.1865`.
Пять сегментов (`8.3.23.1865.0`) и нечисловые сегменты (`a.b.c.d`) — ошибка валидации.

### 6.7 API опускает null-поля (camelCase, omit-null)

Backend сериализует ответы с `JsonIgnoreCondition.WhenWritingNull`: **null-поля не
приходят на wire**, ключ полностью отсутствует в JSON. Это означает, что на фронтенде
`z.nullable()` (ожидает ключ со значением `null`) ломает валидацию пустых полей.

**Правило**: для опциональных полей ответа использовать хелпер `omittable()` из
`frontend/src/lib/apiSchema.ts`:

```typescript
export function omittable<T extends z.ZodTypeAny>(schema: T) {
  return schema.nullish().transform((value): z.infer<T> | null => value ?? null);
}
```

Принимает и отсутствие ключа, и `null`, нормализует в `null`. Пример: `Backup.startedAtUtc`,
`Backup.filePath` и аналогичные «заполняемые позже» поля.

Поля API именуются в `camelCase` (стандарт ASP.NET Core JSON с настройками по умолчанию).
Нет snake_case, нет PascalCase на проводе.

### 6.8 Encrypt=False в dev-конфиге SQL

`appsettings.json` и все `*.ps1`-скрипты используют строку подключения с `Encrypt=False`.
Это допустимо для локальной разработки (SQL Server без TLS-сертификата).

На продуктивном хосте строка подключения из `appsettings.Production.json` задаётся
мастером установки — значение `Encrypt` там явное и зависит от конфигурации хоста.
Не копировать dev-строку подключения в продуктивный конфиг.

### 6.9 PowerShell 5.1: stderr нативных команд как ошибка

В Windows PowerShell 5.1 при `$ErrorActionPreference = 'Stop'` любая запись
нативной команды в stderr (например, баннер `pnpm` или диагностика `dotnet`)
превращается в терминирующую `NativeCommandError` ещё до проверки реального
exit-кода. Это проявляется при захвате лога (`| Tee-Object`), а не при прямом запуске.

Все скрипты применяют единый обход: снимают `Stop` вокруг нативного вызова,
проверяют `$LASTEXITCODE` явно после. Повторять этот паттерн в новых скриптах.

---

## 8. Doc-driven дисциплина

Документация в `docs/` — present-tense, описывает текущее состояние системы.
Правится в том же изменении, что и код: если изменение влияет на поведение,
описанное в `docs/`, обновление `docs/` — часть того же PR, а не отдельная задача.

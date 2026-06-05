# CLAUDE.md — operating guide for agents

MitLicense Center — веб-панель управления мультитенантной 1С-инфраструктурой
(1С 8.3–8.5 на Windows Server + IIS + MSSQL): клиенты, базы, IIS-публикации,
лимиты лицензий и автоматический контроль соблюдения квот. **Не** биллинг и
**не** десктоп-приложение.

## ⛔ Перед тем как править код

1. Прочитать `docs/PROJECT_BACKLOG.md` → найти `NEXT TASK` (одна задача за сессию;
   `NEXT TASK` ставит внешний чат-куратор, исполнитель сам её не выставляет).
2. Свериться с каноном `docs/` по теме правки; архитектурные решения — `DECISIONS.md` (ADR).
3. Расхождение код↔doc — **не чинить молча**: завести `[Doc divergence]` с вариантами.
4. Правишь правило валидации инфобаз — правишь **обе стороны**:
   BE `Endpoints/Shared/InfobaseValidationRules.cs` ↔ FE `features/infobases/validation.ts`.

## Канон — источник правды

`docs/` описывает текущее состояние v1 в present-tense. Старт —
[docs/00_INDEX.md](docs/00_INDEX.md). Порядок: `01→06` (спека) → `DECISIONS`
(почему) → `OPERATIONS` (эксплуатация). Активные задачи — `docs/PROJECT_BACKLOG.md`
(читать первым, см. блок «⛔ Перед тем как править код» выше; сейчас активных нет).
Правь канон present-tense, без
changelog-хвостов — историю несёт git. Менять архитектурное решение — только через
правку соответствующего ADR в `docs/DECISIONS.md`.

## Стек (кратко)
- **Backend** (`backend/`, решение `MitLicenseCenter.slnx`): .NET 10, EF Core 10
  (SQL Server), ASP.NET Identity (cookie-auth), Hangfire (схема `hangfire`),
  minimal API `/api/v1/...`, Swagger `/api/docs`. Слои: `Domain → Application →
  Infrastructure → Web` (границы стерегут NetArchTest-тесты).
- **Frontend** (`frontend/`): React 19 + Vite + TypeScript, Tailwind v4,
  shadcn/ui (Radix), TanStack Query, react-hook-form + zod, i18next (**только ru**).
  pnpm 11 (пиннится через `packageManager`).

## Команды (PowerShell, из корня репо)
- `.\scripts\db-reset.ps1` — пересоздать локальную БД + накатить миграции (первый запуск / после изменения миграций).
- `.\scripts\dev.ps1` — backend (`dotnet watch`, :5080) + frontend (`pnpm dev`, :5173) в двух окнах.
- `.\scripts\build.ps1 [-Configuration Release] [-SkipFormat]` — полный CI-прогон локально.
- `.\scripts\shadcn-add.ps1 <name>` — добавить shadcn-компонент (обход pnpm-isolation на Windows; см. гочи).
- `.\scripts\perf-seed.ps1` / `.\scripts\perf-counters.ps1` — нагрузочный seed + dotnet-counters (dev/test).

URL после старта: SPA `http://localhost:5173` · API `http://localhost:5080` ·
Swagger `/api/docs` · Hangfire `/hangfire` (только `Admin`).

CI — `.github/workflows/ci.yml` (backend на windows-latest, frontend на
ubuntu-latest). Pre-commit (`.husky/pre-commit`, нужен **Git Bash**) гоняет
lint-staged + `dotnet format` на staged-файлах.

## Конвенции
- Коммиты: `MLC-NNN: <что>` для задач бэклога, иначе префикс `docs:` / `chore:` / `fix:`. Ветку — от `main`.
- **Anti-corruption граница (ADR-20):** в Web-эндпоинтах можно инжектить
  `AppDbContext` напрямую (vertical slice), НО к 1С/IIS/SQL-discovery — **только**
  через интерфейсы-адаптеры (`IClusterClient`, `IIisPublishingService`, …).
  Никаких `rac.exe` / `Microsoft.Web.Administration` в Web-проекте.
- **Enum int-значения заморожены** (`AuditActionType`/`AuditReason`,
  `HasConversion<int>`): число переиспользовать нельзя, новое действие = новое число.
- **Валидация в двух местах с parity-тестами:** backend
  `Endpoints/Shared/InfobaseValidationRules.cs` ↔ frontend
  `features/infobases/validation.ts` — меняешь правило, меняешь обе стороны.
  **Гоча:** DataAnnotations на request-контрактах (`[Required]`/`[StringLength]`)
  в minimal API **не прогоняются в runtime** (фильтра-валидатора нет) — они только
  для Swagger. Реальную проверку делают ручные хелперы (`AppendPublicationFieldErrors`),
  и они **не режут max-длины** — длину ловит только `nvarchar` БД. Нужна проверка
  длины на API — добавляй её в хелпер явно.
- Настройки — только из whitelist `SettingDefinitions` (`Application/`); новый ключ = запись в каталоге + сидер.

## Гочи (Windows / 1С)
- **`rac.exe` выдаёт CP866** (OEM на RU Windows) — парсер декодирует сырые байты, не UTF-8 (ADR-3.3).
- **`.ps1` — UTF-8 *с* BOM**, иначе PowerShell портит кириллицу.
- **Node 22.13+** обязателен (pnpm 11 использует `node:sqlite`; на Node 20 падает). `engines` и CI выровнены на `22.13`.
- **pnpm — standalone** (`winget install pnpm.pnpm`); Corepack на Windows не пишет в `Program Files`.
- **shadcn** — только через `scripts/shadcn-add.ps1` (temp-папка + `--config.node-linker=hoisted` + `--ignore-scripts`); напрямую CLI на pnpm-Windows ломается.
- `build.ps1`/`db-reset.ps1` временно глушат `$ErrorActionPreference=Stop` вокруг нативных вызовов — это обход stderr-спама PowerShell 5.1, не баг.
- **DPAPI key ring + БД — единый бэкап-юнит:** key ring в `%ProgramData%\MitLicenseCenter\keys` (prod), purpose `mlc.settings.v1`. Потеря одного из двух → секретные настройки не расшифровать.
- **После `dotnet ef migrations add` нормализуй сгенерированные файлы** (`*_*.cs`, `*.Designer.cs`, `AppDbContextModelSnapshot.cs`): EF пишет их в UTF-8 **с BOM + CRLF**, а пре-коммит требует **без BOM + LF** — иначе коммит падает с `ENDOFLINE`/`CHARSET`. Эталон — существующие миграции (UTF-8 без BOM, LF).
- **Backend, обслуживающий IIS, требует прав администратора:** `ServerManager` читает `%windir%\system32\inetsrv\config\redirection.config` (ACL — только Administrators/SYSTEM); неэлевированный процесс → все публикации в статусе `Error` («Ошибка проверки»). Dev — `dev.ps1` поднимает backend с UAC (флаг `-NoElevate` отключает); prod — service account в локальных Administrators или явный read на `inetsrv\config`. Детали — `docs/OPERATIONS.md` «IIS publishing — required permissions».
- **`PlatformVersion` regex = `^\d+\.\d+\.\d+\.\d+$`** — ровно 4 числовых сегмента, длины **не** фиксировать: у ранних сборок 1С 8.5 одноцифровой build (`8.5.1.1302`), жёсткий `\d{2}\.\d{4}` их отрезает. Менять — синхронно в `Endpoints/Shared/InfobaseValidationRules.cs` и `features/infobases/validation.ts`.

## Проверка изменений
- Backend: `dotnet test` в `backend/` (или `build.ps1`). Тесты — xUnit + NSubstitute + NetArchTest.
- Frontend: `pnpm test` / `pnpm type-check` / `pnpm lint` в `frontend/`.
- IIS-логика Windows-only (`OneCIisPublishingService`, `[SupportedOSPlatform("windows")]`); в тестах — `StubIisPublishingService`.

См. также [SECURITY.md](SECURITY.md) — модель безопасности и принятые риски.

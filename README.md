# MitLicense Center

Веб-панель управления мультитенантной 1С-инфраструктурой (1С 8.3–8.5 на Windows Server + IIS + MSSQL): клиенты, базы, IIS-публикации, лимиты лицензий и автоматический контроль соблюдения квот.

Это не биллинговая система и не десктоп-приложение. Полная цель и ограничения зафиксированы в `docs/01_PROJECT_CONTEXT.md`. Архитектурные решения — в `docs/DECISIONS.md`.

## Требования к локальной среде

- Windows 10/11 или Windows Server.
- **.NET 10 SDK** (10.0.100+). Проверка: `dotnet --version`.
  ```pwsh
  winget install Microsoft.DotNet.SDK.10
  ```
- **Node.js 22.13+** (требование `pnpm` 11; на Node 20 падает с `No such built-in module: node:sqlite`). Проверка: `node --version`.
  ```pwsh
  winget install OpenJS.NodeJS.LTS
  ```
- **pnpm** (standalone-установка):
  ```pwsh
  winget install pnpm.pnpm
  ```
  Версия пнится через `packageManager` поле в `frontend/package.json`. Corepack тоже сработает на Linux/macOS или в elevated-shell на Windows, но обычному пользователю он не может записать в `C:\Program Files\nodejs\`, поэтому базовый путь — standalone-пакет.
- **MSSQL** (Developer/Standard, локальный дефолтный экземпляр) с connection string `Server=.;Database=MitLicenseCenter;Trusted_Connection=True;TrustServerCertificate=True`. Перекрывается через User Secrets / переменные окружения `ConnectionStrings__Default` и `ConnectionStrings__Hangfire`.
- **Git for Windows** (включает Git Bash — нужен pre-commit-хуку в `.husky/pre-commit`).

## Быстрый старт

```pwsh
# 1. Сбросить и накатить локальную БД (первый запуск либо после изменений миграций):
.\scripts\db-reset.ps1

# 2. Запустить бэкенд (dotnet watch) + фронтенд (pnpm dev) параллельно:
.\scripts\dev.ps1
```

После старта:
- SPA — http://localhost:5173
- API — http://localhost:5080
- Swagger UI — http://localhost:5080/api/docs
- Hangfire — http://localhost:5080/hangfire (только для роли `Admin`)

При первом запуске бэкенд создаёт пользователя `admin` со случайным паролем и пишет его в лог одной строкой `WARN`. Залогиньтесь этим паролем и смените его.

## Проверка перед коммитом

```pwsh
# Полный прогон: сборка, тесты, lint, type-check, build фронта:
.\scripts\build.ps1
```

Pre-commit-хук (`husky` + `lint-staged`) автоматически прогонит Prettier/ESLint на staged JS/TS файлах и `dotnet format --verify-no-changes` на staged `.cs`. Если что-то не проходит — коммит блокируется.

## Структура репозитория

```
backend/    .NET 10 solution (Domain / Application / Infrastructure / Web + Tests.Unit)
frontend/   React 19 + TS SPA (Vite, Tailwind v4, shadcn/ui)
docs/       Проектная документация (01..06 + DECISIONS.md)
scripts/    PowerShell-скрипты (build, dev, db-reset)
.github/    CI workflow
```

# MitLicense Center

Веб-панель управления мультитенантной 1С-инфраструктурой (1С 8.3–8.5 на Windows Server + IIS + MSSQL): клиенты, базы, IIS-публикации, лимиты лицензий и автоматический контроль соблюдения квот.

Это не биллинговая система и не десктоп-приложение. Полная цель и ограничения зафиксированы в `docs/01_PROJECT_CONTEXT.md`. Архитектурные решения — в `docs/DECISIONS.md`.

## Требования к локальной среде

- Windows 10/11 или Windows Server.
- **.NET 10 SDK** (10.0.100+). Проверка: `dotnet --version`.
- **Node.js 20+ LTS**. Проверка: `node --version`.
- **Corepack** включён (поставляется с Node 20+):
  ```pwsh
  corepack enable
  ```
  Это даст `pnpm` той версии, что зафиксирована в `frontend/package.json` (`packageManager` поле).
- **MSSQL** (Express/Developer) с локальным экземпляром `.\SQLEXPRESS` либо строкой подключения, указанной в `appsettings.Development.json` / переменной окружения.
- **Git for Windows** (включает Git Bash — нужен Husky pre-commit-хукам).

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

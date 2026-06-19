# Текущее состояние: раздел «Быстродействие»

> Снимок на момент старта трека (2026-06-20). **Перед правкой перепроверить по коду** —
> карта могла устареть. Решение владельца: текущий раздел в целевом дизайне идёт
> **под полную переделку** (см. `30_TARGET_DESIGN.md`), но накопленные источники данных
> (perfmon / rac / DMV) и их адаптеры — ценны и переиспользуются.

---

## Что это сейчас

Live-приборная панель реального времени (pull-by-demand, опрос ~5 c). Воронка экрана:
вердикт → светофор ресурсов (CPU/RAM/диск) → drill-down (Хост / 1С / SQL) →
блокировки → «Запись по требованию».

Маршрут FE: `/performance`. Роли: Viewer (чтение), Admin (управление записью).

## Источники данных (это собирается правильно — стадия «где узкое место»)

| Слой | Источник | Ключевые файлы |
|---|---|---|
| Хост | WMI perfmon-счётчики + `System.Diagnostics.Process` | `backend/src/MitLicenseCenter.Infrastructure/Performance/OneCHostMetricsProbe.cs` |
| 1С | `rac.exe session list` / `process list` через RAS | `backend/src/MitLicenseCenter.Infrastructure/Clusters/RacExecutableRasClusterClient.cs` |
| SQL | DMV (`dm_exec_requests/sessions/sql_text`, `dm_os_wait_stats`, `dm_io_virtual_file_stats`) | `backend/src/MitLicenseCenter.Infrastructure/Performance/SqlPerformanceProbe.cs` |
| Атрибуция база→клиент | `AppDbContext.Infobases ⨝ Tenants` | `backend/src/MitLicenseCenter.Application/Performance/SqlPerformanceView.cs` (+ endpoint) |

Endpoints: `backend/src/MitLicenseCenter.Web/Endpoints/Performance/PerformanceEndpoints.cs`
(`/api/v1/performance/host|onec-sessions|sql|recordings`).

FE: `frontend/src/features/performance/` (~62 файла; контейнер `PerformancePage.tsx`,
оркестрация `usePerformancePage.ts`, типы/Zod `types.ts`).

## «Запись по требованию» (зачаток расследования — стадия времени)

- Сервис `PerfRecordingService.cs` + фоновый `PerfRecordingSamplingService.cs` (~15 c/сэмпл),
  оба в `backend/src/MitLicenseCenter.Infrastructure/Performance/`.
- Хранение: таблицы `dbo.PerfRecordings` / `dbo.PerfRecordingSamples`
  (`backend/src/MitLicenseCenter.Infrastructure/Reporting/PerfRecording*.cs`,
  миграция `..._MLC070PerfRecordings.cs`). Плоские метрики хоста + JSON топ-виновников 1С/SQL.
- Авто-стоп по лимитам (samples/time), восстановление осиротевших при рестарте,
  ночной retention-job (Hangfire).

## Документация и решения

- `docs/01_OVERVIEW.md` (описание раздела), `docs/05_FRONTEND.md` (воронка экрана).
- ADR (в `docs/DECISIONS.md`): live-модель «pull по требованию, live не сохраняется,
  персист только в Recording»; Web читает `AppDbContext` напрямую для атрибуции;
  контракт `rac.exe` CLI.

---

## Честная оценка против методики ИТС

| Стадия воронки (см. `10_SOURCES.md` A1) | Покрытие | Комментарий |
|---|---|---|
| 1. Что медленно (замеры/Apdex) | ❌ нет | Нет замеров времени операций; видим «сервер занят», не «операция X у клиента Y — 8 c». |
| 2. Где узкое место (ресурсы) | ✅ есть | Источники корректные; для своей стадии — те данные. Сильная часть. |
| 3. Почему (технологический журнал) | ❌ нет | **ТЖ не используется вообще** (ни `logcfg.xml`, ни события `TLOCK/EXCP/...` в коде нет). Это главный пробел. |
| 4. Вывод/рекомендации | 🟡 частично | Есть live-«вердикт» и запись, но без доказательной базы ТЖ и без рекомендаций по C4. |

**Что переиспользуем при переделке:** адаптеры источников (Host/SQL probe, rac-клиент),
атрибуцию база→клиент, идею «Записи» как окна времени, схему ролей.
**Что добавляем:** весь слой ТЖ (сбор + парсинг + анализ) и объект-«дело» (см. целевой дизайн).

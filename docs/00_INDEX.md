# Документация MitLicense Center — индекс

Каталог `docs/` — **единственный источник правды** о текущем состоянии v1.
Стиль канона: present-tense (описывает «как есть сейчас»), без changelog-хвостов —
историю несёт git. Последняя построчная сверка канона с кодом: **2026-06-05**.

## Порядок чтения

Спека читается по порядку `01 → 06`; справочники (`DECISIONS`/`OPERATIONS`/`ROADMAP`)
открываются точечно.

| # | Файл | О чём | Когда открывать |
|---|------|-------|-----------------|
| 01 | [01_PROJECT_CONTEXT](01_PROJECT_CONTEXT.md) | Цель, границы, что НЕ делаем, глоссарий | первым — понять, зачем всё |
| 02 | [02_ARCHITECTURE_REQUIREMENTS](02_ARCHITECTURE_REQUIREMENTS.md) | Модульный монолит, границы модулей, two-tier reconciliation loop | прежде чем менять архитектуру |
| 03 | [03_DOMAIN_MODEL](03_DOMAIN_MODEL.md) | Сущности, заморож. enum-значения, индексы, 409-коды, контракты | работа с доменом / БД / API-контрактами |
| 04 | [04_INFRASTRUCTURE](04_INFRASTRUCTURE.md) | rac.exe/RAS, IIS/VRD, DPAPI, **каталог 17 настроек**, джобы и их cron | интеграции, настройки, фоновые джобы |
| 05 | [05_UI_REQUIREMENTS](05_UI_REQUIREMENTS.md) | Стек фронта, страницы/роуты, что в v1 есть и чего нет | функциональность SPA |
| 06 | [06_UI_DESIGN](06_UI_DESIGN.md) | Визуальный язык, таблицы, деструктивные действия, рус. микрокопирайт | вёрстка / UX |
| — | [DECISIONS](DECISIONS.md) | ADR-журнал (монолит): почему так; отозванные решения | вопрос «почему так, а не иначе» |
| — | [OPERATIONS](OPERATIONS.md) | Деплой, бэкап, health-проба, hardening, наблюдаемость, перф-харнесс | эксплуатация / прод |
| — | [ROADMAP](ROADMAP.md) | Статус (v1 поставлен), отложенное, out-of-scope | статус и «что дальше» |

## Реестр задач (не канон)

- [PROJECT_BACKLOG](PROJECT_BACKLOG.md) — активный реестр; **читать первым каждую сессию**. Активных задач нет (на дату сверки).
- [PROJECT_BACKLOG_ARCHIVE](PROJECT_BACKLOG_ARCHIVE.md) — закрытые задачи (MLC-001..043), read-only.

## Где искать X

| Нужно | Где |
|-------|-----|
| Ключи настроек (17 шт., дефолты, диапазоны) | 04 §«Runtime Settings Catalog» |
| Числовые значения enum (`SessionKilled=200`, …) | 03 §«Enum int-stability» |
| Коды 409-конфликтов | 03 §«409 Conflict contract» → `Endpoints/Shared/Problems.cs` |
| Cron фоновых джоб (cold/hot/status-refresh/retention) | 04 §«Background Job Execution» + `Program.cs` |
| Контракт `rac.exe` CLI, CP866, spawn-бюджет | 04 §1 + DECISIONS ADR-3.3 |
| Публикация (webinst) + смена платформы (web.config) | 04 §2 + DECISIONS ADR-4 (ADR-4.1 revoked) |
| Деплой / бэкап / hardening | OPERATIONS |
| Почему RAS, а не REST/COM | DECISIONS ADR-16 |
| Health/readiness-эндпоинты, диаг-флаги | OPERATIONS §«Проверки готовности» / «Профиль EF» |
| Команды сборки/запуска, гочи Windows/1С | [/CLAUDE.md](../CLAUDE.md) |
| Модель безопасности | [/SECURITY.md](../SECURITY.md) |
| Статус v1 / отложенное (RAS Strategy B, multi-node) | ROADMAP |

> **Правило правок канона:** менять архитектурное решение — только через правку
> соответствующего ADR в `DECISIONS.md`. Расхождение doc↔код фиксируется в
> `PROJECT_BACKLOG.md`, а не «молча» переписывается.

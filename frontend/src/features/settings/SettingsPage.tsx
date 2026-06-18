import { Fragment, useEffect, useMemo, useState } from "react";
import { useTranslation } from "react-i18next";
import { cn } from "@/lib/utils";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { Skeleton } from "@/components/ui/skeleton";
import { SettingField } from "./SettingField";
import { PlatformPicker } from "./PlatformPicker";
import { RasPortField } from "./RasPortField";
import { DatabaseServerField } from "./DatabaseServerField";
import { RasServiceCard } from "./RasServiceCard";
import { UpdateCheckCard } from "@/features/updates/UpdateCheckCard";
import type { SettingDescriptor } from "./types";
import { useSettings } from "./useSettings";

// Раскладка секций /settings (MLC-055, перегруппировка MLC-083). Подключение к 1С / RAS
// объединяет креды rac.exe (--cluster-user/--cluster-pwd, ADR-3.3), порт RAS
// (OneC.RAS.Endpoint → RasPortField) и единый пикер платформы (OneC.RAS.ExePath +
// OneC.DefaultPlatformVersion → PlatformPicker, версия в SECTIONS отдельно не
// перечисляется — её ведёт пикер). «SQL Server» — единственное место, где задан
// SQL-инстанс (Sql.Server, MLC-087): формы баз и discovery имён БД берут значение
// отсюда, «дефолтом для форм» он больше не называется. Сайт IIS живёт в секции
// «Публикации IIS» рядом с корневой папкой.
// «Хранение данных» объединяет окна ретенции — аудит, историю использования
// лицензий для /reports и историю размеров баз (DatabaseSize, MLC-185).
const SECTIONS: { id: string; titleKey: string; keys: string[] }[] = [
  {
    id: "settings-cluster",
    titleKey: "settings.sections.cluster",
    keys: [
      "OneC.Cluster.AdminUser",
      "OneC.Cluster.AdminPassword",
      "OneC.RAS.Endpoint",
      "OneC.RAS.AgentPort",
      "OneC.RAS.ExePath",
    ],
  },
  {
    id: "settings-sql",
    titleKey: "settings.sections.sql",
    keys: ["Sql.Server"],
  },
  {
    id: "settings-iis",
    titleKey: "settings.sections.iis",
    keys: ["IIS.DefaultVrdRoot", "IIS.DefaultSiteName"],
  },
  {
    id: "settings-polling",
    titleKey: "settings.sections.polling",
    keys: [
      "Polling.HotIntervalSeconds",
      "Polling.ColdIntervalSeconds",
      "Polling.HotThresholdPercent",
      "Enforcement.KillGraceSeconds",
      "Enforcement.TerminateMessage",
      "Drift.IntervalMinutes",
    ],
  },
  {
    id: "settings-retention",
    titleKey: "settings.sections.retention",
    keys: ["Audit.RetentionDays", "LicenseUsage.RetentionDays", "DatabaseSize.RetentionDays"],
  },
  {
    id: "settings-backup",
    titleKey: "settings.sections.backup",
    keys: [
      "Backup.FolderPath",
      "Backup.TtlHours",
      "Backup.MaxParallel",
      "Backup.DiskSafetyMarginMb",
    ],
  },
];

// Якоря левой навигации (MLC-202): шесть карточек-секций SECTIONS + два блока-компонента
// (служба RAS / обновления), которые не входят в SECTIONS. Подписи короткие — settings.nav.*.
const ANCHORS: { id: string; navKey: string }[] = [
  { id: "settings-cluster", navKey: "settings.nav.cluster" },
  { id: "settings-sql", navKey: "settings.nav.sql" },
  { id: "settings-iis", navKey: "settings.nav.iis" },
  { id: "settings-polling", navKey: "settings.nav.polling" },
  { id: "settings-retention", navKey: "settings.nav.retention" },
  { id: "settings-backup", navKey: "settings.nav.backup" },
  { id: "settings-ras", navKey: "settings.nav.ras" },
  { id: "settings-updates", navKey: "settings.nav.updates" },
];

// Тип ввода + диапазон диктуем со страницы — backend всё равно валидирует
// со своей стороны через SettingDefinitions, но UI хинты для оператора полезны.
const FIELD_META: Record<
  string,
  { type: "text" | "number" | "url" | "password"; min?: number; max?: number; placeholder?: string }
> = {
  "OneC.Cluster.AdminUser": { type: "text" },
  "OneC.Cluster.AdminPassword": { type: "password" },
  // OneC.RAS.Endpoint → RasPortField, OneC.RAS.ExePath → PlatformPicker (см. renderField).
  // MLC-194: порт агента кластера ragent — обычное числовое поле (host фиксирован localhost).
  "OneC.RAS.AgentPort": { type: "number", min: 1024, max: 65535 },
  "IIS.DefaultVrdRoot": { type: "text" },
  "Sql.Server": { type: "text", placeholder: "sql.local или (local)" },
  "IIS.DefaultSiteName": { type: "text", placeholder: "Default Web Site" },
  "Polling.HotIntervalSeconds": { type: "number", min: 2, max: 60 },
  "Polling.ColdIntervalSeconds": { type: "number", min: 10, max: 300 },
  "Polling.HotThresholdPercent": { type: "number", min: 50, max: 100 },
  "Enforcement.KillGraceSeconds": { type: "number", min: 5, max: 120 },
  // MLC-190: свободный текст-причина (одна строка-абзац, 1С сама переносит). Поле SettingField
  // — однострочный input; этого достаточно, текст редактируется и сохраняется как есть.
  "Enforcement.TerminateMessage": { type: "text" },
  "Drift.IntervalMinutes": { type: "number", min: 1, max: 60 },
  "Audit.RetentionDays": { type: "number", min: 30, max: 3650 },
  "LicenseUsage.RetentionDays": { type: "number", min: 30, max: 3650 },
  "DatabaseSize.RetentionDays": { type: "number", min: 30, max: 3650 },
  // Диапазоны зеркалят SettingDefinitions (MLC-076): TtlHours 1..8760, MaxParallel 1..8,
  // DiskSafetyMarginMb 0..1048576 — backend всё равно валидирует со своей стороны.
  "Backup.FolderPath": { type: "text", placeholder: "D:\\Backups" },
  "Backup.TtlHours": { type: "number", min: 1, max: 8760 },
  "Backup.MaxParallel": { type: "number", min: 1, max: 8 },
  "Backup.DiskSafetyMarginMb": { type: "number", min: 0, max: 1048576 },
};

// Scroll-spy: подсвечиваем якорь секции, ближайшей к верху области прокрутки.
// IntersectionObserver с rootMargin, прижатым к верхней кромке вьюпорта (под топбаром
// h-14), отдаёт «активной» секцию, пересекающую узкую полосу у верха. Активный
// якорь — последний из видимых в этой полосе (или, если не пересекается ни один,
// последний прокрученный выше). Подсветка нейтральная (монохром).
function useScrollSpy(ids: string[]): string | null {
  const [activeId, setActiveId] = useState<string | null>(ids[0] ?? null);

  useEffect(() => {
    const elements = ids
      .map((id) => document.getElementById(id))
      .filter((el): el is HTMLElement => el !== null);
    if (elements.length === 0) {
      return;
    }

    const visible = new Map<string, boolean>();

    const observer = new IntersectionObserver(
      (entries) => {
        for (const entry of entries) {
          visible.set(entry.target.id, entry.isIntersecting);
        }
        // Первый по порядку якорь, который сейчас в полосе у верха.
        const firstVisible = ids.find((id) => visible.get(id));
        if (firstVisible) {
          setActiveId(firstVisible);
        }
      },
      // Полоса наблюдения: от ~64px под топбаром до низа минус 60%, чтобы активной
      // становилась секция, доехавшая до верха области прокрутки.
      { rootMargin: "-64px 0px -60% 0px", threshold: 0 }
    );

    for (const el of elements) {
      observer.observe(el);
    }
    return () => observer.disconnect();
  }, [ids]);

  return activeId;
}

export function SettingsPage() {
  const { t } = useTranslation();
  const { data, isLoading, isError } = useSettings();

  const byKey = new Map<string, SettingDescriptor>();
  for (const s of data ?? []) {
    byKey.set(s.key, s);
  }

  const anchorIds = useMemo(() => ANCHORS.map((a) => a.id), []);
  const activeId = useScrollSpy(anchorIds);

  return (
    <div className="space-y-6">
      <div className="space-y-1">
        <h2 className="text-2xl font-semibold tracking-tight">{t("settings.title")}</h2>
        <p className="text-muted-foreground text-sm">{t("settings.subtitle")}</p>
      </div>

      {isError && (
        <div className="border-destructive/40 bg-destructive/5 rounded-md border p-4 text-sm">
          {t("settings.errors.loadFailed")}
        </div>
      )}

      <div className="flex flex-col gap-8 lg:flex-row lg:items-start">
        {/* Левая навигация по секциям-якорям (MLC-202). На узких экранах скрыта —
            контент остаётся полностью доступен прокруткой. */}
        <nav
          aria-label={t("settings.title")}
          className="sticky top-4 hidden w-48 shrink-0 lg:block"
        >
          <ul className="space-y-1">
            {ANCHORS.map((anchor) => {
              const isActive = activeId === anchor.id;
              return (
                <li key={anchor.id}>
                  <a
                    href={`#${anchor.id}`}
                    aria-current={isActive ? "true" : undefined}
                    className={cn(
                      "block rounded-md px-3 py-1.5 text-sm transition-colors",
                      isActive
                        ? "bg-muted text-foreground font-medium"
                        : "text-muted-foreground hover:bg-muted/60 hover:text-foreground"
                    )}
                  >
                    {t(anchor.navKey)}
                  </a>
                </li>
              );
            })}
          </ul>
        </nav>

        {/* Правая колонка — узкая колонка существующих карточек-секций. */}
        <div className="w-full max-w-3xl space-y-6">
          {SECTIONS.map((section) => (
            <Fragment key={section.id}>
              <section id={section.id} className="scroll-mt-20">
                <Card>
                  <CardHeader>
                    <CardTitle>{t(section.titleKey)}</CardTitle>
                    <CardDescription>
                      {t(`${section.titleKey}.description`, { defaultValue: "" })}
                    </CardDescription>
                  </CardHeader>
                  <CardContent className="grid gap-5">
                    {isLoading
                      ? section.keys.map((k) => <Skeleton key={k} className="h-16 w-full" />)
                      : section.keys.map((k) => {
                          const setting = byKey.get(k);
                          if (!setting) {
                            return null;
                          }
                          // Спец-рендер: порт RAS и единый пикер платформы заменяют плоские
                          // поля OneC.RAS.Endpoint / OneC.RAS.ExePath (последний ведёт ещё и
                          // OneC.DefaultPlatformVersion — он в SECTIONS не перечислен).
                          if (k === "OneC.RAS.Endpoint") {
                            return <RasPortField key={k} setting={setting} />;
                          }
                          if (k === "OneC.RAS.ExePath") {
                            return (
                              <PlatformPicker
                                key={k}
                                racSetting={setting}
                                versionSetting={byKey.get("OneC.DefaultPlatformVersion")}
                              />
                            );
                          }
                          // MLC-056: сервер БД — пикер локальных SQL-инстансов (ручной fallback).
                          if (k === "Sql.Server") {
                            return <DatabaseServerField key={k} setting={setting} />;
                          }
                          const meta = FIELD_META[k] ?? { type: "text" as const };
                          return (
                            <SettingField
                              key={k}
                              setting={setting}
                              inputType={meta.type}
                              min={meta.min}
                              max={meta.max}
                              placeholder={meta.placeholder}
                            />
                          );
                        })}
                  </CardContent>
                </Card>
              </section>
            </Fragment>
          ))}

          {/* Две интерактивно-диагностические карточки идут после плоских секций-настроек
            и образуют хвост навигации-якорей (MLC-202): служба RAS → обновления. Порядок
            якорей в DOM совпадает с порядком в ANCHORS, иначе scroll-spy подсвечивал бы
            пункты вразнобой. RasServiceCard/UpdateCheckCard не принимают id — оборачиваем
            в <section> с якорем, сами компоненты не трогаем. */}
          {/* Состояние службы RAS (MLC-160, ADR-47): Admin-only + ленивая диагностика внутри. */}
          <section id="settings-ras" className="scroll-mt-20">
            <RasServiceCard />
          </section>
          {/* MLC-176 (ADR-50): проверка обновлений панели через GitHub Releases. */}
          <section id="settings-updates" className="scroll-mt-20">
            <UpdateCheckCard />
          </section>
        </div>
      </div>
    </div>
  );
}

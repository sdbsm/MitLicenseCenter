import { useTranslation } from "react-i18next";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { Skeleton } from "@/components/ui/skeleton";
import { SettingField } from "./SettingField";
import { PlatformPicker } from "./PlatformPicker";
import { RasPortField } from "./RasPortField";
import { DatabaseServerField } from "./DatabaseServerField";
import type { SettingDescriptor } from "./types";
import { useSettings } from "./useSettings";

// Раскладка секций /settings (MLC-055, перегруппировка MLC-083). Подключение к 1С / RAS
// объединяет креды rac.exe (--cluster-user/--cluster-pwd, ADR-3.3), порт RAS
// (OneC.RAS.Endpoint → RasPortField) и единый пикер платформы (OneC.RAS.ExePath +
// OneC.DefaultPlatformVersion → PlatformPicker, версия в SECTIONS отдельно не
// перечисляется — её ведёт пикер). «SQL Server» — единственное место, где задан
// SQL-инстанс (Defaults.DatabaseServer; ключ настройки исторический, переименование —
// бек-этап 2): формы баз берут значение отсюда, «дефолтом для форм» он больше не
// называется. Сайт IIS живёт в секции «Публикации IIS» рядом с корневой папкой.
// «Хранение данных» объединяет два окна ретенции — аудит и историю использования
// лицензий для /reports.
const SECTIONS: { titleKey: string; keys: string[] }[] = [
  {
    titleKey: "settings.sections.cluster",
    keys: [
      "OneC.Cluster.AdminUser",
      "OneC.Cluster.AdminPassword",
      "OneC.RAS.Endpoint",
      "OneC.RAS.ExePath",
    ],
  },
  {
    titleKey: "settings.sections.sql",
    keys: ["Defaults.DatabaseServer"],
  },
  {
    titleKey: "settings.sections.license",
    keys: ["OneC.LicenseConsumingAppIds"],
  },
  {
    titleKey: "settings.sections.iis",
    keys: ["IIS.DefaultVrdRoot", "IIS.DefaultSiteName"],
  },
  {
    titleKey: "settings.sections.polling",
    keys: [
      "Polling.HotIntervalSeconds",
      "Polling.ColdIntervalSeconds",
      "Polling.HotThresholdPercent",
      "Enforcement.KillGraceSeconds",
      "Drift.IntervalMinutes",
    ],
  },
  {
    titleKey: "settings.sections.retention",
    keys: ["Audit.RetentionDays", "LicenseUsage.RetentionDays"],
  },
  {
    titleKey: "settings.sections.backup",
    keys: [
      "Backup.FolderPath",
      "Backup.TtlHours",
      "Backup.MaxParallel",
      "Backup.DiskSafetyMarginMb",
    ],
  },
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
  "OneC.LicenseConsumingAppIds": {
    type: "text",
    placeholder: "1CV8,1CV8C,WebClient,Designer,COMConnection",
  },
  "IIS.DefaultVrdRoot": { type: "text" },
  "Defaults.DatabaseServer": { type: "text", placeholder: "sql.local или (local)" },
  "IIS.DefaultSiteName": { type: "text", placeholder: "Default Web Site" },
  "Polling.HotIntervalSeconds": { type: "number", min: 2, max: 60 },
  "Polling.ColdIntervalSeconds": { type: "number", min: 10, max: 300 },
  "Polling.HotThresholdPercent": { type: "number", min: 50, max: 100 },
  "Enforcement.KillGraceSeconds": { type: "number", min: 5, max: 120 },
  "Drift.IntervalMinutes": { type: "number", min: 1, max: 60 },
  "Audit.RetentionDays": { type: "number", min: 30, max: 3650 },
  "LicenseUsage.RetentionDays": { type: "number", min: 30, max: 3650 },
  // Диапазоны зеркалят SettingDefinitions (MLC-076): TtlHours 1..8760, MaxParallel 1..8,
  // DiskSafetyMarginMb 0..1048576 — backend всё равно валидирует со своей стороны.
  "Backup.FolderPath": { type: "text", placeholder: "D:\\Backups" },
  "Backup.TtlHours": { type: "number", min: 1, max: 8760 },
  "Backup.MaxParallel": { type: "number", min: 1, max: 8 },
  "Backup.DiskSafetyMarginMb": { type: "number", min: 0, max: 1048576 },
};

export function SettingsPage() {
  const { t } = useTranslation();
  const { data, isLoading, isError } = useSettings();

  const byKey = new Map<string, SettingDescriptor>();
  for (const s of data ?? []) {
    byKey.set(s.key, s);
  }

  return (
    <div className="mx-auto max-w-3xl space-y-6">
      <div className="space-y-1">
        <h2 className="text-2xl font-semibold tracking-tight">{t("settings.title")}</h2>
        <p className="text-muted-foreground text-sm">{t("settings.subtitle")}</p>
      </div>

      {isError && (
        <div className="border-destructive/40 bg-destructive/5 rounded-md border p-4 text-sm">
          {t("settings.errors.loadFailed")}
        </div>
      )}

      {SECTIONS.map((section) => (
        <Card key={section.titleKey}>
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
                  if (k === "Defaults.DatabaseServer") {
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
      ))}
    </div>
  );
}

import { useTranslation } from "react-i18next";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { Skeleton } from "@/components/ui/skeleton";
import { SettingField } from "./SettingField";
import type { SettingDescriptor } from "./types";
import { useSettings } from "./useSettings";

// Группировка по ключам: совпадает с разделами в UI (4 секции).
const SECTIONS: { titleKey: string; keys: string[] }[] = [
  {
    titleKey: "settings.sections.cluster",
    keys: [
      "OneC.Cluster.RestApiUrl",
      "OneC.Cluster.AdminUser",
      "OneC.Cluster.AdminPassword",
      "OneC.Cluster.RestApiTimeoutSeconds",
      "OneC.RAS.Endpoint",
      "OneC.RAS.ExePath",
    ],
  },
  {
    titleKey: "settings.sections.iis",
    keys: ["IIS.ServiceAccount.UserName", "IIS.DefaultVrdRoot"],
  },
  {
    titleKey: "settings.sections.polling",
    keys: [
      "Polling.HotIntervalSeconds",
      "Polling.ColdIntervalSeconds",
      "Polling.HotThresholdPercent",
      "Drift.IntervalMinutes",
    ],
  },
  {
    titleKey: "settings.sections.circuit",
    keys: ["CircuitBreaker.ProbeIntervalSeconds", "CircuitBreaker.FailureCount"],
  },
];

// Тип ввода + диапазон диктуем со страницы — backend всё равно валидирует
// со своей стороны через SettingDefinitions, но UI хинты для оператора полезны.
const FIELD_META: Record<
  string,
  { type: "text" | "number" | "url" | "password"; min?: number; max?: number; placeholder?: string }
> = {
  "OneC.Cluster.RestApiUrl": { type: "url", placeholder: "http://1c-cluster.local:1545" },
  "OneC.Cluster.AdminUser": { type: "text" },
  "OneC.Cluster.AdminPassword": { type: "password" },
  "OneC.Cluster.RestApiTimeoutSeconds": { type: "number", min: 1, max: 30 },
  "OneC.RAS.Endpoint": { type: "text", placeholder: "host:1545" },
  "OneC.RAS.ExePath": { type: "text" },
  "IIS.ServiceAccount.UserName": { type: "text" },
  "IIS.DefaultVrdRoot": { type: "text" },
  "Polling.HotIntervalSeconds": { type: "number", min: 2, max: 60 },
  "Polling.ColdIntervalSeconds": { type: "number", min: 10, max: 300 },
  "Polling.HotThresholdPercent": { type: "number", min: 50, max: 100 },
  "Drift.IntervalMinutes": { type: "number", min: 1, max: 60 },
  "CircuitBreaker.ProbeIntervalSeconds": { type: "number", min: 10, max: 300 },
  "CircuitBreaker.FailureCount": { type: "number", min: 2, max: 10 },
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

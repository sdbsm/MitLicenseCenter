import { useTranslation } from "react-i18next";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { Skeleton } from "@/components/ui/skeleton";
import { SettingField } from "./SettingField";
import { RacPathDetect } from "./RacPathDetect";
import type { SettingDescriptor } from "./types";
import { useSettings } from "./useSettings";

// Stage 5 PR 5.1 (ADR-16): REST adapter и circuit breaker удалены — 3 секции
// вместо 4. OneC.Cluster.AdminUser/AdminPassword остаются в секции «cluster» —
// rac.exe RAS-адаптер использует их для --cluster-user / --cluster-pwd
// (см. ADR-3.3). Секция «defaults» (ADR-17) добавляет 3 form-prefill ключа;
// informational IIS.ServiceAccount.UserName удалён (не читался) → 13 ключей.
const SECTIONS: { titleKey: string; keys: string[] }[] = [
  {
    titleKey: "settings.sections.cluster",
    keys: [
      "OneC.Cluster.AdminUser",
      "OneC.Cluster.AdminPassword",
      "OneC.RAS.Endpoint",
      "OneC.RAS.ExePath",
      "OneC.LicenseConsumingAppIds",
    ],
  },
  {
    titleKey: "settings.sections.iis",
    keys: ["IIS.DefaultVrdRoot"],
  },
  {
    titleKey: "settings.sections.defaults",
    keys: ["Defaults.DatabaseServer", "IIS.DefaultSiteName", "OneC.DefaultPlatformVersion"],
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
];

// Тип ввода + диапазон диктуем со страницы — backend всё равно валидирует
// со своей стороны через SettingDefinitions, но UI хинты для оператора полезны.
const FIELD_META: Record<
  string,
  { type: "text" | "number" | "url" | "password"; min?: number; max?: number; placeholder?: string }
> = {
  "OneC.Cluster.AdminUser": { type: "text" },
  "OneC.Cluster.AdminPassword": { type: "password" },
  "OneC.RAS.Endpoint": { type: "text", placeholder: "host:1545" },
  "OneC.RAS.ExePath": { type: "text" },
  "OneC.LicenseConsumingAppIds": {
    type: "text",
    placeholder: "1CV8,1CV8C,WebClient,Designer,COMConnection",
  },
  "IIS.DefaultVrdRoot": { type: "text" },
  "Defaults.DatabaseServer": { type: "text", placeholder: "sql.local или (local)" },
  "IIS.DefaultSiteName": { type: "text", placeholder: "Default Web Site" },
  "OneC.DefaultPlatformVersion": { type: "text", placeholder: "8.3.23.1865" },
  "Polling.HotIntervalSeconds": { type: "number", min: 2, max: 60 },
  "Polling.ColdIntervalSeconds": { type: "number", min: 10, max: 300 },
  "Polling.HotThresholdPercent": { type: "number", min: 50, max: 100 },
  "Drift.IntervalMinutes": { type: "number", min: 1, max: 60 },
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
                    <div key={k} className="space-y-2">
                      <SettingField
                        setting={setting}
                        inputType={meta.type}
                        min={meta.min}
                        max={meta.max}
                        placeholder={meta.placeholder}
                      />
                      {k === "OneC.RAS.ExePath" ? <RacPathDetect /> : null}
                    </div>
                  );
                })}
          </CardContent>
        </Card>
      ))}
    </div>
  );
}

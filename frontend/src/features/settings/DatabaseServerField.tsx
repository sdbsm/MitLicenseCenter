import { useState } from "react";
import { useTranslation } from "react-i18next";
import { toast } from "sonner";
import { Button } from "@/components/ui/button";
import { Label } from "@/components/ui/label";
import { DiscoveryField } from "@/features/discovery/DiscoveryField";
import { useSqlInstances, toDiscoveryState } from "@/features/discovery/useDiscovery";
import type { SettingDescriptor } from "./types";
import { useUpdateSetting } from "./useSettings";

// MLC-056 — поле «Сервер СУБД по умолчанию» (Defaults.DatabaseServer): пикер
// локальных SQL-инстансов (GET /discovery/sql-instances) с ручным fallback.
// Зеркалит VersionEscapeField из PlatformPicker: локальный draft + явный Save +
// ресинк при внешней мутации. DiscoveryField.onChange срабатывает на каждый keystroke
// ручного ввода — поэтому НЕ save-on-change (страница пер-контрольная), только через кнопку.
export function DatabaseServerField({ setting }: { setting: SettingDescriptor }) {
  const { t } = useTranslation();
  const update = useUpdateSetting();
  const query = useSqlInstances(true);
  const state = toDiscoveryState(query);
  const options = (query.data?.items ?? []).map((s) => ({ value: s, label: s }));

  const serverValue = setting.value ?? "";
  const [draft, setDraft] = useState(serverValue);
  const [syncedValue, setSyncedValue] = useState(serverValue);
  if (serverValue !== syncedValue) {
    setSyncedValue(serverValue);
    setDraft(serverValue);
  }

  const label = t(`settings.labels.${setting.key}`, { defaultValue: setting.key });
  const hint = t(`settings.hints.${setting.key}`, { defaultValue: "" });
  const isDirty = draft !== serverValue;

  const handleSave = async () => {
    try {
      await update.mutateAsync({
        key: setting.key,
        value: draft.trim().length > 0 ? draft.trim() : null,
      });
      toast.success(
        draft.trim().length > 0 ? t("settings.toasts.saved") : t("settings.toasts.cleared")
      );
    } catch {
      toast.error(t("errors.generic"));
    }
  };

  return (
    <div className="grid gap-2">
      <Label className="text-sm">{label}</Label>
      {hint && <p className="text-muted-foreground text-xs">{hint}</p>}
      <div className="flex items-start gap-2">
        <div className="flex-1">
          <DiscoveryField
            value={draft}
            onChange={setDraft}
            options={options}
            available={state.available}
            loading={state.loading}
            error={state.error}
            onRefresh={() => void query.refetch()}
            manualPlaceholder="localhost"
          />
        </div>
        <Button size="sm" onClick={() => void handleSave()} disabled={!isDirty || update.isPending}>
          {t("settings.actions.save")}
        </Button>
      </div>
    </div>
  );
}

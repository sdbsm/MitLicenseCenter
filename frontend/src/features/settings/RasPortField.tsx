import { useState } from "react";
import { useTranslation } from "react-i18next";
import { toast } from "sonner";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { ApiError } from "@/lib/api";
import type { SettingDescriptor } from "./types";
import { parseRasPort, buildRasEndpoint } from "./parsing";
import { useUpdateSetting } from "./useSettings";

interface ValidationProblemBody {
  errors?: Record<string, string[]>;
}

// Поле «Порт RAS» для ключа OneC.RAS.Endpoint. RAS всегда localhost (single-node
// топология), поэтому оператор задаёт только порт; в БД пишем wire-формат
// localhost:<порт> — бэкенд (ADR-3.3) читает host:port без изменений. Структура
// повторяет плоскую ветку SettingField (label/hint/input/Save/Cancel + ресинк draft
// при внешней смене значения).
export function RasPortField({ setting }: { setting: SettingDescriptor }) {
  const { t } = useTranslation();
  const update = useUpdateSetting();

  const serverPort = parseRasPort(setting.value);
  const [draft, setDraft] = useState(String(serverPort));
  // Ресинхронизация draft, если значение сменилось извне (паттерн SettingField).
  const [syncedValue, setSyncedValue] = useState(setting.value ?? "");
  if ((setting.value ?? "") !== syncedValue) {
    setSyncedValue(setting.value ?? "");
    setDraft(String(parseRasPort(setting.value)));
  }
  const [serverError, setServerError] = useState<string | null>(null);

  const label = t("settings.rasPort.label");
  const hint = t("settings.rasPort.hint");

  const isDirty = draft !== String(serverPort);

  const handleSave = async () => {
    setServerError(null);
    const port = Number.parseInt(draft.trim(), 10);
    try {
      await update.mutateAsync({ key: setting.key, value: buildRasEndpoint(port) });
      toast.success(t("settings.toasts.saved"));
    } catch (error) {
      if (error instanceof ApiError && error.status === 400) {
        const body = error.body as ValidationProblemBody | null;
        const fieldErrors = body?.errors ?? {};
        const message = fieldErrors.Value?.[0] ?? fieldErrors.value?.[0] ?? t("errors.generic");
        setServerError(message);
        return;
      }
      toast.error(t("errors.generic"));
    }
  };

  return (
    <div className="grid gap-2">
      <Label htmlFor={`setting-${setting.key}`} className="text-sm">
        {label}
      </Label>
      {hint && <p className="text-muted-foreground text-xs">{hint}</p>}

      <div className="flex gap-2">
        <Input
          id={`setting-${setting.key}`}
          type="number"
          min={1024}
          max={65535}
          className="max-w-[10rem]"
          value={draft}
          onChange={(e) => setDraft(e.target.value)}
        />
        <Button size="sm" onClick={() => void handleSave()} disabled={!isDirty || update.isPending}>
          {t("settings.actions.save")}
        </Button>
        {isDirty && (
          <Button
            size="sm"
            variant="ghost"
            onClick={() => {
              setDraft(String(serverPort));
              setServerError(null);
            }}
            disabled={update.isPending}
          >
            {t("settings.actions.cancel")}
          </Button>
        )}
      </div>

      {serverError && <p className="text-destructive text-xs">{serverError}</p>}
    </div>
  );
}

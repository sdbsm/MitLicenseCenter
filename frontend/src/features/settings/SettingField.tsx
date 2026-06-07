import { useState } from "react";
import { useTranslation } from "react-i18next";
import { toast } from "sonner";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { ApiError } from "@/lib/api";
import type { SettingDescriptor } from "./types";
import { useUpdateSetting } from "./useSettings";

interface ValidationProblemBody {
  errors?: Record<string, string[]>;
}

export interface SettingFieldProps {
  setting: SettingDescriptor;
  inputType?: "text" | "number" | "url" | "password";
  min?: number;
  max?: number;
  placeholder?: string;
}

// Plain поле: всегда показано значение, Save сохраняет diff.
// Secret поле: «Не задано» / «Задано (скрыто)» индикатор + «Изменить» раскрывает password input.
export function SettingField({
  setting,
  inputType = "text",
  min,
  max,
  placeholder,
}: SettingFieldProps) {
  const { t } = useTranslation();
  const update = useUpdateSetting();

  const serverValue = setting.value ?? "";
  const [draft, setDraft] = useState(serverValue);
  // Серверное значение может смениться ИЗВНЕ этого поля: для OneC.RAS.ExePath
  // соседний PlatformPicker сохраняет путь своей мутацией (выбор платформы), после
  // чего список настроек рефетчится и сюда приходит новый `setting.value`. useState
  // инициализирует draft лишь однажды, поэтому без ресинхронизации поле осталось
  // бы со старым (обычно пустым) draft при активной кнопке Save — клик слал бы
  // этот пустой draft поверх свежего пути и затирал его в null. Паттерн «храним
  // предыдущий проп»: ресинхронизируем draft РОВНО когда серверное значение
  // реально изменилось (см. react.dev/learn/you-might-not-need-an-effect —
  // adjusting state during render, без useEffect и лишнего ререндера-кадра).
  const [syncedValue, setSyncedValue] = useState(serverValue);
  if (serverValue !== syncedValue) {
    setSyncedValue(serverValue);
    setDraft(serverValue);
  }
  const [editingSecret, setEditingSecret] = useState(false);
  const [secretDraft, setSecretDraft] = useState("");
  const [serverError, setServerError] = useState<string | null>(null);

  const label = t(`settings.labels.${setting.key}`, { defaultValue: setting.key });
  const hint = t(`settings.hints.${setting.key}`, { defaultValue: "" });

  const handleSave = async (value: string | null) => {
    setServerError(null);
    try {
      await update.mutateAsync({ key: setting.key, value });
      toast.success(value === null ? t("settings.toasts.cleared") : t("settings.toasts.saved"));
      if (setting.isSecret) {
        setEditingSecret(false);
        setSecretDraft("");
      }
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

  if (setting.isSecret) {
    return (
      <div className="grid gap-2">
        <Label className="text-sm">{label}</Label>
        {hint && <p className="text-muted-foreground text-xs">{hint}</p>}

        {!editingSecret ? (
          <div className="flex items-center gap-2">
            {setting.isSet ? (
              <Badge variant="secondary">{t("settings.states.secretSet")}</Badge>
            ) : (
              <Badge variant="outline">{t("settings.states.notSet")}</Badge>
            )}
            <Button
              size="sm"
              variant="outline"
              onClick={() => setEditingSecret(true)}
              disabled={update.isPending}
            >
              {setting.isSet ? t("settings.actions.edit") : t("settings.actions.set")}
            </Button>
            {setting.isSet && (
              <Button
                size="sm"
                variant="ghost"
                onClick={() => void handleSave(null)}
                disabled={update.isPending}
              >
                {t("settings.actions.clear")}
              </Button>
            )}
          </div>
        ) : (
          <div className="flex flex-col gap-2">
            <Input
              type="password"
              autoComplete="new-password"
              placeholder="***"
              value={secretDraft}
              onChange={(e) => setSecretDraft(e.target.value)}
            />
            <div className="flex gap-2">
              <Button
                size="sm"
                onClick={() => void handleSave(secretDraft.trim().length > 0 ? secretDraft : null)}
                disabled={update.isPending}
              >
                {t("settings.actions.save")}
              </Button>
              <Button
                size="sm"
                variant="ghost"
                onClick={() => {
                  setEditingSecret(false);
                  setSecretDraft("");
                  setServerError(null);
                }}
                disabled={update.isPending}
              >
                {t("settings.actions.cancel")}
              </Button>
            </div>
          </div>
        )}

        {serverError && <p className="text-destructive text-xs">{serverError}</p>}
      </div>
    );
  }

  const isDirty = serverValue !== draft;
  return (
    <div className="grid gap-2">
      <Label htmlFor={`setting-${setting.key}`} className="text-sm">
        {label}
      </Label>
      {hint && <p className="text-muted-foreground text-xs">{hint}</p>}

      <div className="flex gap-2">
        <Input
          id={`setting-${setting.key}`}
          type={inputType}
          min={min}
          max={max}
          placeholder={placeholder}
          value={draft}
          onChange={(e) => setDraft(e.target.value)}
        />
        <Button
          size="sm"
          onClick={() => void handleSave(draft.trim().length > 0 ? draft.trim() : null)}
          disabled={!isDirty || update.isPending}
        >
          {t("settings.actions.save")}
        </Button>
        {isDirty && (
          <Button
            size="sm"
            variant="ghost"
            onClick={() => {
              setDraft(serverValue);
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

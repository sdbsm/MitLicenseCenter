import { useState } from "react";
import { useTranslation } from "react-i18next";
import { ChevronDown } from "lucide-react";
import { toast } from "sonner";
import { Button } from "@/components/ui/button";
import { Label } from "@/components/ui/label";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import { DiscoveryField } from "@/features/discovery/DiscoveryField";
import {
  useRacPaths,
  usePlatformVersions,
  toDiscoveryState,
} from "@/features/discovery/useDiscovery";
import { SettingField } from "./SettingField";
import type { SettingDescriptor } from "./types";
import { parsePlatformVersionFromRacPath } from "./parsing";
import { useUpdateSetting } from "./useSettings";

interface PlatformPickerProps {
  // OneC.RAS.ExePath — путь к rac.exe (обязателен на странице).
  racSetting: SettingDescriptor;
  // OneC.DefaultPlatformVersion — версия по умолчанию для новых публикаций.
  versionSetting: SettingDescriptor | undefined;
}

// Единый блок «Платформа 1С». Выбор установленной платформы из списка rac.exe
// (GET /discovery/rac-paths) сохраняет СРАЗУ два раздельных ключа: путь rac.exe
// (OneC.RAS.ExePath) и версию по умолчанию (OneC.DefaultPlatformVersion, парсится
// из пути ...\1cv8\<version>\bin\rac.exe). Для редкого расхождения (нестандартный
// путь / версия без rac.exe) свёрнутый escape-hatch разводит ключи врозь.
// Ключи в БД остаются раздельными — сливается только UI-подача (ADR-3.3).
export function PlatformPicker({ racSetting, versionSetting }: PlatformPickerProps) {
  const { t } = useTranslation();
  const update = useUpdateSetting();
  const [escapeOpen, setEscapeOpen] = useState(false);

  const racQuery = useRacPaths(true);
  const racState = toDiscoveryState(racQuery);
  const racPaths = racQuery.data?.items ?? [];

  const currentPath = racSetting.value ?? "";
  const currentVersion = versionSetting?.value ?? "";

  // Выбор платформы → две мутации одним действием (ключи раздельны).
  const handlePick = async (path: string) => {
    try {
      await update.mutateAsync({ key: "OneC.RAS.ExePath", value: path });
      const version = parsePlatformVersionFromRacPath(path);
      if (version) {
        await update.mutateAsync({ key: "OneC.DefaultPlatformVersion", value: version });
      }
      toast.success(t("settings.toasts.platformSaved"));
    } catch {
      toast.error(t("errors.generic"));
    }
  };

  return (
    <div className="grid gap-2">
      <Label className="text-sm">{t("settings.platform.label")}</Label>
      <p className="text-muted-foreground text-xs">{t("settings.platform.hint")}</p>

      <div className="flex items-center gap-2">
        <Select
          value={currentPath || undefined}
          onValueChange={(v) => void handlePick(v)}
          disabled={racState.loading || update.isPending}
        >
          <SelectTrigger className="w-full">
            <SelectValue
              placeholder={
                racState.loading ? t("discovery.loading") : t("settings.platform.selectPlaceholder")
              }
            />
          </SelectTrigger>
          <SelectContent>
            {racPaths.map((p) => {
              const version = parsePlatformVersionFromRacPath(p);
              return (
                <SelectItem key={p} value={p}>
                  {version ? `${version} — ${p}` : p}
                </SelectItem>
              );
            })}
          </SelectContent>
        </Select>
        <Button
          type="button"
          variant="outline"
          size="sm"
          onClick={() => void racQuery.refetch()}
          disabled={racState.loading}
        >
          {racState.loading ? t("discovery.loading") : t("discovery.refresh")}
        </Button>
      </div>

      {!racState.loading && racState.available && racPaths.length === 0 ? (
        <p className="text-muted-foreground text-xs">{t("discovery.racNotFound")}</p>
      ) : null}
      {!racState.available ? (
        <p className="text-muted-foreground text-xs">{t("discovery.unavailable")}</p>
      ) : null}

      {/* Текущее состояние обоих ключей. */}
      <p className="text-muted-foreground text-xs">
        {t("settings.platform.current", {
          path: currentPath || "—",
          version: currentVersion || "—",
        })}
      </p>

      <button
        type="button"
        onClick={() => setEscapeOpen((o) => !o)}
        aria-expanded={escapeOpen}
        className="text-muted-foreground flex items-center gap-2 text-xs font-medium"
      >
        <ChevronDown className={`size-3 transition-transform ${escapeOpen ? "rotate-180" : ""}`} />
        {t("settings.platform.advancedToggle")}
      </button>

      {escapeOpen && (
        <div className="border-muted grid gap-5 border-l-2 pl-4">
          <SettingField
            setting={racSetting}
            inputType="text"
            placeholder={"C:\\Program Files\\1cv8\\8.3.23.1865\\bin\\rac.exe"}
          />
          {versionSetting ? <VersionEscapeField setting={versionSetting} /> : null}
        </div>
      )}
    </div>
  );
}

// Версия по умолчанию в escape-hatch: DiscoveryField (полный список платформ, в т.ч.
// без rac.exe) + локальный draft + явный Save. Мост между «save-on-change» семантикой
// DiscoveryField (onChange на каждый keystroke ручного ввода) и пер-контрольным
// сохранением страницы настроек. Draft ресинхронизируется при внешней мутации.
function VersionEscapeField({ setting }: { setting: SettingDescriptor }) {
  const { t } = useTranslation();
  const update = useUpdateSetting();
  const query = usePlatformVersions(true);
  const state = toDiscoveryState(query);
  const options = (query.data?.items ?? []).map((v) => ({
    value: v.version,
    label: v.version,
    hint: v.architecture,
  }));

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
            manualPlaceholder="8.3.23.1865"
            inputClassName="font-mono text-xs"
          />
        </div>
        <Button size="sm" onClick={() => void handleSave()} disabled={!isDirty || update.isPending}>
          {t("settings.actions.save")}
        </Button>
      </div>
    </div>
  );
}

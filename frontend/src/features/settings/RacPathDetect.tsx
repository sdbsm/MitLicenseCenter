import { useState } from "react";
import { useTranslation } from "react-i18next";
import { toast } from "sonner";
import { Button } from "@/components/ui/button";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import { useRacPaths } from "@/features/discovery/useDiscovery";
import { useUpdateSetting } from "./useSettings";

// Автодетект пути к rac.exe для поля OneC.RAS.ExePath. Изолирован от SettingField:
// сканирует ФС на бэке (GET /discovery/rac-paths) и сохраняет выбранный путь через
// тот же PUT /settings, что и ручной ввод. После сохранения список настроек
// инвалидируется → SettingField показывает новое значение.
export function RacPathDetect() {
  const { t } = useTranslation();
  const [triggered, setTriggered] = useState(false);
  const [picked, setPicked] = useState("");
  const query = useRacPaths(triggered);
  const update = useUpdateSetting();

  const paths = query.data?.items ?? [];

  const onDetect = () => {
    if (!triggered) {
      setTriggered(true);
    } else {
      void query.refetch();
    }
  };

  const save = async (value: string) => {
    try {
      await update.mutateAsync({ key: "OneC.RAS.ExePath", value });
      toast.success(t("discovery.racApplied"));
    } catch {
      toast.error(t("errors.generic"));
    }
  };

  return (
    <div className="flex flex-wrap items-center gap-2">
      <Button
        type="button"
        variant="outline"
        size="sm"
        onClick={onDetect}
        disabled={query.isFetching}
      >
        {query.isFetching ? t("discovery.loading") : t("discovery.racDetect")}
      </Button>

      {triggered && !query.isFetching && paths.length === 0 ? (
        <span className="text-muted-foreground text-xs">{t("discovery.racNotFound")}</span>
      ) : null}

      {paths.length === 1 ? (
        <>
          <code className="text-muted-foreground text-xs">{paths[0]}</code>
          <Button
            type="button"
            size="sm"
            onClick={() => void save(paths[0])}
            disabled={update.isPending}
          >
            {t("discovery.racApply")}
          </Button>
        </>
      ) : null}

      {paths.length > 1 ? (
        <>
          <Select value={picked} onValueChange={setPicked}>
            <SelectTrigger className="min-w-[18rem]">
              <SelectValue placeholder={t("discovery.selectPlaceholder")} />
            </SelectTrigger>
            <SelectContent>
              {paths.map((p) => (
                <SelectItem key={p} value={p}>
                  {p}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>
          <Button
            type="button"
            size="sm"
            onClick={() => picked && void save(picked)}
            disabled={!picked || update.isPending}
          >
            {t("discovery.racApply")}
          </Button>
        </>
      ) : null}
    </div>
  );
}

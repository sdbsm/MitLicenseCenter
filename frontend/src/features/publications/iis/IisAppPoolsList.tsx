import { PlayIcon, RotateCwIcon, SquareIcon } from "lucide-react";
import { useTranslation } from "react-i18next";
import { Button } from "@/components/ui/button";
import { IisStateBadge } from "./IisStateBadge";
import type { IisAppPool } from "./iisTypes";

interface IisAppPoolsListProps {
  pools: IisAppPool[];
  isAdmin: boolean;
  busy: boolean;
  onRecycle: (name: string) => void;
  onStart: (name: string) => void;
  onStop: (name: string) => void;
}

// MLC-047: список пулов приложений с состоянием и действиями (recycle/start/stop).
// Start показывается для остановленных, Stop — для запущенных; Recycle — всегда.
export function IisAppPoolsList({
  pools,
  isAdmin,
  busy,
  onRecycle,
  onStart,
  onStop,
}: IisAppPoolsListProps) {
  const { t } = useTranslation();

  if (pools.length === 0) {
    return <p className="text-muted-foreground text-sm">{t("publications.iis.pools.empty")}</p>;
  }

  return (
    <ul className="divide-y rounded-md border">
      {pools.map((pool) => {
        const stopped = pool.state === "Stopped" || pool.state === "Stopping";
        return (
          <li key={pool.name} className="flex items-center justify-between gap-4 px-3 py-2">
            <div className="flex min-w-0 items-center gap-3">
              <span className="truncate font-medium">{pool.name}</span>
              <IisStateBadge state={pool.state} />
            </div>
            {isAdmin && (
              <div className="flex shrink-0 gap-2">
                <Button
                  size="sm"
                  variant="outline"
                  disabled={busy}
                  onClick={() => onRecycle(pool.name)}
                >
                  <RotateCwIcon className="size-4" />
                  {t("publications.iis.actions.recycle")}
                </Button>
                {stopped ? (
                  <Button
                    size="sm"
                    variant="outline"
                    disabled={busy}
                    className="border-transparent bg-emerald-600 text-white hover:bg-emerald-700"
                    onClick={() => onStart(pool.name)}
                  >
                    <PlayIcon className="size-4" />
                    {t("publications.iis.actions.start")}
                  </Button>
                ) : (
                  <Button
                    size="sm"
                    variant="destructive"
                    disabled={busy}
                    onClick={() => onStop(pool.name)}
                  >
                    <SquareIcon className="size-4" />
                    {t("publications.iis.actions.stop")}
                  </Button>
                )}
              </div>
            )}
          </li>
        );
      })}
    </ul>
  );
}

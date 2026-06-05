import { PlayIcon, RefreshCwIcon, SquareIcon } from "lucide-react";
import { useTranslation } from "react-i18next";
import { Button } from "@/components/ui/button";
import { IisStateBadge } from "./IisStateBadge";
import type { IisSiteState } from "./iisTypes";

interface IisSitesListProps {
  sites: IisSiteState[];
  isAdmin: boolean;
  busy: boolean;
  onStart: (name: string) => void;
  onStop: (name: string) => void;
  onRestart: (name: string) => void;
}

// MLC-047: список сайтов IIS с состоянием и действиями (start/stop/restart).
export function IisSitesList({
  sites,
  isAdmin,
  busy,
  onStart,
  onStop,
  onRestart,
}: IisSitesListProps) {
  const { t } = useTranslation();

  if (sites.length === 0) {
    return <p className="text-muted-foreground text-sm">{t("publications.iis.sites.empty")}</p>;
  }

  return (
    <ul className="divide-y rounded-md border">
      {sites.map((site) => {
        const stopped = site.state === "Stopped" || site.state === "Stopping";
        return (
          <li key={site.siteName} className="flex items-center justify-between gap-4 px-3 py-2">
            <div className="flex min-w-0 items-center gap-3">
              <span className="truncate font-medium">{site.siteName}</span>
              <IisStateBadge state={site.state} />
            </div>
            {isAdmin && (
              <div className="flex shrink-0 gap-2">
                <Button
                  size="sm"
                  variant="outline"
                  disabled={busy}
                  onClick={() => onRestart(site.siteName)}
                >
                  <RefreshCwIcon className="size-4" />
                  {t("publications.iis.actions.restart")}
                </Button>
                {stopped ? (
                  <Button
                    size="sm"
                    variant="outline"
                    disabled={busy}
                    className="border-transparent bg-emerald-600 text-white hover:bg-emerald-700"
                    onClick={() => onStart(site.siteName)}
                  >
                    <PlayIcon className="size-4" />
                    {t("publications.iis.actions.start")}
                  </Button>
                ) : (
                  <Button
                    size="sm"
                    variant="destructive"
                    disabled={busy}
                    onClick={() => onStop(site.siteName)}
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

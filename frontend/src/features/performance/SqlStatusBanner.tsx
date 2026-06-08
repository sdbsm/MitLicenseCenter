import { ServerOffIcon, ShieldAlertIcon } from "lucide-react";
import { useTranslation } from "react-i18next";
import type { SqlProbeStatus } from "./types";

interface SqlStatusBannerProps {
  // Только degraded-статусы (PermissionDenied/Unavailable) — на Ok баннер не рисуется.
  status: Exclude<SqlProbeStatus, "Ok">;
}

/**
 * Честный degraded-сигнал DMV-пробы (MLC-069, образец MLC-064a / IIS-permissions). Когда у
 * учётки backend'а нет `VIEW SERVER STATE` (`PermissionDenied`) или SQL недоступен/строка не
 * настроена (`Unavailable`), снимок приходит с пустыми списками — вместо тихого «всё спокойно»
 * называем причину и что делать. PermissionDenied — амбер (исправимо grant'ом), Unavailable —
 * нейтральный (инфраструктура).
 */
export function SqlStatusBanner({ status }: SqlStatusBannerProps) {
  const { t } = useTranslation();

  if (status === "PermissionDenied") {
    return (
      <div className="flex items-center gap-3 rounded-md border border-amber-600/40 bg-amber-600/5 p-3 text-sm font-medium text-amber-700 dark:text-amber-300">
        <ShieldAlertIcon className="h-5 w-5 shrink-0" />
        <span>{t("performance.sql.status.permissionDenied")}</span>
      </div>
    );
  }

  return (
    <div className="text-muted-foreground flex items-center gap-3 rounded-md border p-3 text-sm">
      <ServerOffIcon className="h-5 w-5 shrink-0" />
      <span>{t("performance.sql.status.unavailable")}</span>
    </div>
  );
}

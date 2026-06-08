import { ShieldAlertIcon } from "lucide-react";
import { useTranslation } from "react-i18next";

interface AttributionWarningBannerProps {
  // Сколько процессов backend не смог прочитать из-за нехватки прав.
  processesInaccessible: number;
}

/**
 * Честный сигнал неполноты атрибуции (MLC-064a). Если backend не смог прочитать
 * часть процессов (нехватка прав), их CPU/RAM выпали из семей и раздел рискует
 * показать ложное «всё Прочее». Баннер по образцу IIS-permissions / readiness:
 * называем причину и что делать, вместо тихого искажения данных.
 */
export function AttributionWarningBanner({ processesInaccessible }: AttributionWarningBannerProps) {
  const { t } = useTranslation();

  return (
    <div className="flex items-center gap-3 rounded-md border border-amber-600/40 bg-amber-600/5 p-3 text-sm font-medium text-amber-700 dark:text-amber-300">
      <ShieldAlertIcon className="h-5 w-5 shrink-0" />
      <span>{t("performance.attributionWarning", { count: processesInaccessible })}</span>
    </div>
  );
}

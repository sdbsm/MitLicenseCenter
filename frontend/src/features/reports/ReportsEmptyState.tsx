import { LineChartIcon } from "lucide-react";
import { useTranslation } from "react-i18next";

/** Empty-state «данные накапливаются» — пустой ряд приходит как 200, не ошибка
 *  (телеметрия копится с релиза MLC-048). Образец — empty-state AuditTable. */
export function ReportsEmptyState() {
  const { t } = useTranslation();
  return (
    <div className="flex h-[320px] flex-col items-center justify-center gap-3 text-center">
      <LineChartIcon className="text-muted-foreground size-8" />
      <div className="space-y-1">
        <p className="font-medium">{t("reports.empty.title")}</p>
        <p className="text-muted-foreground text-sm">{t("reports.empty.hint")}</p>
      </div>
    </div>
  );
}

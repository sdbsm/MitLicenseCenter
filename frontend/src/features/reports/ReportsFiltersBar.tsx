import { useTranslation } from "react-i18next";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import type { ReportsFilters } from "./types";

interface ReportsFiltersBarProps {
  filters: ReportsFilters;
  onChange: (next: ReportsFilters) => void;
}

/** Фильтр периода (общий на сводку и детализацию). Без указанных границ сервер
 *  берёт последние 7 дней; ширину >31 дня кламп двигает сам (эффективный диапазон —
 *  в подписи сводки). Образец — AuditFiltersBar (только даты + сброс). */
export function ReportsFiltersBar({ filters, onChange }: ReportsFiltersBarProps) {
  const { t } = useTranslation();

  const update = (patch: Partial<ReportsFilters>) => {
    onChange({ ...filters, ...patch });
  };

  const hasPeriod = filters.from !== null || filters.to !== null;

  return (
    <div className="bg-muted/30 flex flex-wrap items-end gap-3 rounded-md border p-3">
      <div className="grid gap-1.5">
        <Label className="text-xs font-medium" htmlFor="reports-from">
          {t("reports.filters.from")}
        </Label>
        <Input
          id="reports-from"
          type="date"
          className="w-40"
          value={filters.from ?? ""}
          onChange={(e) => update({ from: e.target.value || null })}
        />
      </div>

      <div className="grid gap-1.5">
        <Label className="text-xs font-medium" htmlFor="reports-to">
          {t("reports.filters.to")}
        </Label>
        <Input
          id="reports-to"
          type="date"
          className="w-40"
          value={filters.to ?? ""}
          onChange={(e) => update({ to: e.target.value || null })}
        />
      </div>

      {hasPeriod && (
        <Button variant="ghost" size="sm" onClick={() => update({ from: null, to: null })}>
          {t("common.reset")}
        </Button>
      )}
    </div>
  );
}
